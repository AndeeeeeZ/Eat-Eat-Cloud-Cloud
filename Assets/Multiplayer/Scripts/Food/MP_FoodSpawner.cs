using System.Runtime.InteropServices;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;

public class MP_FoodSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private int startingFoodCount = 100;

    [SerializeField] private Vector2 xRange;
    [SerializeField] private Vector2 yRange;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (!isServer)
            return;

        for (int i = 0; i < startingFoodCount; i++)
        {
            SpawnFood();
        }
    }

    public void RespawnFood()
    {
        if (!isServer)
            return; 

        SpawnFood(); 
    }

    private void SpawnFood()
    {
        Vector2 position = new Vector2(
            Random.Range(xRange.x, xRange.y),
            Random.Range(yRange.x, yRange.y)
        );

        GameObject foodObject = Instantiate(foodPrefab, position, Quaternion.identity, transform);
        
        MP_Food food = foodObject.GetComponent<MP_Food>(); 

        if (food == null)
        {
            Debug.LogError($"Missing MP_Food on {food.name}");
            return;  
        }

        food.Initialize(this); 
    }
}
