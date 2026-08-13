using UnityEngine; 
using PurrNet; 

// Inherit from PlayerIdentity and notify local player manager on spawn
public class MP_Player : PlayerIdentity<MP_Player>
{
    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
    
        if (!asServer && isOwner)
        {
            if (MP_LocalPlayerManager.Instance == null)
            {
                Debug.LogError("MP_LocalPlayerManager doesn't exist when player spawned", this); 
            }
            // Notify the local player manager that sends out event to other local systems
            MP_LocalPlayerManager.Instance.SetLocalPlayer(this); 
        }
    }
}