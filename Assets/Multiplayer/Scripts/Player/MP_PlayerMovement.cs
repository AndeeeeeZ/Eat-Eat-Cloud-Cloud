using System.Collections.Generic;
using PurrNet;
using UnityEngine;

// Server controls gameplay movement; the owner predicts visuals only
[DefaultExecutionOrder(-1)]
[RequireComponent(typeof(MP_PlayerInput))]
public class MP_PlayerMovement : NetworkBehaviour
{
    [SerializeField] private FloatValue baseMoveSpeed;
    private MP_PlayerInput playerInput;
    private MP_EnemyAI enemyAI;
    private MP_Player player;
    private MP_PlayerGrowth playerGrowth;
    private CircleCollider2D bodyCollider;
    private MP_Ping ping;
    private Transform[] visualChildren;
    private Vector3[] visualLocalPositions;
    private Transform presentationTarget;
    private Vector2 presentationPosition;
    private Vector2 previousServerPosition;
    private bool presentationInitialized;
    private float predictionLeadSeconds = 0.12f;
    private double nextPingLookupAt;
    private double lastInputAt;
    private double lastServerMovedAt;

    public Transform PresentationTarget
    {
        get
        {
            if (isServer || !isOwner || enemyAI != null)
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

    private void Awake()
    {
        playerInput = GetComponent<MP_PlayerInput>(); 
        enemyAI = GetComponent<MP_EnemyAI>();
        player = GetComponent<MP_Player>();
        playerGrowth = GetComponent<MP_PlayerGrowth>();
        bodyCollider = GetComponent<CircleCollider2D>();
        CacheVisualChildren();
    }

    private void FixedUpdate()
    {
        if (!isSpawned || (MP_PlayerManager.Instance != null &&
            MP_PlayerManager.Instance.RoundEnded))
            return;

        if (!isServer)
            return;

        Vector2 previousPosition = transform.position;
        Vector2 direction = enemyAI != null
            ? enemyAI.GetMovementDirection()
            : playerInput.MovementInput;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        float speedMultiplier = timer != null ? timer.SpeedMultiplier : 1f;
        if (enemyAI != null)
            speedMultiplier *= enemyAI.MovementSpeedMultiplier;
        Vector3 nextPosition = transform.position +
            (Vector3)(direction * baseMoveSpeed.Value * speedMultiplier * Time.fixedDeltaTime);
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary != null)
            nextPosition = boundary.ClampPosition(nextPosition);

        transform.position = nextPosition;

        MP_FoodSpawner foodSpawner = MP_FoodSpawner.Instance;
        if (foodSpawner != null && player != null && player.IsAlive)
            foodSpawner.ConsumeFoodAlongPath(player, playerGrowth, bodyCollider,
                previousPosition, nextPosition, enemyAI);
    }

    private void LateUpdate()
    {
        if (!isSpawned || !isClient || !isOwner || isServer || enemyAI != null)
            return;

        Vector2 serverPosition = transform.position;
        if (!presentationInitialized)
        {
            presentationPosition = serverPosition;
            previousServerPosition = serverPosition;
            lastServerMovedAt = Time.timeAsDouble;
            presentationInitialized = true;
        }
        if ((serverPosition - previousServerPosition).sqrMagnitude > 0.000001f)
            lastServerMovedAt = Time.timeAsDouble;

        float speedMultiplier = MP_CycleTimer.Instance != null
            ? MP_CycleTimer.Instance.SpeedMultiplier : 1f;
        float speed = baseMoveSpeed != null ? baseMoveSpeed.Value * speedMultiplier : 0f;
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

        Vector3 worldOffset = (Vector3)(presentationPosition - serverPosition);
        Vector3 localOffset = transform.InverseTransformVector(worldOffset);
        for (int i = 0; i < visualChildren.Length; i++)
            visualChildren[i].localPosition = visualLocalPositions[i] + localOffset;

        if (presentationTarget != null)
            presentationTarget.position = transform.position + worldOffset;

        previousServerPosition = serverPosition;
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

    private void CacheVisualChildren()
    {
        List<Transform> children = new();
        List<Vector3> positions = new();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<Collider2D>() != null ||
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
        if (!asServer)
        {
            presentationInitialized = false;
            presentationPosition = Vector2.zero;
            previousServerPosition = Vector2.zero;
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
}
