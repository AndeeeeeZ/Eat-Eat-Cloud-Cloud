using PurrNet;
using UnityEngine;
using System.Collections.Generic;
using System;
using PurrNet.Packing;

public struct MP_RoundResult : IPackedAuto, IEquatable<MP_RoundResult>
{
    public bool HasWinner;
    public string WinnerName;
    public ulong WinnerObjectId;

    public MP_RoundResult(string winnerName, ulong winnerObjectId)
    {
        HasWinner = true;
        WinnerName = winnerName;
        WinnerObjectId = winnerObjectId;
    }

    public bool Equals(MP_RoundResult other)
    {
        return HasWinner == other.HasWinner && WinnerName == other.WinnerName &&
            WinnerObjectId == other.WinnerObjectId;
    }
}

// Server-side connection records survive the death of a player's cloud

[DefaultExecutionOrder(-5)]
public class MP_PlayerManager : NetworkBehaviour
{
    [Header("Respawning")]
    [SerializeField] private MP_Player playerPrefab;
    [SerializeField] private PlayerSpawner spawnPointsSource;
    [SerializeField] private MP_PlayerData localPlayerData;
    private int nextRespawnPoint;

    private const float StartReadinessRetrySeconds = 1f;
    private const float StartRequestTimeoutSeconds = 10f;
    private bool serverReadyToStart;
    private bool startPending;
    private float nextStartReadinessCheck;
    private float startRequestedAt;
    private string startFailure = "";

    [Header("Safe Spawning")]
    [SerializeField, Min(0f)] private float spawnClearance = 0.5f;
    [SerializeField, Min(1)] private int spawnSearchAttempts = 64;
    private readonly List<MP_Player> pendingClouds = new();
    private readonly List<MP_SpawnSafety.Space> occupiedSpaces = new();

    public static MP_PlayerManager Instance { get; private set; }
    private readonly List<MP_PlayerStats> players = new();
    public IReadOnlyList<MP_PlayerStats> Players => players;
    private readonly SyncVar<MP_RoundResult> roundResult = new(default);
    public bool RoundEnded => isSpawned && roundResult.value.HasWinner;
    public string WinnerName => roundResult.value.WinnerName ?? "";
    public bool IsWinner(MP_Player player) => RoundEnded && player != null &&
        player.objectId == roundResult.value.WinnerObjectId;
    public bool CanStart => localPlayerData != null && IsClientPrepared() &&
        serverReadyToStart && !startPending &&
        !RoundEnded && MP_CycleTimer.Instance.CurrentCycle <= 1 &&
        MP_LocalPlayerManager.Instance.LocalPlayer == null &&
        !MP_LocalPlayerManager.Instance.HasDied;
    public bool CanSpectate => IsClientPrepared() &&
        MP_LocalPlayerManager.Instance.LocalPlayer == null;
    public string StartStatus
    {
        get
        {
            MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
            if (RoundEnded)
                return "Round ended.";
            if (MP_CycleTimer.Instance != null && MP_CycleTimer.Instance.CurrentCycle > 1)
                return "Joining is closed. Spectating this round.";
            if (startPending)
                return "Starting...";
            if (!string.IsNullOrEmpty(startFailure))
                return startFailure;
            if (localPlayerData == null)
                return "Player data is not configured.";
            if (!IsClientPrepared())
                return "Preparing match...";
            if (!serverReadyToStart)
                return "Waiting for server...";
            if (local != null && local.LocalPlayer != null)
                return "Playing";
            return "Ready to start";
        }
    }

    public sealed class PlayerSession
    {
        public MP_Player Cloud { get; internal set; }
        public string PlayerName { get; internal set; } = "";
        public MP_CostumeIndices Costume { get; internal set; }
        public bool IsAlive => Cloud != null && Cloud.IsAlive;
        public bool CanRespawn { get; internal set; }
        public bool ReadyToStart { get; internal set; }
    }

    private readonly Dictionary<PlayerID, PlayerSession> sessions = new();
    private bool resettingRound;
    private bool resetPending;
    // Populated on the server only. Counts include living AI clouds
    public IReadOnlyDictionary<PlayerID, PlayerSession> Sessions => sessions;
    private NetworkManager sessionNetworkManager;

