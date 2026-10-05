using UnityEngine;
using PurrNet;

public class MP_Player : PlayerIdentity<MP_Player>
{
    [SerializeField] private MP_PlayerStats playerStats;
    [SerializeField] private MP_PlayerData playerData;

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

            MP_LocalPlayerManager.Instance.SetLocalPlayer(this);

            playerStats.SetPlayerName(playerData.PlayerName);
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