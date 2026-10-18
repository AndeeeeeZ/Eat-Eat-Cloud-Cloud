using PurrNet;
using UnityEngine;

// Controls the player's movement
// Controlled by the server
[RequireComponent(typeof(MP_PlayerInput))]
public class MP_PlayerMovement : NetworkBehaviour
{
    [SerializeField] private FloatValue baseMoveSpeed;
    private MP_PlayerInput playerInput;
    private MP_EnemyAI enemyAI;
    private MP_Player player;
    private MP_PlayerGrowth playerGrowth;
    private CircleCollider2D bodyCollider;

    private void Awake()
    {
        playerInput = GetComponent<MP_PlayerInput>(); 
        enemyAI = GetComponent<MP_EnemyAI>();
        player = GetComponent<MP_Player>();
        playerGrowth = GetComponent<MP_PlayerGrowth>();
        bodyCollider = GetComponent<CircleCollider2D>();
    }

    private void FixedUpdate()
    {
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
        {
            float radius = 0f;
            if (bodyCollider != null)
            {
                Vector3 scale = bodyCollider.transform.lossyScale;
                radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                radius += Vector2.Distance(bodyCollider.transform.TransformPoint(bodyCollider.offset), transform.position);
            }

            nextPosition = boundary.ClampPosition(nextPosition, radius);
        }

        transform.position = nextPosition;

        MP_FoodSpawner foodSpawner = MP_FoodSpawner.Instance;
        if (foodSpawner != null && player != null && player.IsAlive)
            foodSpawner.ConsumeFoodAlongPath(player, playerGrowth, bodyCollider,
                previousPosition, nextPosition);
    }
}
