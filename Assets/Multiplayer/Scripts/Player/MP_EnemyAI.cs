using PurrNet;
using UnityEngine;
using UnityEngine.Serialization;

// Chooses movement for an ownerless cloud on the server
[RequireComponent(typeof(MP_Player), typeof(MP_PlayerStats), typeof(MP_PlayerMovement))]
[RequireComponent(typeof(MP_PlayerGrowth))]
public class MP_EnemyAI : NetworkBehaviour
{
    [SerializeField] private string enemyName = "Enemy";
    [SerializeField, Min(0f)] private float startingExperience = 10f;
    [SerializeField, Min(0.5f)] private float assumedCameraAspectRatio = 16f / 9f;
    [SerializeField, Min(0.05f)] private float decisionInterval = 0.25f;
    [SerializeField, Min(0f)] private float movementSpeedMultiplier = 0.9f;
    [SerializeField, Min(1f)] private float turnSpeedDegreesPerSecond = 360f;
    [SerializeField, Min(0.1f)] private float chaseDurationSeconds = 3f;
    [SerializeField, Min(0f)] private float targetSwitchPauseSeconds = 1f;
    [FormerlySerializedAs("cloudsEatenBeforePause")]
    [SerializeField, Min(1)] private int foodEatenBeforePause = 2;
    [SerializeField, Min(0f)] private float postEatPauseSeconds = 3f;

