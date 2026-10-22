using PurrNet;
using UnityEngine;
using System.Collections.Generic;
using System;

// Server-side connection records survive the death of a player's cloud

[DefaultExecutionOrder(-5)]
public class MP_PlayerManager : NetworkBehaviour, IProvideSpawnPoints, IProvidePrefabInstantiated
{
    [Header("Respawning")]
    [SerializeField] private MP_Player playerPrefab;
    [SerializeField] private PlayerSpawner spawnPointsSource;
    private int nextRespawnPoint;

    [Header("Safe Spawning")]
    [SerializeField, Min(0f)] private float spawnClearance = 0.5f;
    [SerializeField, Min(1)] private int spawnSearchAttempts = 64;
    private readonly List<MP_Player> pendingClouds = new();
    private readonly List<MP_SpawnSafety.Space> occupiedSpaces = new();

    public static MP_PlayerManager Instance { get; private set; }
    private readonly List<MP_PlayerStats> players = new();
    public IReadOnlyList<MP_PlayerStats> Players => players;

    public sealed class PlayerSession
    {
        public MP_Player Cloud { get; internal set; }
        public string PlayerName { get; internal set; } = "";
        public bool IsAlive => Cloud != null && Cloud.IsAlive;
        public bool CanRespawn { get; internal set; }
    }

    private readonly Dictionary<PlayerID, PlayerSession> sessions = new();
    // Populated on the server only. Counts include living AI clouds
    public IReadOnlyDictionary<PlayerID, PlayerSession> Sessions => sessions;
    private NetworkManager sessionNetworkManager;

    public event Action<int> OnPlayerCountChanged;
    public event Action<int> OnAIPlayerCountChanged;
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

        if (spawnPointsSource != null)
        {
            spawnPointsSource.SetRespawnPointProvider(this);
            spawnPointsSource.SetPrefabInstantiatedProvider(this);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        playerCount.onChanged -= HandlePlayerCountChange; 
        aiPlayerCount.onChanged -= HandleAIPlayerCountChange;

        if (spawnPointsSource != null)
        {
            spawnPointsSource.ResetSpawnPointProvider();
            spawnPointsSource.ResetPrefabInstantiatedProvider();
        }

        if (Instance == this)
            Instance = null; 
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (!asServer)
            return;

        sessionNetworkManager = networkManager;
        sessionNetworkManager.onPlayerJoined += HandlePlayerJoined;
        sessionNetworkManager.onPlayerLeft += HandlePlayerLeft;

        foreach (PlayerID player in sessionNetworkManager.players)
            HandlePlayerJoined(player, false, true);
    }

    protected override void OnDespawned(bool asServer)
    {
        base.OnDespawned(asServer);
        if (!asServer)
            return;

        if (sessionNetworkManager != null)
        {
            sessionNetworkManager.onPlayerJoined -= HandlePlayerJoined;
            sessionNetworkManager.onPlayerLeft -= HandlePlayerLeft;
            sessionNetworkManager = null;
        }

        sessions.Clear();
        players.Clear();
        pendingClouds.Clear();
        nextRespawnPoint = 0;
        playerCount.value = 0;
        aiPlayerCount.value = 0;
    }

    private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
    {
        if (asServer && !sessions.ContainsKey(player))
            sessions.Add(player, new PlayerSession());
    }

    private void HandlePlayerLeft(PlayerID player, bool asServer)
    {
        if (!asServer || !sessions.TryGetValue(player, out PlayerSession session))
            return;

        if (session.Cloud != null)
            UnregisterPlayer(session.Cloud);

        sessions.Remove(player);
    }

    public void RegisterPlayer(MP_Player player)
    {
        if (!isServer)
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

    public void NotifyPlayerDeath(PlayerID player, string killerName)
    {
        if (!isServer || !sessions.TryGetValue(player, out PlayerSession session) || session.IsAlive)
            return;

        session.CanRespawn = true;
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
        if (!isSpawned || !isClient || MP_LocalPlayerManager.Instance == null ||
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
        if (!session.CanRespawn || session.Cloud != null)
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
        if (!isServer || !isSpawned || enemyPrefab == null ||
            enemyPrefab.GetComponent<MP_EnemyAI>() == null)
            return false;

        if (!TryFindSafeSpawn(enemyPrefab, out SpawnPoint spawn))
            return false;

        GameObject cloud = null;
        try
        {
            cloud = UnityProxy.Instantiate(enemyPrefab.gameObject, spawn.position,
                spawn.rotation, gameObject.scene);
            TrackPendingCloud(cloud);
            return true;
        }
        catch
        {
            if (cloud != null)
                UnityProxy.Destroy(cloud);
            throw;
        }
    }

    // PurrNet's default spawner uses the same selection for initial joins
    public SpawnPoint NextSpawnPoint(PlayerID player, SceneID scene)
    {
        if (TryFindSafeSpawn(playerPrefab, out SpawnPoint spawn))
            return spawn;

        Debug.LogWarning("No clear player spawn was found inside the game boundary", this);
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        Vector3 position = playerPrefab.transform.position;
        if (boundary != null)
            position = boundary.ClampPosition(position);

        return new SpawnPoint { position = position, rotation = playerPrefab.transform.rotation };
    }

    public void OnPrefabInstantiated(GameObject prefabInstance, PlayerID player, SceneID scene)
    {
        TrackPendingCloud(prefabInstance);
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
