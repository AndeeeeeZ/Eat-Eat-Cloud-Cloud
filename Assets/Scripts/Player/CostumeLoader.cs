using UnityEngine;
using UnityEngine.InputSystem;

public class CostumeLoader : MonoBehaviour
{
    [SerializeField] private PlayerCostume costume;
    [SerializeField] private SpriteRenderer cloudBase, bottom, rightSide, leftSide, face;
    [SerializeField] private SpriteRenderer ring;
    [SerializeField, Min(0f)] private float faceOffsetDistance = 0.12f;
    [SerializeField, Min(0f)] private float faceFollowSpeed = 12f;

    private PlayerMovement playerMovement;
    private Rigidbody2D playerBody;
    private Camera previewCamera;
    private Vector3 faceRestPosition;
    private Vector2 faceOffset;

    private void Awake()
    {
        playerMovement = GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
            playerBody = playerMovement.GetComponent<Rigidbody2D>();
        previewCamera = Camera.main;
        if (face != null)
            faceRestPosition = face.transform.localPosition;
    }

    private void Start()
    {
        UpdateCostume();
    }

    private void Update()
    {
        if (costume == null || ring == null || ring.sprite == null)
            return;

        ring.transform.Rotate(0f, 0f, costume.RingSpinSpeed * Time.deltaTime);
        ring.color = CostumeRingAnimation.WithBreathingAlpha(
            ring.color, Time.time, costume);
    }

    private void LateUpdate()
    {
        if (face == null)
            return;

        Vector2 direction = Vector2.zero;
        float offsetAmount = 0f;
        if (playerMovement != null)
        {
            if (playerMovement.isActiveAndEnabled && playerBody != null)
            {
                direction = playerBody.linearVelocity;
                offsetAmount = Mathf.Clamp01(playerMovement.GetDirection().magnitude);
            }
        }
        else if (previewCamera != null && cloudBase != null && Mouse.current != null)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            float depth = Mathf.Abs(previewCamera.transform.position.z - cloudBase.transform.position.z);
            Vector3 mouseWorld = previewCamera.ScreenToWorldPoint(
                new Vector3(mousePosition.x, mousePosition.y, depth));
            direction = (Vector2)(mouseWorld - cloudBase.bounds.center);
            float cloudRadius = Mathf.Max(0.01f,
                Mathf.Min(cloudBase.bounds.extents.x, cloudBase.bounds.extents.y));
            offsetAmount = Mathf.Clamp01(direction.magnitude / cloudRadius);
        }

        Vector3 localDirection = face.transform.parent != null
            ? face.transform.parent.InverseTransformDirection(direction)
            : (Vector3)direction;
        Vector2 targetOffset = ((Vector2)localDirection).normalized *
            (faceOffsetDistance * offsetAmount);
        float deltaTime = playerMovement != null ? Time.deltaTime : Time.unscaledDeltaTime;
        faceOffset = Vector2.Lerp(faceOffset, targetOffset,
            1f - Mathf.Exp(-faceFollowSpeed * deltaTime));
        face.transform.localPosition = faceRestPosition + (Vector3)faceOffset;
    }

    private void OnDisable()
    {
        faceOffset = Vector2.zero;
        if (face != null)
            face.transform.localPosition = faceRestPosition;
    }

    public void UpdateCostume()
    {
        cloudBase.sprite = costume.GetCloudBase();
        cloudBase.color = costume.GetBaseColor();
        if (ring != null)
        {
            ring.sprite = costume.ringSprite;
            ring.color = CostumeRingAnimation.WithBreathingAlpha(
                costume.GetRingColor(), Time.time, costume);
        }
        bottom.sprite = costume.GetBottom();
        rightSide.sprite = costume.GetRight();
        leftSide.sprite = costume.GetLeft();
        face.sprite = costume.GetFace();
    }
}
