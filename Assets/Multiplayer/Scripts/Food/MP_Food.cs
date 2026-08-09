using UnityEngine;
using PurrNet;

public class MP_Food : NetworkBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
            return; 

        MP_PlayerMovement player = collision.GetComponent<MP_PlayerMovement>(); 

        if (player = null)
            return; 
        
        Debug.Log("Eat one food"); 
        Destroy(gameObject); 
    }
}
