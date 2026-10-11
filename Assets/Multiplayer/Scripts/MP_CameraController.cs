using Unity.Cinemachine;
using UnityEngine;

// Note this script is not networked
public class MP_CameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private float zoomSpeed = 10f;

    private MP_PlayerGrowth currentPlayer;
    private float normalSize;
    private float normalScale;
    private float targetScale;
    private MP_LocalPlayerManager localPlayerManager;

    private void Awake()
    {
        normalSize = cam.Lens.OrthographicSize;
        normalScale = targetScale = 1f;
    }

    private void OnEnable()
    {
        localPlayerManager = MP_LocalPlayerManager.Instance;
        if (localPlayerManager == null)
            return;

        localPlayerManager.OnLocalPlayerReady += HandleLocalPlayerReady;
        localPlayerManager.OnLocalPlayerLost += RemoveTarget;

        // In case player spawned before this object subscribe to the event
        if (localPlayerManager.LocalPlayer != null)
            HandleLocalPlayerReady(localPlayerManager.LocalPlayer);
    }

    private void OnDisable()
    {
        if (localPlayerManager != null)
        {
            localPlayerManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
            localPlayerManager.OnLocalPlayerLost -= RemoveTarget;
        }
        RemoveTarget();
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        SetTarget(player.transform);
    }

    public void SetTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("Target is null", this);
            return;
        }

        RemoveTarget();

        cam.Follow = target;

        currentPlayer = target.GetComponent<MP_PlayerGrowth>();

        if (currentPlayer == null)
            return;

        currentPlayer.OnScaleChanged += SetScaleTo;

        SetScaleTo(currentPlayer.Scale);
    }

    private void LateUpdate()
    {
        float currentSize = cam.Lens.OrthographicSize;

        float newSize = Mathf.Lerp(
            currentSize,
            normalSize * targetScale,
            zoomSpeed * Time.deltaTime
        );

        var lens = cam.Lens;
        lens.OrthographicSize = newSize;
        cam.Lens = lens;
    }

    public void RemoveTarget()
    {
        if (!ReferenceEquals(currentPlayer, null))
            currentPlayer.OnScaleChanged -= SetScaleTo;
        currentPlayer = null;
        if (cam != null)
            cam.Follow = null;
    }

    private void SetScaleTo(float scale)
    {
        targetScale *= scale / normalScale;
        normalScale = scale;
    }
}
