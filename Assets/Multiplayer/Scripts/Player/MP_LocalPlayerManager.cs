using System;
using UnityEngine;

// Note this class is executed early through script execution order setting
// Due to other classes referencing to this class's instance in OnEnable
public class MP_LocalPlayerManager : MonoBehaviour
{
    public static MP_LocalPlayerManager Instance { get; private set; }

    public event Action<MP_Player> OnLocalPlayerReady;
    public event Action OnLocalPlayerLost;
    public event Action<string> OnLocalPlayerDied;

    public bool HasDied { get; private set; }
    public string KillerName { get; private set; } = "";

    public MP_Player LocalPlayer => localPlayer;
    private MP_Player localPlayer;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    public void SetLocalPlayer(MP_Player player)
    {
        if (player == null || ReferenceEquals(localPlayer, player))
            return;

        if (!ReferenceEquals(localPlayer, null))
            ClearLocalPlayer(localPlayer);

        localPlayer = player;
        HasDied = false;
        KillerName = "";
        OnLocalPlayerReady?.Invoke(player);
    }

    public void ClearLocalPlayer(MP_Player player)
    {
        // An old cloud's despawn must not clear a newer replacement cloud.
        if (ReferenceEquals(localPlayer, null) || !ReferenceEquals(localPlayer, player))
            return;

        localPlayer = null;
        OnLocalPlayerLost?.Invoke();
    }

    // Called by the server's targeted death notification, not by generic despawns.
    public void HandleLocalPlayerDeath(string killerName)
    {
        if (HasDied)
            return;

        ClearLocalPlayer(localPlayer);
        HasDied = true;
        KillerName = killerName;
        OnLocalPlayerDied?.Invoke(killerName);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

/*
Template code for subscribing to OnLocalPlayerReady

    private void OnEnable()
    {
        MP_LocalPlayerManager manager = MP_LocalPlayerManager.Instance;
        manager.OnLocalPlayerReady += HandleLocalPlayerReady;

        // In case player spawned before this object subscribe to the event
        if (manager.LocalPlayer != null)
            HandleLocalPlayerReady(manager.LocalPlayer);
    }

    private void OnDisable()
    {
        MP_LocalPlayerManager.Instance.OnLocalPlayerReady -= HandleLocalPlayerReady;
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {

    }
*/
