using UnityEngine;
using PurrNet;

public class MP_Food : NetworkBehaviour
{
    [SerializeField] private float expAmount = 1f; 

    private readonly SyncVar<float> spawnSizeMultiplier = new(1f);
    private MP_FoodSpawner foodSpawner; 
    private Vector3 baseScale;
    private bool consumed;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer)
            consumed = false;

        spawnSizeMultiplier.onChanged -= ApplySizeMultiplier;
        spawnSizeMultiplier.onChanged += ApplySizeMultiplier;
        ApplySizeMultiplier(spawnSizeMultiplier.value);
    }

    protected override void OnDespawned(bool asServer)
    {
        spawnSizeMultiplier.onChanged -= ApplySizeMultiplier;
        if (asServer && foodSpawner != null)
        {
            foodSpawner.UnregisterFood(this);
            foodSpawner = null;
        }

        base.OnDespawned(asServer);
    }

    protected override void OnDestroy()
    {
        spawnSizeMultiplier.onChanged -= ApplySizeMultiplier;
        if (foodSpawner != null)
            foodSpawner.UnregisterFood(this);

        base.OnDestroy();
    }

    private void ApplySizeMultiplier(float multiplier)
    {
        transform.localScale = baseScale * multiplier;
    }

    public void Initialize(MP_FoodSpawner spawner, float sizeMultiplier)
    {
        foodSpawner = spawner;
        spawnSizeMultiplier.value = sizeMultiplier;
        ApplySizeMultiplier(sizeMultiplier);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer || consumed)
            return; 

        MP_PlayerGrowth player = collision.GetComponent<MP_PlayerGrowth>(); 

        if (player == null)
            return; 

        consumed = true;
        player.GainExperience(expAmount); 

        if (foodSpawner != null)
            foodSpawner.RespawnFood(this);

        Destroy(gameObject); 
    }
}
