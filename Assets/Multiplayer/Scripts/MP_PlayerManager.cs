using PurrNet;
using UnityEngine;
using System.Collections.Generic;
using System;

// A server side manager that keeps references to all current players

public class MP_PlayerManager : NetworkBehaviour
{
    public static MP_PlayerManager Instance { get; private set; }
    private readonly List<MP_PlayerStats> players = new();
    public IReadOnlyList<MP_PlayerStats> Players => players;

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

    public void RegisterPlayer(MP_PlayerStats stats)
    {
        if (!isServer)
            return;

        if (stats == null)
        {
            Debug.LogWarning("Trying to register a null player"); 
            return; 
        }

        if (!players.Contains(stats))
        {
            players.Add(stats);
            playerCount.value++;
        }
        else
        {
            Debug.LogWarning("Trying to register a player that is already registered"); 
        }
    }

    public void UnregisterPlayer(MP_PlayerStats stats)
    {
        if (!isServer)
            return;

        if (stats == null)
        {
            Debug.LogWarning("Trying to unregister a null player"); 
            return; 
        }

        if (players.Remove(stats))
        {
            playerCount.value--; 
        }
        else
        {
            Debug.LogWarning("Trying to unregister a non-existing player");
        }
    }

    private void HandlePlayerCountChange(int newCount)
    {
        OnPlayerCountChanged?.Invoke(newCount); 
    }
}