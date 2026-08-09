using PurrNet; 
using UnityEngine; 

// On spawn, gives the references of the current object to the camera for camera movement
public class MP_LocalPlayerCamera : NetworkBehaviour
{
    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (isOwner)
        {
            Camera.main.GetComponent<MP_CameraController>().SetTarget(transform); 
        }
    }
}