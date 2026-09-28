using UnityEngine;
using PurrNet;

public class MP_Food : NetworkBehaviour
{
    [SerializeField] private float expAmount = 1f; 

    private MP_FoodSpawner foodSpawner; 

    public void Initialize(MP_FoodSpawner spawner)
    {
        foodSpawner = spawner; 
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
            return; 

        MP_PlayerGrowth player = collision.GetComponent<MP_PlayerGrowth>(); 

        if (player == null)
            return; 

        player.GainExperience(expAmount); 

        foodSpawner.RespawnFood(); 

        Destroy(gameObject); 
    }
}
