using UnityEngine;
using PurrNet;

public class MP_Player : PlayerIdentity<MP_Player>
{
    [SerializeField] private MP_PlayerStats playerStats;
    [SerializeField] private MP_PlayerData playerData;

    // Server-only lifecycle state. Set before despawning to guard queued collisions.
    public bool IsAlive { get; private set; }
    public MP_PlayerStats Stats => playerStats;

    public bool TryDieFromEating(MP_Player eater)
    {
        if (!isServer || !IsAlive || eater == null || eater == this ||
            !eater.isServer || !eater.IsAlive || eater.Stats.Level <= playerStats.Level)
            return false;

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        MP_PlayerGrowth eaterGrowth = eater.GetComponent<MP_PlayerGrowth>();
        if (manager == null || eaterGrowth == null)
            return false;

        float experienceToAward = playerStats.TotalExp;
        IsAlive = false;

        // Remove the living cloud immediately; keep its connection record for respawn.
        manager.UnregisterPlayer(this);

        eaterGrowth.GainExperienceFromEating(experienceToAward);
        Debug.Log($"{eater.Stats.PlayerName} ate {playerStats.PlayerName}", this);

        if (owner.HasValue)
            manager.NotifyPlayerDeath(owner.Value, eater.Stats.PlayerName);

        Despawn();
        return true;
    }

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

            IsAlive = true;
            MP_PlayerManager.Instance.RegisterPlayer(this);
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
        {
            if (MP_LocalPlayerManager.Instance != null)
                MP_LocalPlayerManager.Instance.ClearLocalPlayer(this);
            return;
        }

        IsAlive = false;
        if (MP_PlayerManager.Instance != null)
            MP_PlayerManager.Instance.UnregisterPlayer(this);
    }
}