    public event Action<int> OnPlayerCountChanged;
    public event Action<int> OnAIPlayerCountChanged;
    public event Action OnRoundEnded;
    private readonly SyncVar<int> playerCount = new(0);
    private readonly SyncVar<int> aiPlayerCount = new(0);
    public int PlayerCount => playerCount.value;
    public int AIPlayerCount => aiPlayerCount.value;
    public int HumanPlayerCount => playerCount.value - aiPlayerCount.value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        playerCount.onChanged += HandlePlayerCountChange; 
        aiPlayerCount.onChanged += HandleAIPlayerCountChange;
        roundResult.onChanged += HandleRoundResultChanged;

        if (spawnPointsSource != null)
        {
            // Keep spawn points available while preventing PurrNet's automatic join spawn
            spawnPointsSource.enabled = false;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        playerCount.onChanged -= HandlePlayerCountChange; 
        aiPlayerCount.onChanged -= HandleAIPlayerCountChange;
        roundResult.onChanged -= HandleRoundResultChanged;

        if (Instance == this)
            Instance = null; 
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (!asServer)
        {
            ResetClientStartState();
            return;
        }

        sessionNetworkManager = networkManager;
        roundResult.value = default;
        sessionNetworkManager.onPlayerJoined += HandlePlayerJoined;
        sessionNetworkManager.onPlayerLeft += HandlePlayerLeft;

        foreach (PlayerID player in sessionNetworkManager.players)
            HandlePlayerJoined(player, false, true);
    }

    protected override void OnDespawned(bool asServer)
    {
        base.OnDespawned(asServer);
        if (!asServer)
        {
            ResetClientStartState();
            return;
        }

        if (sessionNetworkManager != null)
        {
            sessionNetworkManager.onPlayerJoined -= HandlePlayerJoined;
            sessionNetworkManager.onPlayerLeft -= HandlePlayerLeft;
            sessionNetworkManager = null;
        }

        sessions.Clear();
        players.Clear();
        pendingClouds.Clear();
        resetPending = false;
        nextRespawnPoint = 0;
        playerCount.value = 0;
        aiPlayerCount.value = 0;
        roundResult.value = default;
    }

    private void Update()
    {
        if (isClient && IsSpawned(false))
            UpdateClientStartReadiness();

        if (!isServer || !isSpawned || resettingRound)
            return;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        if (sessions.Count == 0 && (resetPending || players.Count > 0 ||
            pendingClouds.Count > 0 || RoundEnded ||
            (timer != null && timer.CurrentCycle > 0)))
        {
            ResetRoundForEmptyServer();
            return;
        }

        if (RoundEnded)
            return;

        if (timer == null || timer.CurrentCycle <= 1)
            return;

        CloseJoining();
        TryEndRound();
    }

    public void CloseJoining()
    {
        if (isServer && spawnPointsSource != null && spawnPointsSource.enabled)
            spawnPointsSource.enabled = false;
    }

    private void TryEndRound()
    {
        if (!isServer || !isSpawned || resettingRound || sessions.Count == 0 ||
            RoundEnded || players.Count != 1)
            return;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        if (timer == null || timer.CurrentCycle <= 1)
            return;

        MP_PlayerStats stats = players[0];
        MP_Player winner = stats != null ? stats.GetComponent<MP_Player>() : null;
        if (winner == null || !winner.IsAlive)
            return;

        roundResult.value = new MP_RoundResult(stats.PlayerName, winner.objectId);
        foreach (PlayerSession session in sessions.Values)
            session.CanRespawn = false;

        timer.StopForWinner();
        CloseJoining();
    }

    private void HandleRoundResultChanged(MP_RoundResult result)
    {
        if (result.HasWinner)
            OnRoundEnded?.Invoke();
    }

    private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
    {
        if (!asServer || sessions.ContainsKey(player))
            return;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        if (sessions.Count == 0 && (resetPending || players.Count > 0 ||
            pendingClouds.Count > 0 || RoundEnded ||
            (timer != null && timer.CurrentCycle > 0)))
            ResetRoundForEmptyServer();

        sessions.Add(player, new PlayerSession());
    }

    private void HandlePlayerLeft(PlayerID player, bool asServer)
    {
        if (!asServer || !sessions.TryGetValue(player, out PlayerSession session))
            return;

        sessions.Remove(player);
        if (sessions.Count == 0)
        {
            resetPending = true;
            return;
        }

        if (session.Cloud != null)
            UnregisterPlayer(session.Cloud);
    }