    private MP_Player player;
    private MP_PlayerStats stats;
    private MP_PlayerGrowth growth;
    private CircleCollider2D bodyCollider;
    private MP_Player target;
    private MP_Player recentlyChasedTarget;
    private Vector2 foodDestination;
    private Vector2 currentDirection;
    private double nextDecisionAt;
    private double chaseEndsAt;
    private double recentlyChasedTargetAvailableAt;
    private double nextHuntAt;
    private int foodEatenSincePause;
    private bool fleeing;
    private bool hasFoodDestination;
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
        foodDestination = default;
        currentDirection = Vector2.zero;
        chaseEndsAt = 0d;
        recentlyChasedTargetAvailableAt = 0d;
        nextHuntAt = 0d;
        foodEatenSincePause = 0;
        fleeing = false;
        hasFoodDestination = false;
        startingExperienceApplied = false;
        stats.SetPlayerNameOnServer(enemyName);
    }

    protected override void OnDespawned(bool asServer)
    {
        if (asServer)
        {
            target = null;
            recentlyChasedTarget = null;
            hasFoodDestination = false;
            currentDirection = Vector2.zero;
            nextHuntAt = 0d;
            foodEatenSincePause = 0;
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
        MP_FoodSpawner foodSpawner = MP_FoodSpawner.Instance;
        Vector2 viewExtents = foodSpawner != null
            ? foodSpawner.GetCameraViewExtents(transform.localScale.x, assumedCameraAspectRatio)
            : Vector2.zero;
        MP_CycleTimer timer = MP_CycleTimer.Instance;
        bool chaseExpired = target != null && !fleeing && now >= chaseEndsAt;
        if (chaseExpired)
        {
            recentlyChasedTarget = target;
            recentlyChasedTargetAvailableAt = now + Mathf.Max(0.1f, chaseDurationSeconds);
            nextHuntAt = System.Math.Max(nextHuntAt,
                now + Mathf.Max(0f, targetSwitchPauseSeconds));
            target = null;
        }

        bool canChase = timer != null && timer.CurrentCycle > 1 && now >= nextHuntAt;
        if (chaseExpired || now >= nextDecisionAt || !HasValidTarget(canChase, viewExtents))
        {
            ChooseTarget(now, canChase, viewExtents, foodSpawner);
            nextDecisionAt = now + Mathf.Max(0.05f, decisionInterval);
        }

        if (target != null)
        {
            Vector2 offset = (Vector2)target.transform.position - (Vector2)transform.position;
            return SmoothDirection((fleeing ? -offset : offset).normalized);
        }

        if (hasFoodDestination)
        {
            Vector2 toFood = foodDestination - (Vector2)transform.position;
            if (toFood.sqrMagnitude > 0.0001f)
                return SmoothDirection(toFood.normalized);
        }

        return SmoothDirection(GetFallbackDirection());
    }

    public void RecordFoodEaten()
    {
        if (!isServer || !isSpawned)
            return;

        foodEatenSincePause++;
        if (foodEatenSincePause < Mathf.Max(1, foodEatenBeforePause))
            return;

        foodEatenSincePause = 0;
        nextHuntAt = System.Math.Max(nextHuntAt,
            Time.timeAsDouble + Mathf.Max(0f, postEatPauseSeconds));
        target = null;
        fleeing = false;
        nextDecisionAt = 0d;
    }

    private Vector2 GetFallbackDirection()
    {
        if (currentDirection.sqrMagnitude < 0.0001f)
        {
            currentDirection = Random.insideUnitCircle.normalized;
            if (currentDirection.sqrMagnitude < 0.0001f)
                currentDirection = Vector2.right;
        }

        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary == null)
            return currentDirection;

        float radius = 0f;
        if (bodyCollider != null)
        {
            Vector3 scale = bodyCollider.transform.lossyScale;
            radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            radius += Vector2.Distance(bodyCollider.transform.TransformPoint(bodyCollider.offset),
                transform.position);
        }

        Vector2 projected = (Vector2)transform.position + currentDirection *
            Mathf.Max(1f, radius);
        Vector3 clamped = boundary.ClampPosition(projected, radius);
        Vector2 direction = currentDirection;
        if (Mathf.Abs(projected.x - clamped.x) > 0.001f)
            direction.x = -direction.x;
        if (Mathf.Abs(projected.y - clamped.y) > 0.001f)
            direction.y = -direction.y;
        return direction.normalized;
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

    private bool HasValidTarget(bool canChase, Vector2 viewExtents)
    {
        if (target == null)
            return true;

        if (!fleeing && !canChase)
            return false;

        if (!target.IsAlive || target.IsSpawnProtected || target.Stats == null)
            return false;

        Vector2 offset = (Vector2)target.transform.position - (Vector2)transform.position;
        if (Mathf.Abs(offset.x) > viewExtents.x || Mathf.Abs(offset.y) > viewExtents.y)
            return false;

        return fleeing ? target.Stats.Level > stats.Level : target.Stats.Level < stats.Level;
    }

    private void ChooseTarget(double now, bool canChase, Vector2 viewExtents,
        MP_FoodSpawner foodSpawner)
    {
        MP_Player previousTarget = target;
        bool wasFleeing = fleeing;
        bool keepCurrentChase = previousTarget != null && !wasFleeing &&
            HasValidTarget(canChase, viewExtents);
        target = null;
        fleeing = false;
        hasFoodDestination = false;

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager == null)
            return;

        float nearestThreatSquared = float.MaxValue;
        float nearestPreySquared = float.MaxValue;
        MP_Player nearestPrey = null;

        // Human and AI clouds share the same target list
        foreach (MP_PlayerStats otherStats in manager.Players)
        {
            if (otherStats == null || otherStats == stats)
                continue;

            MP_Player other = otherStats.GetComponent<MP_Player>();
            if (other == null || !other.IsAlive || other.IsSpawnProtected)
                continue;

            Vector2 offset = (Vector2)other.transform.position - (Vector2)transform.position;
            if (Mathf.Abs(offset.x) > viewExtents.x ||
                Mathf.Abs(offset.y) > viewExtents.y)
                continue;

            float distanceSquared = offset.sqrMagnitude;
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

        if (target == null && foodSpawner != null)
            hasFoodDestination = foodSpawner.TryGetNearestFoodInView((Vector2)transform.position,
                viewExtents, out foodDestination);
    }
}
