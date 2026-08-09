using PurrNet;
using UnityEngine;

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

    private void SpawnFood()
    {
        Vector2 position = new Vector2(
            Random.Range(xRange.x, xRange.y),
            Random.Range(yRange.x, yRange.y)
        );

        Instantiate(foodPrefab, position, Quaternion.identity, transform);
    }
}
