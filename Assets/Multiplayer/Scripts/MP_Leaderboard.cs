using System;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Packing;
using UnityEngine;

public struct MP_LeaderboardEntry : IPackedAuto, IEquatable<MP_LeaderboardEntry>
{
    public PlayerID PlayerId;
    public ulong ObjectId;
    public bool IsAI;
    public string PlayerName;
    public float TotalExp;

    public MP_LeaderboardEntry(PlayerID playerId, ulong objectId, bool isAI,
        string playerName, float totalExp)
    {
        PlayerId = playerId;
        ObjectId = objectId;
        IsAI = isAI;
        PlayerName = playerName;
        TotalExp = totalExp;
    }

    public bool Equals(MP_LeaderboardEntry other)
    {
        return PlayerId == other.PlayerId && ObjectId == other.ObjectId &&
            IsAI == other.IsAI && PlayerName == other.PlayerName &&
            TotalExp.Equals(other.TotalExp);
    }

    public static int CompareRank(MP_LeaderboardEntry first, MP_LeaderboardEntry second)
    {
        int scoreOrder = second.TotalExp.CompareTo(first.TotalExp);
        // Stable tie order so equal scores do not swap places between updates
        return scoreOrder != 0 ? scoreOrder : first.ObjectId.CompareTo(second.ObjectId);
    }
}

// Server publishes one ordered snapshot so every client displays the same ranks
public class MP_Leaderboard : NetworkBehaviour
{
    [SerializeField] private MP_PlayerManager playerManager;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private readonly SyncVar<MP_LeaderboardEntry[]> standings = new(Array.Empty<MP_LeaderboardEntry>());
    private readonly List<MP_LeaderboardEntry> workingEntries = new();
    private double nextRefreshTime;
    private bool available;

    public event Action OnLeaderboardChanged;
    public IReadOnlyList<MP_LeaderboardEntry> Entries => available
        ? standings.value
        : Array.Empty<MP_LeaderboardEntry>();

    private void Awake()
    {
        standings.onChanged += HandleStandingsChanged;
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        available = true;

        if (asServer)
        {
            if (playerManager == null)
                playerManager = MP_PlayerManager.Instance;
            if (playerManager == null)
                Debug.LogError("MP_Leaderboard requires an MP_PlayerManager.", this);

            PublishStandings();
            nextRefreshTime = Time.timeAsDouble + Mathf.Max(0.05f, refreshInterval);
        }

        OnLeaderboardChanged?.Invoke();
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        available = false;
        OnLeaderboardChanged?.Invoke();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        standings.onChanged -= HandleStandingsChanged;
    }

    private void Update()
    {
        if (!isServer || Time.timeAsDouble < nextRefreshTime)
            return;

        nextRefreshTime = Time.timeAsDouble + Mathf.Max(0.05f, refreshInterval);
        PublishStandings();
    }

    public void ResetRound()
    {
        if (!isServer || !isSpawned)
            return;

        PublishStandings();
        nextRefreshTime = Time.timeAsDouble + Mathf.Max(0.05f, refreshInterval);
    }

    private void PublishStandings()
    {
        if (!isServer || playerManager == null)
            return;

        workingEntries.Clear();
        foreach (MP_PlayerStats stats in playerManager.Players)
        {
            if (stats == null)
                continue;

            MP_Player player = stats.GetComponent<MP_Player>();
            if (player == null || !player.IsAlive)
                continue;

            bool isAI = player.GetComponent<MP_EnemyAI>() != null;
            if (!isAI && !player.owner.HasValue)
                continue;

            PlayerID owner = player.owner.GetValueOrDefault();
            workingEntries.Add(new MP_LeaderboardEntry(owner, player.objectId,
                isAI, stats.PlayerName, stats.TotalExp));
        }

        workingEntries.Sort(MP_LeaderboardEntry.CompareRank);

        // Avoid sending a snapshot when neither scores, names, nor membership changed
        MP_LeaderboardEntry[] previous = standings.value;
        bool changed = previous.Length != workingEntries.Count;
        for (int i = 0; !changed && i < previous.Length; i++)
            changed = !previous[i].Equals(workingEntries[i]);

        if (changed)
            standings.value = workingEntries.ToArray();
    }

    private void HandleStandingsChanged(MP_LeaderboardEntry[] entries)
    {
        OnLeaderboardChanged?.Invoke();
    }
}
