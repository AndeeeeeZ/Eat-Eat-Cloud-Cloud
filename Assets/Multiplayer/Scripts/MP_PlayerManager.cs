using PurrNet;
using UnityEngine;
using System.Collections.Generic;
using System;

// Server-side connection records survive the death of a player's cloud.

public class MP_PlayerManager : NetworkBehaviour
{
    public static MP_PlayerManager Instance { get; private set; }
    private readonly List<MP_PlayerStats> players = new();
    public IReadOnlyList<MP_PlayerStats> Players => players;

    public sealed class PlayerSession
    {
        public MP_Player Cloud { get; internal set; }
        public string PlayerName { get; internal set; } = "";
        public bool IsAlive => Cloud != null && Cloud.IsAlive;
    }

    private readonly Dictionary<PlayerID, PlayerSession> sessions = new();
    // Populated on the server only. PlayerCount continues to count living clouds.
    public IReadOnlyDictionary<PlayerID, PlayerSession> Sessions => sessions;
    private NetworkManager sessionNetworkManager;

    public event Action<int> OnPlayerCountChanged;
    private readonly SyncVar<int> playerCount = new(0);
    public int PlayerCount => playerCount.value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        playerCount.onChanged += HandlePlayerCountChange; 
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        playerCount.onChanged -= HandlePlayerCountChange; 

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
        playerCount.value = 0;
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

        if (player.owner.HasValue)
        {
            PlayerID owner = player.owner.Value;
            HandlePlayerJoined(owner, false, true);
            sessions[owner].Cloud = player;
            sessions[owner].PlayerName = player.Stats.PlayerName;
        }

        if (!players.Contains(player.Stats))
        {
            players.Add(player.Stats);
            playerCount.value++;
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

        // Death removes it immediately; the later despawn callback is a no-op.
        if (players.Remove(player.Stats))
        {
            playerCount.value--;
        }
    }

    private void HandlePlayerCountChange(int newCount)
    {
        OnPlayerCountChanged?.Invoke(newCount); 
    }

    public void NotifyPlayerDeath(PlayerID player, string killerName)
    {
        if (!isServer || !sessions.TryGetValue(player, out PlayerSession session) || session.IsAlive)
            return;

        ReceivePlayerDeath(player, killerName);
    }

    // Sent from the persistent manager so the message survives the cloud's despawn.
    [TargetRpc]
    private void ReceivePlayerDeath(PlayerID target, string killerName)
    {
        if (MP_LocalPlayerManager.Instance != null)
            MP_LocalPlayerManager.Instance.HandleLocalPlayerDeath(killerName);
    }
}
