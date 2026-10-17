using PurrNet;
using UnityEngine;

public class MP_FoodSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private int startingFoodCount = 100;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (!isServer)
            return;

        if (MP_GameBoundary.Instance == null)
        {
            Debug.LogError("Add MP_GameBoundary to the scene before spawning food", this);
            return;
        }

        if (foodPrefab == null)
        {
            Debug.LogError("Assign a food prefab on MP_FoodSpawner", this);
            return;
        }

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
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary == null || foodPrefab == null)
            return;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        float sizeMultiplier = timer != null ? timer.FoodSizeMultiplier : 1f;
        CircleCollider2D collider = foodPrefab.GetComponent<CircleCollider2D>();
        float radius = 0f;
        if (collider != null)
        {
            Vector3 scale = foodPrefab.transform.lossyScale;
            float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            radius = (collider.radius + collider.offset.magnitude) * largestScale * sizeMultiplier;
        }

        if (!boundary.TryGetRandomPosition(radius, out Vector2 position))
        {
            Debug.LogWarning("Food no longer fits inside MP_GameBoundary", this);
            return;
        }

        GameObject foodObject = Instantiate(foodPrefab, position, Quaternion.identity, transform);
        
        MP_Food food = foodObject.GetComponent<MP_Food>(); 

        if (food == null)
        {
            Debug.LogError($"Missing MP_Food on {foodObject.name}", this);
            Destroy(foodObject);
            return;  
        }

        food.Initialize(this, sizeMultiplier);
    }
}
