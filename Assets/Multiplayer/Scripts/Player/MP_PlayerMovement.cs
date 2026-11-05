using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

// Server controls gameplay movement; clients adjust visuals only
[DefaultExecutionOrder(-1)]
[RequireComponent(typeof(MP_PlayerInput))]
public class MP_PlayerMovement : NetworkBehaviour
{
    [SerializeField] private FloatValue baseMoveSpeed;
    [Header("Speed Boost")]
    [SerializeField, Min(1f)] private float boostSpeedMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float boostDurationSeconds = 2f;
    [SerializeField, Min(0f)] private float boostCooldownSeconds = 5f;
    [SerializeField] private ParticleSystem boostParticles;
    private readonly SyncVar<bool> boostActive = new(false);
    private MP_PlayerInput playerInput;
    private MP_EnemyAI enemyAI;
    private MP_Player player;
    private MP_PlayerGrowth playerGrowth;
    private MP_PlayerCostume playerCostume;
    private MP_PlayerEating playerEating;
    private CircleCollider2D bodyCollider;
    private MP_Ping ping;
    private Transform[] visualChildren;
    private Vector3[] visualLocalPositions;
    private Transform presentationTarget;
    private Vector2 presentationPosition;
    private Vector2 previousServerPosition;
    private Vector2 remoteVelocity;
    private bool presentationInitialized;
    private float predictionLeadSeconds = 0.12f;
    private double nextPingLookupAt;
    private double lastInputAt;
    private double lastServerMovedAt;
    private double boostEndsAt;
    private double nextBoostAt;
    private double boostVisualEndsAt;
    private double cooldownVisualEndsAt;

    public bool IsSpeedBoostActive => boostActive.value;
    public float SpeedBoostDuration => Mathf.Max(0.01f, boostDurationSeconds);
    public float SpeedBoostCooldown => Mathf.Max(0f, boostCooldownSeconds);
    public float SpeedBoostRemaining => boostActive.value
        ? (float)System.Math.Max(0d, boostVisualEndsAt - Time.realtimeSinceStartupAsDouble)
        : 0f;
    public float SpeedBoostCooldownRemaining => !boostActive.value
        ? (float)System.Math.Max(0d, cooldownVisualEndsAt - Time.realtimeSinceStartupAsDouble)
        : 0f;
    public event Action OnSpeedBoostCooldownAttempt;

    public Transform PresentationTarget
    {
        get
        {
            if (isServer)
                return transform;

            if (presentationTarget == null)
            {
                GameObject target = new GameObject("Predicted Camera Target");
                presentationTarget = target.transform;
                presentationTarget.SetParent(transform, false);
            }

            return presentationTarget;
        }
    }

    public Vector2 PresentationPosition => presentationInitialized
        ? presentationPosition : (Vector2)transform.position;
    public float CurrentFullMoveSpeed
    {
        get
        {
            MP_CycleTimer timer = MP_CycleTimer.Instance;
            float speed = baseMoveSpeed != null ? baseMoveSpeed.Value : 0f;
            speed *= (timer != null ? timer.SpeedMultiplier : 1f) *
                GetSizeSpeedMultiplier(timer);
            if (boostActive.value)
                speed *= Mathf.Max(1f, boostSpeedMultiplier);
            if (enemyAI != null)
                speed *= enemyAI.MovementSpeedMultiplier;
            return speed;
        }
    }

    private void Awake()
    {
        playerInput = GetComponent<MP_PlayerInput>(); 
        enemyAI = GetComponent<MP_EnemyAI>();
        player = GetComponent<MP_Player>();
        playerGrowth = GetComponent<MP_PlayerGrowth>();
        playerCostume = GetComponent<MP_PlayerCostume>();
        playerEating = GetComponentInChildren<MP_PlayerEating>();
        bodyCollider = GetComponent<CircleCollider2D>();
        CacheVisualChildren();
        SetBoostParticles(false);
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        boostActive.onChanged -= HandleBoostChanged;
        boostActive.onChanged += HandleBoostChanged;
        if (asServer)
        {
            boostEndsAt = 0d;
            nextBoostAt = 0d;
            boostActive.value = false;
        }
        else
        {
            boostVisualEndsAt = boostActive.value
                ? Time.realtimeSinceStartupAsDouble + SpeedBoostDuration : 0d;
            cooldownVisualEndsAt = 0d;
            SetBoostParticles(boostActive.value);
        }
    }

    public void RequestSpeedBoost()
    {
        if (!isSpawned || !isOwner || enemyAI != null ||
            (MP_PlayerManager.Instance != null && MP_PlayerManager.Instance.RoundEnded))
            return;

        if (SpeedBoostCooldownRemaining > 0f)
        {
            OnSpeedBoostCooldownAttempt?.Invoke();
            return;
        }

        ActivateSpeedBoostOnServer();
    }

