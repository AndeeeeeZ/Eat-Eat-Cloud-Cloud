using PurrNet;
using UnityEngine;
using UnityEngine.InputSystem;

public class MP_PlayerInput : NetworkBehaviour
{
    [SerializeField] private SyncInput<Vector2> movement = new();
    public Vector2 MovementInput => movement.value; 

    private Inputs input;
    private Camera mainCamera; 
    private MP_PlayerMovement playerMovement;

    private void Awake()
    {
        input = new Inputs();
        mainCamera = Camera.main; 
        playerMovement = GetComponent<MP_PlayerMovement>();
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer)
            return;

        if (!isOwner)
            return;

        if (isActiveAndEnabled)
            input.Multiplayer.Enable();
    }

    private void OnEnable()
    {
        if (IsSpawned(false) && isOwner)
            input.Multiplayer.Enable();
    }

    private void OnDisable()
    {
        if (IsSpawned(false) && isOwner)
            movement.value = Vector2.zero;

        input?.Disable();
    }

    protected override void OnDespawned(bool asServer)
    {
        // Ownership may already be cleared, so cleanup must not depend on isOwner
        input?.Disable();
        base.OnDespawned(asServer);
    }

    protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
    {
        base.OnOwnerChanged(oldOwner, newOwner, asServer);
        if (asServer)
            return;

        if (isOwner && isActiveAndEnabled)
            input.Multiplayer.Enable();
        else
            input.Disable();
    }

    protected override void OnDestroy()
    {
        if (input != null)
        {
            input.Disable();
            input.Dispose();
            input = null;
        }
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned(false) || !isOwner || input == null || !input.Multiplayer.enabled)
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

        Vector2 origin = playerMovement != null
            ? playerMovement.PresentationPosition : (Vector2)transform.position;
        Vector2 moveDirection = ((Vector2)mouseWorldPosition - origin).normalized;

        return moveDirection;
    }
}
