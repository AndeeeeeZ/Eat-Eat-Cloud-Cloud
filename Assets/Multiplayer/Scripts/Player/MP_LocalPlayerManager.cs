using System;
using UnityEngine;

public class MP_LocalPlayerManager : MonoBehaviour
{
    public static MP_LocalPlayerManager Instance;

    public event Action<MP_Player> OnLocalPlayerReady;

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
        localPlayer = player;
        OnLocalPlayerReady?.Invoke(player);
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