    [ServerRpc]
    private void ActivateSpeedBoostOnServer()
    {
        if (enemyAI == null)
            TryActivateSpeedBoostOnServer();
    }

    public bool TryActivateSpeedBoostForAI()
    {
        return enemyAI != null && TryActivateSpeedBoostOnServer();
    }

    private bool TryActivateSpeedBoostOnServer()
    {
        if (!isServer || !isSpawned || player == null || !player.IsAlive ||
            (MP_PlayerManager.Instance != null && MP_PlayerManager.Instance.RoundEnded))
            return false;

        double now = Time.realtimeSinceStartupAsDouble;
        if (boostActive.value || now < nextBoostAt)
            return false;

        boostEndsAt = now + SpeedBoostDuration;
        nextBoostAt = boostEndsAt + SpeedBoostCooldown;
        boostActive.value = true;
        return true;
    }

    private void HandleBoostChanged(bool isActive)
    {
        if (!isSpawned || !isClient)
            return;

        SetBoostParticles(isActive);
        if (!isOwner)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        boostVisualEndsAt = isActive ? now + SpeedBoostDuration : 0d;
        cooldownVisualEndsAt = isActive ? 0d : now + SpeedBoostCooldown;
    }

    private void SetBoostParticles(bool isActive)
    {
        if (boostParticles == null)
            return;

        if (isActive)
        {
            if (!boostParticles.isPlaying)
                boostParticles.Play();
        }
        else
        {
            boostParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void FixedUpdate()
    {
        if (!isSpawned)
            return;

        bool roundEnded = MP_PlayerManager.Instance != null &&
            MP_PlayerManager.Instance.RoundEnded;
        if (isServer && boostActive.value &&
            (roundEnded || Time.realtimeSinceStartupAsDouble >= boostEndsAt))
            boostActive.value = false;

        if (!isServer || roundEnded || player == null || !player.IsAlive)
            return;

        Vector2 previousPosition = transform.position;
        Vector2 direction = enemyAI != null
            ? enemyAI.GetMovementDirection()
            : playerInput.MovementInput;

        Vector3 nextPosition = transform.position +
            (Vector3)(direction * CurrentFullMoveSpeed * Time.fixedDeltaTime);
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary != null)
            nextPosition = boundary.ClampPosition(nextPosition);

        transform.position = nextPosition;

        MP_FoodSpawner foodSpawner = MP_FoodSpawner.Instance;
        if (foodSpawner != null && player != null && player.IsAlive)
            foodSpawner.ConsumeFoodAlongPath(player, playerGrowth, bodyCollider,
                previousPosition, nextPosition, enemyAI);
        if (playerEating != null && player != null && player.IsAlive)
            playerEating.TryEatAlongPath(previousPosition, nextPosition);
    }

    private void LateUpdate()
    {
        if (!isSpawned || !isClient)
            return;

        Vector2 serverPosition = transform.position;
        if (isServer)
        {
            if (playerCostume != null)
                playerCostume.UpdateFacePosition(serverPosition);
            return;
        }

        if (!presentationInitialized)
        {
            presentationPosition = serverPosition;
            previousServerPosition = serverPosition;
            lastServerMovedAt = Time.timeAsDouble;
            presentationInitialized = true;
        }
        if ((serverPosition - previousServerPosition).sqrMagnitude > 0.000001f)
            lastServerMovedAt = Time.timeAsDouble;

        if (isOwner && enemyAI == null)
            UpdateOwnerPresentation(serverPosition);
        else
            UpdateRemotePresentation(serverPosition);

        Vector3 worldOffset = (Vector3)(presentationPosition - serverPosition);
        Vector3 localOffset = transform.InverseTransformVector(worldOffset);
        for (int i = 0; i < visualChildren.Length; i++)
            visualChildren[i].localPosition = visualLocalPositions[i] + localOffset;

        // Apply facial movement after smoothing has positioned every visual child
        if (playerCostume != null)
            playerCostume.UpdateFacePosition(presentationPosition);

        if (presentationTarget != null)
            presentationTarget.position = transform.position + worldOffset;

        previousServerPosition = serverPosition;
    }

    private void UpdateOwnerPresentation(Vector2 serverPosition)
    {
        float speed = CurrentFullMoveSpeed;
        bool roundEnded = MP_PlayerManager.Instance != null &&
            MP_PlayerManager.Instance.RoundEnded;
        Vector2 direction = !roundEnded && playerInput != null
            ? playerInput.MovementInput : Vector2.zero;
        if (direction.sqrMagnitude > 0f)
        {
            presentationPosition += direction * (speed * Time.deltaTime);
            lastInputAt = Time.timeAsDouble;
        }

        predictionLeadSeconds = Mathf.MoveTowards(predictionLeadSeconds,
            GetPredictionLeadSeconds(), Time.deltaTime * 0.25f);
        if (direction.sqrMagnitude > 0f)
        {
            Vector2 target = serverPosition + direction * (speed * predictionLeadSeconds);
            presentationPosition = Vector2.Lerp(presentationPosition, target,
                1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        double idleSeconds = Time.timeAsDouble - lastInputAt;
        bool serverSettled = lastServerMovedAt > lastInputAt &&
            Time.timeAsDouble - lastServerMovedAt > 0.05d;
        if (direction.sqrMagnitude == 0f &&
            idleSeconds > predictionLeadSeconds * 2f &&
            (serverSettled || idleSeconds > 1.5d))
            presentationPosition = Vector2.MoveTowards(presentationPosition,
                serverPosition, Mathf.Max(1f, speed) * Time.deltaTime);

        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary != null)
            presentationPosition = boundary.ClampPosition(presentationPosition);
    }

    private void UpdateRemotePresentation(Vector2 serverPosition)
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        float baseSpeed = baseMoveSpeed != null ? baseMoveSpeed.Value : 0f;
        float cycleSpeed = MP_CycleTimer.Instance != null
            ? MP_CycleTimer.Instance.SpeedMultiplier : 1f;
        float maximumSpeed = Mathf.Max(1f,
            baseSpeed * cycleSpeed * Mathf.Max(1f, boostSpeedMultiplier) * 1.25f);
        Vector2 displacement = serverPosition - previousServerPosition;
        if (displacement.magnitude > Mathf.Max(2f, maximumSpeed * deltaTime * 4f))
        {
            presentationPosition = serverPosition;
            remoteVelocity = Vector2.zero;
            return;
        }

        Vector2 observedVelocity = Vector2.ClampMagnitude(displacement / deltaTime,
            maximumSpeed);
        remoteVelocity = Vector2.Lerp(remoteVelocity, observedVelocity,
            1f - Mathf.Exp(-12f * deltaTime));
        Vector2 target = serverPosition + remoteVelocity * 0.05f;
        presentationPosition = Vector2.Lerp(presentationPosition, target,
            1f - Mathf.Exp(-20f * deltaTime));
    }

    private float GetPredictionLeadSeconds()
    {
        if (ping == null && Time.timeAsDouble >= nextPingLookupAt)
        {
            ping = FindFirstObjectByType<MP_Ping>();
            nextPingLookupAt = Time.timeAsDouble + 1d;
        }

        float roundTrip = ping != null && ping.PingMilliseconds.HasValue
            ? ping.PingMilliseconds.Value * 0.001f : 0.16f;
        // Include the transform's buffered interpolation behind the server state
        return Mathf.Clamp(roundTrip * 0.5f + 2f * Time.fixedDeltaTime, 0.08f, 0.35f);
    }

    private float GetSizeSpeedMultiplier(MP_CycleTimer timer)
    {
        int baseLevel = timer != null ? timer.MovementBaseLevel : 1;
        int playerLevel = playerGrowth != null ? playerGrowth.Level : 1;
        int levelsAboveBase = Mathf.Max(0, playerLevel - baseLevel);
        float reductionPerLevel = timer != null ? timer.MovementSpeedReductionPerLevel : 0.05f;
        float maxReduction = timer != null ? timer.MaxMovementSpeedReduction : 0.2f;
        return 1f - Mathf.Min(maxReduction, levelsAboveBase * reductionPerLevel);
    }

    private void CacheVisualChildren()
    {
        List<Transform> children = new();
        List<Vector3> positions = new();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if ((playerCostume != null && child == playerCostume.FaceTransform) ||
                child.GetComponent<Collider2D>() != null ||
                (child.GetComponent<Renderer>() == null && child.GetComponent<Canvas>() == null))
                continue;

            children.Add(child);
            positions.Add(child.localPosition);
        }

        visualChildren = children.ToArray();
        visualLocalPositions = positions.ToArray();
    }

    protected override void OnDespawned(bool asServer)
    {
        if (asServer)
        {
            boostActive.value = false;
            boostEndsAt = 0d;
            nextBoostAt = 0d;
        }
        else
        {
            SetBoostParticles(false);
            boostVisualEndsAt = 0d;
            cooldownVisualEndsAt = 0d;
            presentationInitialized = false;
            presentationPosition = Vector2.zero;
            previousServerPosition = Vector2.zero;
            remoteVelocity = Vector2.zero;
            lastServerMovedAt = 0d;
            predictionLeadSeconds = 0.12f;
            for (int i = 0; i < visualChildren.Length; i++)
                visualChildren[i].localPosition = visualLocalPositions[i];
            if (presentationTarget != null)
                Destroy(presentationTarget.gameObject);
            presentationTarget = null;
        }

        base.OnDespawned(asServer);
    }

    protected override void OnDestroy()
    {
        boostActive.onChanged -= HandleBoostChanged;
        base.OnDestroy();
    }
}
