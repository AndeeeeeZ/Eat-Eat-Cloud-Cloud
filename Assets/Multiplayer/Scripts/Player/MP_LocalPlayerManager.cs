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