    private void ResetRoundForEmptyServer()
    {
        if (!isServer || !isSpawned || resettingRound || sessions.Count != 0)
            return;

        resettingRound = true;
        try
        {
            // Despawn registered clouds before clearing their round state
            MP_PlayerStats[] currentPlayers = players.ToArray();
            foreach (MP_PlayerStats stats in currentPlayers)
            {
                if (stats != null && stats.TryGetComponent(out MP_Player cloud))
                    cloud.Despawn();
            }

            MP_Player[] unregisteredClouds = pendingClouds.ToArray();
            foreach (MP_Player cloud in unregisteredClouds)
            {
                if (cloud != null && !players.Contains(cloud.Stats))
                    UnityProxy.Destroy(cloud.gameObject);
            }

            players.Clear();
            pendingClouds.Clear();
            occupiedSpaces.Clear();
            playerCount.value = 0;
            aiPlayerCount.value = 0;
            nextRespawnPoint = 0;

            MP_FoodSpawner.Instance?.ResetRound();
            MP_EnemySpawner[] enemySpawners = FindObjectsByType<MP_EnemySpawner>(FindObjectsSortMode.None);
            foreach (MP_EnemySpawner enemySpawner in enemySpawners)
                enemySpawner.ResetRound();

            MP_CycleTimer.Instance?.ResetRound();
            roundResult.value = default;
            MP_Leaderboard[] leaderboards = FindObjectsByType<MP_Leaderboard>(FindObjectsSortMode.None);
            foreach (MP_Leaderboard leaderboard in leaderboards)
                leaderboard.ResetRound();
            resetPending = false;
        }
        finally
        {
            resettingRound = false;
        }
    }

    public void RegisterPlayer(MP_Player player)
    {
        if (!isServer || RoundEnded || resettingRound)
            return;

        if (player == null || player.Stats == null)
        {
            Debug.LogWarning("Trying to register a null player"); 
            return; 
        }

        pendingClouds.Remove(player);

        if (player.owner.HasValue)
        {
            PlayerID owner = player.owner.Value;
            HandlePlayerJoined(owner, false, true);
            PlayerSession session = sessions[owner];
            if (!string.IsNullOrWhiteSpace(session.PlayerName))
                player.Stats.SetPlayerNameOnServer(session.PlayerName);
            session.Cloud = player;
            session.PlayerName = player.Stats.PlayerName;
            session.CanRespawn = false;
        }

        if (!players.Contains(player.Stats))
        {
            players.Add(player.Stats);
            playerCount.value++;
            if (player.GetComponent<MP_EnemyAI>() != null)
                aiPlayerCount.value++;
        }
        else
        {
            Debug.LogWarning("Trying to register a player that is already registered"); 
        }
    }

    public void UnregisterPlayer(MP_Player player)
    {
        if (!isServer)
            return;

        if (player == null)
        {
            Debug.LogWarning("Trying to unregister a null player"); 
            return; 
        }

        if (player.owner.HasValue &&
            sessions.TryGetValue(player.owner.Value, out PlayerSession session) &&
            session.Cloud == player)
        {
            session.PlayerName = player.Stats.PlayerName;
            session.Cloud = null;
        }

        // Death removes it immediately; the later despawn callback is a no-op
        if (players.Remove(player.Stats))
        {
            playerCount.value--;
            if (player.GetComponent<MP_EnemyAI>() != null)
                aiPlayerCount.value--;

            TryEndRound();
        }
    }

    private void HandlePlayerCountChange(int newCount)
    {
        OnPlayerCountChanged?.Invoke(newCount); 
    }

    private void HandleAIPlayerCountChange(int newCount)
    {
        OnAIPlayerCountChanged?.Invoke(newCount);
    }

    private void ResetClientStartState()
    {
        serverReadyToStart = false;
        startPending = false;
        nextStartReadinessCheck = 0f;
        startFailure = "";
    }

    private bool IsClientPrepared()
    {
        return isClient && IsSpawned(false) &&
            MP_LocalPlayerManager.Instance != null &&
            MP_GameBoundary.Instance != null &&
            MP_CycleTimer.Instance != null && MP_CycleTimer.Instance.IsSpawned(false) &&
            MP_FoodSpawner.Instance != null && MP_FoodSpawner.Instance.IsSpawned(false);
    }

