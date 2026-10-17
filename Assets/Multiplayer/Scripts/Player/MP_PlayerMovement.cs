using PurrNet;
using UnityEngine;

// Controls the player's movement
// Controlled by the server
[RequireComponent(typeof(MP_PlayerInput))]
public class MP_PlayerMovement : NetworkBehaviour
{
    [SerializeField] private FloatValue baseMoveSpeed;
    private MP_PlayerInput playerInput;
    private CircleCollider2D bodyCollider;

    private void Awake()
    {
        playerInput = GetComponent<MP_PlayerInput>(); 
        bodyCollider = GetComponent<CircleCollider2D>();
    }

    private void FixedUpdate()
    {
        if (!isServer)
            return;

        Vector2 direction = playerInput.MovementInput;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        float speedMultiplier = timer != null ? timer.SpeedMultiplier : 1f;
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
    }
}
