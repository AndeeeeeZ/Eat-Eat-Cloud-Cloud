using System.Collections;
using UnityEngine;
using PurrNet;

public class MP_Player : PlayerIdentity<MP_Player>
{
    [SerializeField] private MP_PlayerStats playerStats;
    private const float EatAnimationSeconds = 0.35f;
    private const float EatDespawnDelaySeconds = 0.6f;
    private readonly SyncVar<Vector2> eatenTarget = new(Vector2.zero);
    private readonly SyncVar<bool> beingEaten = new(false);
    private Coroutine pendingDespawn;

    [Header("Spawn Protection")]
    [SerializeField, Min(0f)] private float spawnProtectionDuration = 3f;
    [SerializeField] private GameObject spawnProtectionRing;
    private readonly SyncVar<bool> spawnProtected = new(false);
    private double protectionEndsAt;
    public bool IsSpawnProtected => spawnProtected.value;

    private void Awake()
    {
        spawnProtected.onChanged += HandleProtectionChanged;
        beingEaten.onChanged += HandleBeingEatenChanged;
        HideProtectionRing();
    }

    private void Update()
    {
        if (isServer && IsAlive && spawnProtected.value && Time.timeAsDouble >= protectionEndsAt)
            spawnProtected.value = false;
    }

    private void HandleProtectionChanged(bool isProtected)
    {
        if (spawnProtectionRing != null && spawnProtectionRing != gameObject)
        {
            MP_SpawnProtectionRing ring = spawnProtectionRing.GetComponent<MP_SpawnProtectionRing>();
            if (ring != null)
                ring.SetProtected(isClient && isProtected);
        }
    }

    private void HideProtectionRing()
    {
        if (spawnProtectionRing != null && spawnProtectionRing != gameObject)
        {
            MP_SpawnProtectionRing ring = spawnProtectionRing.GetComponent<MP_SpawnProtectionRing>();
            if (ring != null)
                ring.HideImmediately();
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        spawnProtected.onChanged -= HandleProtectionChanged;
        beingEaten.onChanged -= HandleBeingEatenChanged;
    }

    // Server-only lifecycle state. Set before despawning to guard queued collisions
    public bool IsAlive { get; private set; }
    public MP_PlayerStats Stats => playerStats;

    public bool TryDieFromEating(MP_Player eater)
    {
        if (!isServer || !IsAlive || eater == null || eater == this ||
            !eater.isServer || !eater.IsAlive || IsSpawnProtected || eater.IsSpawnProtected ||
            eater.Stats.Level <= playerStats.Level)
            return false;

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        MP_PlayerGrowth eaterGrowth = eater.GetComponent<MP_PlayerGrowth>();
        if (manager == null || manager.RoundEnded || eaterGrowth == null)
            return false;

        float experienceToAward = playerStats.TotalExp;
        IsAlive = false;
        eatenTarget.value = eater.transform.position;
        beingEaten.value = true;

        eaterGrowth.GainExperienceFromEating(experienceToAward);
        // Remove the living cloud immediately; keep its connection record for respawn
        manager.UnregisterPlayer(this);
        Debug.Log($"{eater.Stats.PlayerName} ate {playerStats.PlayerName}", this);

        pendingDespawn = StartCoroutine(DespawnAfterEating(eater.Stats.PlayerName));
        return true;
    }

    private IEnumerator DespawnAfterEating(string killerName)
    {
        yield return new WaitForSecondsRealtime(EatAnimationSeconds);
        if (isServer && isSpawned && owner.HasValue && MP_PlayerManager.Instance != null)
            MP_PlayerManager.Instance.NotifyPlayerDeath(owner.Value, killerName);
        yield return new WaitForSecondsRealtime(EatDespawnDelaySeconds - EatAnimationSeconds);
        pendingDespawn = null;
        if (isServer && isSpawned)
            Despawn();
    }

    private void HandleBeingEatenChanged(bool eaten)
    {
        if (eaten && isClient)
            GetComponent<MP_PlayerCostume>()?.BeginBeingEaten(eatenTarget.value,
                EatAnimationSeconds);
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer)
        {
            beingEaten.value = false;
            eatenTarget.value = Vector2.zero;
            protectionEndsAt = Time.timeAsDouble + Mathf.Max(0f, spawnProtectionDuration);
            spawnProtected.value = spawnProtectionDuration > 0f;

            if (MP_PlayerManager.Instance == null)
            {
                Debug.LogError("MP_PlayerManager doesn't exist when player spawned", this);
                return;
            }

            IsAlive = true;
            MP_PlayerManager.Instance.RegisterPlayer(this);
            return;
        }

        HandleProtectionChanged(spawnProtected.value);
        HandleBeingEatenChanged(beingEaten.value);

        if (isOwner)
        {
            if (MP_LocalPlayerManager.Instance == null)
            {
                Debug.LogError("MP_LocalPlayerManager doesn't exist when player spawned", this);
                return;
            }

            MP_LocalPlayerManager.Instance.SetLocalPlayer(this);

        }
    }

    protected override void OnDespawned(bool asServer)
    {
        if (pendingDespawn != null)
        {
            StopCoroutine(pendingDespawn);
            pendingDespawn = null;
        }
        base.OnDespawned(asServer);
        HideProtectionRing();

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
