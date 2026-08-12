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

    private void Awake()
    {
        normalSize = cam.Lens.OrthographicSize;
        normalScale = targetScale = 1f;
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

    public void SetTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("Target is null", this); 
            return; 
        }

        if (currentPlayer != null)
        {
            RemoveTarget(); 
            Debug.LogWarning("Cleared camera's previous target to set to new one", this); 
        }

        cam.Follow = target;

        currentPlayer = target.GetComponent<MP_PlayerGrowth>();

        if (currentPlayer == null)
            return;

        currentPlayer.OnScaleChanged += SetScaleTo;

        SetScaleTo(currentPlayer.Scale);
    }

    public void RemoveTarget()
    {
        if (currentPlayer == null)
        {
            Debug.LogError("Trying to remove target while target is null", this); 
            return; 
        }
        currentPlayer.OnScaleChanged -= SetScaleTo; 
        currentPlayer = null; 
        cam.Follow = null; 
    }

    private void SetScaleTo(float scale)
    {
        targetScale *= scale / normalScale;
        normalScale = scale;
    }
}