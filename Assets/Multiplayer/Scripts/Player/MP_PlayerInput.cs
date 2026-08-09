using PurrNet;
using UnityEngine;
using UnityEngine.InputSystem;

public class MP_PlayerInput : NetworkBehaviour
{
    [SerializeField] private SyncInput<Vector2> movement = new();
    public Vector2 MovementInput => movement.value; 

    private Inputs input;
    private Camera mainCamera; 

    private void Awake()
    {
        input = new Inputs();
        mainCamera = Camera.main; 
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer)
            return;

        if (!isOwner)
            return;

        input.Enable();
    }

    protected override void OnDestroy()
    {
        if (isOwner)
        {
            input.Disable();
        }

        base.OnDestroy();
    }

    private void Update()
    {
        if (!isOwner)
            return;

        MovementUpdate();
    }

    // Only move when the IsMoving button is pressed
    // Currently the button is the left mouse button
    private void MovementUpdate()
    {
        if (!input.Multiplayer.IsMoving.IsPressed())
        {
            movement.value = Vector2.zero;
            return;
        }

        movement.value = GetMoveDirection(); 
    }

    private Vector2 GetMoveDirection()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(
            new Vector3(
                mouseScreenPosition.x,
                mouseScreenPosition.y,
                -Camera.main.transform.position.z
            )
        );

        Vector2 moveDirection = ((Vector2)mouseWorldPosition - (Vector2)transform.position).normalized;

        return moveDirection;
    }
}
