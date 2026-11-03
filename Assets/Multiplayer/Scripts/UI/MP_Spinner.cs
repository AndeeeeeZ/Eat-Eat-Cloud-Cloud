using UnityEngine;

public class MP_Spinner : MonoBehaviour
{
    [SerializeField] private float rotationSpeed;

    void Update()
    {
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}