    private void UpdateClientStartReadiness()
    {
        MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
        if (local != null && (local.LocalPlayer != null || local.HasDied))
        {
            startPending = false;
            return;
        }

        if (startPending && Time.unscaledTime - startRequestedAt >= StartRequestTimeoutSeconds)
        {
            startPending = false;
            startFailure = "Start has not completed. Please try again.";
        }

        if (serverReadyToStart || !IsClientPrepared() || RoundEnded ||
            MP_CycleTimer.Instance.CurrentCycle > 1 ||
            Time.unscaledTime < nextStartReadinessCheck)
            return;

        nextStartReadinessCheck = Time.unscaledTime + StartReadinessRetrySeconds;
        ReportClientReady();
    }

    public bool TryRequestStart()
    {
        if (!CanStart)
            return false;

        startPending = true;
        startFailure = "";
        startRequestedAt = Time.unscaledTime;
        RequestStart(localPlayerData.Costume);
        return true;
    }

    [ServerRpc(requireOwnership: false)]
    private void ReportClientReady(RPCInfo info = default)
    {
        if (!TryGetConnectedSession(info.sender, out PlayerSession session))
            return;

        bool ready = CanStartOnServer(session, out string reason);
        session.ReadyToStart = ready;
        ReceiveStartReadiness(info.sender, ready, reason);
    }

    [TargetRpc]
    private void ReceiveStartReadiness(PlayerID target, bool ready, string reason)
    {
        serverReadyToStart = ready;
        startFailure = ready ? "" : reason;
    }

    [ServerRpc(requireOwnership: false)]
    private void RequestStart(MP_CostumeIndices selectedCostume, RPCInfo info = default)
    {
        if (!TryGetConnectedSession(info.sender, out PlayerSession session))
            return;

        bool serverReady = CanStartOnServer(session, out string reason);
        if (!session.ReadyToStart || !serverReady)
        {
            ReceiveStartFailure(info.sender,
                string.IsNullOrEmpty(reason) ? "Wait until the match is ready." : reason);
            return;
        }

        MP_PlayerCostume prefabCostume = playerPrefab.GetComponent<MP_PlayerCostume>();
        if (prefabCostume == null || !prefabCostume.IsValid(selectedCostume))
        {
            ReceiveStartFailure(info.sender, "The selected cloud costume is unavailable.");
            return;
        }

        if (!TryFindSafeSpawn(playerPrefab, out SpawnPoint spawn))
        {
            ReceiveStartFailure(info.sender, "No safe space is available inside the game boundary.");
            return;
        }

        session.ReadyToStart = false;
        session.Costume = selectedCostume;
        GameObject cloud = null;
        try
        {
            cloud = UnityProxy.Instantiate(playerPrefab.gameObject, spawn.position, spawn.rotation,
                gameObject.scene);
            TrackPendingCloud(cloud);
            MP_Player player = cloud.GetComponent<MP_Player>();
            session.Cloud = player;
            player.GiveOwnership(info.sender);
            if (!player.GetComponent<MP_PlayerCostume>().SetCostumeOnServer(session.Costume))
                throw new InvalidOperationException("Unable to apply the selected cloud costume");
        }
        catch (Exception exception)
        {
            if (cloud != null)
                UnityProxy.Destroy(cloud);
            session.Cloud = null;
            session.ReadyToStart = true;
            Debug.LogException(exception, this);
            ReceiveStartFailure(info.sender, "Unable to start. Please try again.");
        }
    }

    private bool TryGetConnectedSession(PlayerID sender, out PlayerSession session)
    {
        session = null;
        if (!isServer || !isSpawned ||
            !networkManager.TryGetModule<PurrNet.Modules.PlayersManager>(true, out var connectedPlayers) ||
            !connectedPlayers.IsPlayerConnected(sender))
            return false;

        HandlePlayerJoined(sender, false, true);
        return sessions.TryGetValue(sender, out session);
    }

