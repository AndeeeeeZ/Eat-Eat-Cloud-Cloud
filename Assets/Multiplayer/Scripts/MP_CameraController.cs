using UnityEngine;

public class MP_CameraController : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 10f; 
    private Transform targetTransform; 

    public void SetTarget(Transform target)
    {
        targetTransform = target; 
    }

    private void LateUpdate()
    {
        if (targetTransform == null)
            return; 
        
        Vector3 targetPosition = new Vector3(
            targetTransform.position.x, 
            targetTransform.position.y, 
            transform.position.z
            ); 

        transform.position = Vector3.Lerp(
            transform.position, 
            targetPosition, 
            lerpSpeed * Time.deltaTime
            ); 
    }
}
