using PurrNet;
using UnityEngine;

// Chooses movement for an ownerless cloud on the server
[RequireComponent(typeof(MP_Player), typeof(MP_PlayerStats), typeof(MP_PlayerMovement))]
[RequireComponent(typeof(MP_PlayerGrowth))]
public class MP_EnemyAI : NetworkBehaviour
{
    [SerializeField] private string enemyName = "Enemy";
    [SerializeField, Min(0f)] private float startingExperience = 10f;
    [SerializeField, Min(0.1f)] private float detectionRadius = 8f;
    [SerializeField, Min(0.05f)] private float decisionInterval = 0.25f;
    [SerializeField, Min(0f)] private float movementSpeedMultiplier = 0.9f;
    [SerializeField, Min(1f)] private float turnSpeedDegreesPerSecond = 360f;
    [SerializeField, Min(0.1f)] private float chaseDurationSeconds = 3f;
    [SerializeField, Min(0.1f)] private float wanderInterval = 2f;
    [SerializeField, Min(0.1f)] private float wanderDistance = 5f;

    private MP_Player player;
    private MP_PlayerStats stats;
    private MP_PlayerGrowth growth;
    private CircleCollider2D bodyCollider;
    private MP_Player target;
    private MP_Player recentlyChasedTarget;
    private Vector2 wanderDestination;
    private Vector2 currentDirection;
    private double nextDecisionAt;
    private double nextWanderAt;
    private double chaseEndsAt;
    private double recentlyChasedTargetAvailableAt;
    private bool fleeing;
    private bool startingExperienceApplied;

    public float MovementSpeedMultiplier => Mathf.Max(0f, movementSpeedMultiplier);

    private void Awake()
    {
        player = GetComponent<MP_Player>();
        stats = GetComponent<MP_PlayerStats>();
        growth = GetComponent<MP_PlayerGrowth>();
        bodyCollider = GetComponent<CircleCollider2D>();
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (!asServer)
            return;

        target = null;
        recentlyChasedTarget = null;
        nextDecisionAt = 0d;
        nextWanderAt = 0d;
        currentDirection = Vector2.zero;
        chaseEndsAt = 0d;
        recentlyChasedTargetAvailableAt = 0d;
        fleeing = false;
        startingExperienceApplied = false;
        stats.SetPlayerNameOnServer(enemyName);
    }

    protected override void OnDespawned(bool asServer)
    {
        if (asServer)
        {
            target = null;
            recentlyChasedTarget = null;
            currentDirection = Vector2.zero;
        }

        base.OnDespawned(asServer);
    }

    public Vector2 GetMovementDirection()
    {
        if (!isServer || !isSpawned || player == null || !player.IsAlive)
            return Vector2.zero;

        if (!startingExperienceApplied)
        {
            startingExperienceApplied = true;
            if (growth != null && startingExperience > 0f)
                growth.GainExperience(startingExperience);
        }

        double now = Time.timeAsDouble;
        MP_CycleTimer timer = MP_CycleTimer.Instance;
        bool canChase = timer != null && timer.CurrentCycle > 1;
        bool chaseExpired = target != null && !fleeing && now >= chaseEndsAt;
        if (chaseExpired)
        {
            recentlyChasedTarget = target;
            recentlyChasedTargetAvailableAt = now + Mathf.Max(0.1f, chaseDurationSeconds);
            target = null;
        }

        if (chaseExpired || now >= nextDecisionAt || !HasValidTarget(canChase))
        {
            ChooseTarget(now, canChase);
            nextDecisionAt = now + Mathf.Max(0.05f, decisionInterval);
        }

        if (target != null)
        {
            Vector2 offset = (Vector2)target.transform.position - (Vector2)transform.position;
            return SmoothDirection((fleeing ? -offset : offset).normalized);
        }

        Vector2 toDestination = wanderDestination - (Vector2)transform.position;
        if (now >= nextWanderAt || toDestination.sqrMagnitude < 0.25f)
        {
            PickWanderDestination();
            nextWanderAt = now + Mathf.Max(0.1f, wanderInterval);
            toDestination = wanderDestination - (Vector2)transform.position;
        }

        return SmoothDirection(toDestination.normalized);
    }

    private Vector2 SmoothDirection(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            currentDirection = Vector2.zero;
            return currentDirection;
        }

        if (currentDirection.sqrMagnitude < 0.0001f)
        {
            currentDirection = desiredDirection;
            return currentDirection;
        }

        float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        float desiredAngle = Mathf.Atan2(desiredDirection.y, desiredDirection.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, desiredAngle,
            Mathf.Max(1f, turnSpeedDegreesPerSecond) * Time.fixedDeltaTime) * Mathf.Deg2Rad;
        currentDirection = new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle));
        return currentDirection;
    }

    private bool HasValidTarget(bool canChase)
    {
        if (target == null)
            return true;

        if (!fleeing && !canChase)
            return false;

        if (!target.IsAlive || target.IsSpawnProtected || target.Stats == null)
            return false;

        float detectionDistanceSquared = detectionRadius * detectionRadius;
        if (((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude >
            detectionDistanceSquared)
            return false;

        return fleeing ? target.Stats.Level > stats.Level : target.Stats.Level < stats.Level;
    }

    private void ChooseTarget(double now, bool canChase)
    {
        MP_Player previousTarget = target;
        bool wasFleeing = fleeing;
        bool keepCurrentChase = previousTarget != null && !wasFleeing && HasValidTarget(canChase);
        target = null;
        fleeing = false;

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager == null)
            return;

        float maximumDistanceSquared = detectionRadius * detectionRadius;
        float nearestThreatSquared = maximumDistanceSquared;
        float nearestPreySquared = maximumDistanceSquared;
        MP_Player nearestPrey = null;

        foreach (MP_PlayerStats otherStats in manager.Players)
        {
            if (otherStats == null || otherStats == stats)
                continue;

            MP_Player other = otherStats.GetComponent<MP_Player>();
            if (other == null || !other.IsAlive || other.IsSpawnProtected)
                continue;

            float distanceSquared = ((Vector2)other.transform.position -
                (Vector2)transform.position).sqrMagnitude;
            if (otherStats.Level > stats.Level && distanceSquared < nearestThreatSquared)
            {
                target = other;
                nearestThreatSquared = distanceSquared;
                fleeing = true;
            }
            else if (canChase && otherStats.Level < stats.Level &&
                (other != recentlyChasedTarget || now >= recentlyChasedTargetAvailableAt) &&
                distanceSquared < nearestPreySquared)
            {
                nearestPrey = other;
                nearestPreySquared = distanceSquared;
            }
        }

        if (target == null)
        {
            target = keepCurrentChase ? previousTarget : nearestPrey;
            if (target != null && (target != previousTarget || wasFleeing))
                chaseEndsAt = now + Mathf.Max(0.1f, chaseDurationSeconds);
        }
    }

    private void PickWanderDestination()
    {
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary != null)
        {
            float radius = 0f;
            if (bodyCollider != null)
            {
                Vector3 scale = bodyCollider.transform.lossyScale;
                radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }

            if (boundary.CanFit(radius))
            {
                Vector2 candidate = (Vector2)transform.position +
                    Random.insideUnitCircle * Mathf.Max(0.1f, wanderDistance);
                wanderDestination = boundary.ClampPosition(candidate, radius);
                if ((wanderDestination - (Vector2)transform.position).sqrMagnitude < 0.25f)
                    wanderDestination = boundary.Center;
                return;
            }
        }

        wanderDestination = (Vector2)transform.position +
            Random.insideUnitCircle * Mathf.Max(0.1f, wanderDistance);
    }
}