    private bool CanStartOnServer(PlayerSession session, out string reason)
    {
        reason = "";
        if (resettingRound || RoundEnded)
            reason = "This round has ended.";
        else if (MP_CycleTimer.Instance == null || !MP_CycleTimer.Instance.IsSpawned(true) ||
            MP_FoodSpawner.Instance == null || !MP_FoodSpawner.Instance.IsReadyForPlayers ||
            MP_GameBoundary.Instance == null)
            reason = "Waiting for server...";
        else if (MP_CycleTimer.Instance.CurrentCycle > 1)
            reason = "Joining is closed. Spectating this round.";
        else if (playerPrefab == null || playerPrefab.Stats == null ||
            playerPrefab.GetComponent<MP_PlayerGrowth>() == null)
            reason = "Starting is not configured on the server.";
        else if (playerPrefab.GetComponent<MP_PlayerCostume>() is not { } costume ||
            !costume.IsValid(default))
            reason = "Cloud costumes are not configured on the server.";
        else if (session.Cloud != null)
            reason = "You are already in the match.";

        return string.IsNullOrEmpty(reason);
    }

    [TargetRpc]
    private void ReceiveStartFailure(PlayerID target, string message)
    {
        startPending = false;
        startFailure = message;
    }

    public void NotifyPlayerDeath(PlayerID player, string killerName)
    {
        if (!isServer || !sessions.TryGetValue(player, out PlayerSession session) || session.IsAlive)
            return;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        session.CanRespawn = !RoundEnded && timer != null && timer.CurrentCycle == 1;
        ReceivePlayerDeath(player, killerName);
    }

    // Sent from the persistent manager so the message survives the cloud's despawn
    [TargetRpc]
    private void ReceivePlayerDeath(PlayerID target, string killerName)
    {
        if (MP_LocalPlayerManager.Instance != null)
            MP_LocalPlayerManager.Instance.HandleLocalPlayerDeath(killerName);
    }

    public bool TryRequestRespawn()
    {
        if (!isSpawned || !isClient || RoundEnded ||
            MP_CycleTimer.Instance == null || MP_CycleTimer.Instance.CurrentCycle != 1 ||
            MP_LocalPlayerManager.Instance == null ||
            !MP_LocalPlayerManager.Instance.HasDied)
            return false;

        RequestRespawn();
        return true;
    }

    [ServerRpc(requireOwnership: false)]
    private void RequestRespawn(RPCInfo info = default)
    {
        // Identity comes from PurrNet's authenticated sender, never a client-supplied ID
        if (!isServer || !networkManager.TryGetModule<PurrNet.Modules.PlayersManager>(true, out var connectedPlayers) ||
            !connectedPlayers.IsPlayerConnected(info.sender) ||
            !sessions.TryGetValue(info.sender, out PlayerSession session))
            return;

        // Cloud may already exist while its spawn callbacks are still pending
        if (RoundEnded || !session.CanRespawn || session.Cloud != null ||
            MP_CycleTimer.Instance == null || MP_CycleTimer.Instance.CurrentCycle != 1)
            return;

        if (playerPrefab == null || playerPrefab.Stats == null ||
            playerPrefab.GetComponent<MP_PlayerGrowth>() == null)
        {
            Debug.LogError("Assign a valid player prefab on MP_PlayerManager to enable respawning.", this);
            ReceiveRespawnFailure(info.sender, "Respawning is not configured on the server.");
            return;
        }

        if (!TryFindSafeSpawn(playerPrefab, out SpawnPoint spawn))
        {
            ReceiveRespawnFailure(info.sender, "No safe space is available inside the game boundary.");
            return;
        }

        // Claim eligibility before instantiation to reject repeated requests
        session.CanRespawn = false;
        GameObject cloud = null;
        try
        {
            cloud = UnityProxy.Instantiate(playerPrefab.gameObject, spawn.position, spawn.rotation, gameObject.scene);
            TrackPendingCloud(cloud);
            MP_Player player = cloud.GetComponent<MP_Player>();
            session.Cloud = player;
            player.GiveOwnership(info.sender);
            if (!player.GetComponent<MP_PlayerCostume>().SetCostumeOnServer(session.Costume))
                throw new InvalidOperationException("Unable to restore the cloud costume");
        }
        catch (Exception exception)
        {
            if (cloud != null)
                UnityProxy.Destroy(cloud);
            session.Cloud = null;
            session.CanRespawn = true;
            Debug.LogException(exception, this);
            ReceiveRespawnFailure(info.sender, "Unable to respawn. Please try again.");
        }
    }

