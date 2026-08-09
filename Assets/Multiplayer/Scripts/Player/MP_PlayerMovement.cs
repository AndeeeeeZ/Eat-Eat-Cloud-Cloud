using PurrNet;
using UnityEngine;

// Controls the player's movement
// Controlled by the server
[RequireComponent(typeof(MP_PlayerInput))]
public class MP_PlayerMovement : NetworkBehaviour
{
    [SerializeField] private FloatValue baseMoveSpeed;
    private MP_PlayerInput playerInput;

    private void Awake()
    {
        playerInput = GetComponent<MP_PlayerInput>(); 
    }

    private void FixedUpdate()
    {
        if (!isServer)
            return;

        Vector2 direction = playerInput.MovementInput;

        transform.position += (Vector3)(direction * baseMoveSpeed.Value * Time.fixedDeltaTime);
    }
}
