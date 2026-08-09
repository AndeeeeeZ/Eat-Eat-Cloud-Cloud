using UnityEngine;
using PurrNet;

public class MP_Food : NetworkBehaviour
{
    [SerializeField] private float expAmount = 1f; 
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
            return; 

        MP_PlayerStats player = collision.GetComponent<MP_PlayerStats>(); 

        if (player == null)
            return; 

        player.GainExperience(expAmount); 

        Destroy(gameObject); 
    }
}