    [TargetRpc]
    private void ReceiveRespawnFailure(PlayerID target, string message)
    {
        if (MP_LocalPlayerManager.Instance != null)
            MP_LocalPlayerManager.Instance.HandleRespawnFailure(message);
    }

    public bool TrySpawnEnemy(MP_Player enemyPrefab)
    {
        if (!isServer || !isSpawned || RoundEnded || enemyPrefab == null ||
            enemyPrefab.GetComponent<MP_EnemyAI>() == null)
            return false;

        MP_PlayerCostume prefabCostume = enemyPrefab.GetComponent<MP_PlayerCostume>();
        if (prefabCostume == null || !prefabCostume.IsValid(default))
            return false;

        if (!TryFindSafeSpawn(enemyPrefab, out SpawnPoint spawn))
            return false;

        GameObject cloud = null;
        try
        {
            cloud = UnityProxy.Instantiate(enemyPrefab.gameObject, spawn.position,
                spawn.rotation, gameObject.scene);
            TrackPendingCloud(cloud);
            MP_PlayerCostume costume = cloud.GetComponent<MP_PlayerCostume>();
            if (costume == null || !costume.RandomizeOnServer())
                throw new InvalidOperationException("Unable to randomize the AI cloud costume");
            return true;
        }
        catch
        {
            if (cloud != null)
                UnityProxy.Destroy(cloud);
            throw;
        }
    }

    private void TrackPendingCloud(GameObject cloud)
    {
        MP_Player player = cloud.GetComponent<MP_Player>();
        if (player != null && !players.Contains(player.Stats) && !pendingClouds.Contains(player))
            pendingClouds.Add(player);
    }

    private bool TryFindSafeSpawn(MP_Player prefab, out SpawnPoint spawn)
    {
        spawn = default;
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary == null)
        {
            Debug.LogError("Add MP_GameBoundary to the scene before spawning clouds", this);
            return false;
        }

        occupiedSpaces.Clear();
        foreach (MP_PlayerStats stats in players)
        {
            if (stats == null)
                continue;
            MP_Player player = stats.GetComponent<MP_Player>();
            if (player != null && player.IsAlive)
                AddOccupiedSpace(player);
        }

        for (int i = pendingClouds.Count - 1; i >= 0; i--)
        {
            MP_Player player = pendingClouds[i];
            if (player == null)
                pendingClouds.RemoveAt(i);
            else
                AddOccupiedSpace(player);
        }

        float radius = GetColliderRadius(prefab);
        float clearance = Mathf.Max(0f, spawnClearance);
        if (!boundary.CanFit(0f))
            return false;

        if (spawnPointsSource != null)
        {
            var points = spawnPointsSource.SpawnPoints;
            for (int i = 0; i < points.Count; i++)
            {
                Transform point = points[nextRespawnPoint % points.Count];
                nextRespawnPoint = (nextRespawnPoint + 1) % points.Count;
                if (point != null && boundary.Contains(point.position) &&
                    MP_SpawnSafety.IsClear(point.position, radius, clearance, occupiedSpaces))
                {
                    spawn = new SpawnPoint { position = point.position, rotation = point.rotation };
                    return true;
                }
            }
        }

        for (int i = 0; i < Mathf.Max(1, spawnSearchAttempts); i++)
        {
            boundary.TryGetRandomPosition(0f, out Vector2 position);
            if (MP_SpawnSafety.IsClear(position, radius, clearance, occupiedSpaces))
            {
                spawn = new SpawnPoint { position = position, rotation = prefab.transform.rotation };
                return true;
            }
        }

        return false;
    }

    private void AddOccupiedSpace(MP_Player player)
    {
        occupiedSpaces.Add(new MP_SpawnSafety.Space(player.transform.position, GetColliderRadius(player)));
    }

    private static float GetColliderRadius(MP_Player player)
    {
        float radius = 0f;
        // Enclose every enabled circle, including child eating triggers and offsets
        // Transform math also works on prefab assets, whose physics bounds are empty
        foreach (CircleCollider2D collider in player.GetComponentsInChildren<CircleCollider2D>())
        {
            if (!collider.enabled)
                continue;
            Vector2 center = collider.transform.TransformPoint(collider.offset);
            Vector3 scale = collider.transform.lossyScale;
            float worldRadius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            radius = Mathf.Max(radius, Vector2.Distance(center, player.transform.position) + worldRadius);
        }
        return radius;
    }
}
