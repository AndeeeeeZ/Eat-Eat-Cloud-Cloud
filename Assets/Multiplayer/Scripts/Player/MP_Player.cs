using UnityEngine;
using PurrNet;

public class MP_Player : PlayerIdentity<MP_Player>
{
    [SerializeField] private MP_PlayerStats playerStats;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer)
        {
            if (MP_PlayerManager.Instance == null)
            {
                Debug.LogError("MP_PlayerManager doesn't exist when player spawned", this);
                return;
            }

            // Register this player's stats with the server-only manager
            MP_PlayerManager.Instance.RegisterPlayer(playerStats);
            return;
        }

        if (isOwner)
        {
            if (MP_LocalPlayerManager.Instance == null)
            {
                Debug.LogError("MP_LocalPlayerManager doesn't exist when player spawned", this);
                return;
            }

            // Notify local systems about the local player
            MP_LocalPlayerManager.Instance.SetLocalPlayer(this);
        }
    }

    protected override void OnDespawned(bool asServer)
    {
        base.OnDespawned(asServer);

        if (!asServer)
            return;

        if (MP_PlayerManager.Instance != null)
            MP_PlayerManager.Instance.UnregisterPlayer(playerStats);
    }
}