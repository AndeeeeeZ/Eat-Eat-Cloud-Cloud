using UnityEngine;
using PurrNet;

public class MP_Food : NetworkBehaviour
{
    [SerializeField] private float expAmount = 1f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float despawnGraceSeconds = 0.25f;

    private readonly SyncVar<float> spawnSizeMultiplier = new(1f);
    private readonly SyncVar<bool> retiring = new(false);
    private MP_FoodSpawner foodSpawner;
    private Vector3 baseScale;
    private CircleCollider2D foodCollider;
    private SpriteRenderer[] sprites;
    private Color[] baseColors;
    private float spawnExpMultiplier = 1f;
    private int spawnCycle = 1;
    private float fadeInElapsed;
    private float fadeElapsed;
    private float fadeStartFraction = 1f;
    private float currentFade = 1f;
    private double edibleAt;
    private double retireAt;
    private bool consumed;

    public int SpawnCycle => spawnCycle;
    public bool IsRetiring => retiring.value;

    public float VisibilityRadius
    {
        get
        {
            if (foodCollider == null)
                return 0f;

            Vector3 scale = transform.lossyScale;
            return (foodCollider.radius + foodCollider.offset.magnitude) *
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        foodCollider = GetComponent<CircleCollider2D>();
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            baseColors[i] = sprites[i].color;
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer)
        {
            consumed = false;
            retiring.value = false;
        }

        spawnSizeMultiplier.onChanged -= ApplySizeMultiplier;
        spawnSizeMultiplier.onChanged += ApplySizeMultiplier;
        retiring.onChanged -= HandleRetiringChanged;
        retiring.onChanged += HandleRetiringChanged;
        ApplySizeMultiplier(spawnSizeMultiplier.value);
        HandleRetiringChanged(retiring.value);
    }

    protected override void OnDespawned(bool asServer)
    {
        spawnSizeMultiplier.onChanged -= ApplySizeMultiplier;
        retiring.onChanged -= HandleRetiringChanged;
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
        retiring.onChanged -= HandleRetiringChanged;
        if (foodSpawner != null)
            foodSpawner.UnregisterFood(this);

        base.OnDestroy();
    }

    private void ApplySizeMultiplier(float multiplier)
    {
        transform.localScale = baseScale * multiplier;
    }

    public void Initialize(MP_FoodSpawner spawner, int cycle, float sizeMultiplier,
        float expMultiplier)
    {
        foodSpawner = spawner;
        spawnCycle = Mathf.Max(1, cycle);
        spawnExpMultiplier = Mathf.Max(1f, expMultiplier);
        edibleAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, fadeInDuration);
        if (foodCollider != null)
            foodCollider.enabled = fadeInDuration <= 0f;
        spawnSizeMultiplier.value = sizeMultiplier;
        ApplySizeMultiplier(sizeMultiplier);
    }

    public void RetireIfOutdated(int currentCycle)
    {
        if (!isServer || consumed || retiring.value || spawnCycle > currentCycle - 2)
            return;

        retireAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, fadeOutDuration) +
            Mathf.Max(0f, despawnGraceSeconds);
        retiring.value = true;
    }

    private void HandleRetiringChanged(bool isRetiring)
    {
        fadeElapsed = 0f;
        if (foodCollider != null)
            foodCollider.enabled = !isRetiring &&
                (!isServer || Time.realtimeSinceStartupAsDouble >= edibleAt);

        if (isRetiring)
        {
            fadeStartFraction = currentFade;
        }
        else
        {
            fadeInElapsed = 0f;
            SetFade(fadeInDuration > 0f ? 0f : 1f);
        }
    }

    private void Update()
    {
        if (retiring.value)
        {
            if (!Application.isBatchMode)
            {
                fadeElapsed += Time.deltaTime;
                float fade = fadeOutDuration > 0f ?
                    1f - Mathf.Clamp01(fadeElapsed / fadeOutDuration) : 0f;
                SetFade(fadeStartFraction * fade);
            }

            if (isServer && Time.realtimeSinceStartupAsDouble >= retireAt)
            {
                consumed = true;
                if (foodSpawner != null)
                {
                    foodSpawner.RespawnFood(this);
                    foodSpawner = null;
                }

                Despawn();
            }
        }
        else if (!Application.isBatchMode && fadeInElapsed < fadeInDuration)
        {
            fadeInElapsed += Time.deltaTime;
            SetFade(Mathf.Clamp01(fadeInElapsed / fadeInDuration));
        }

        if (isServer && !retiring.value && !consumed && foodCollider != null &&
            !foodCollider.enabled && Time.realtimeSinceStartupAsDouble >= edibleAt)
            foodCollider.enabled = true;
    }

    private void SetFade(float fraction)
    {
        currentFade = fraction;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            Color color = baseColors[i];
            color.a *= fraction;
            sprites[i].color = color;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer || consumed || retiring.value ||
            Time.realtimeSinceStartupAsDouble < edibleAt)
            return; 

        MP_PlayerGrowth player = collision.GetComponent<MP_PlayerGrowth>(); 

        if (player == null)
            return; 

        consumed = true;
        player.GainExperience(expAmount * spawnExpMultiplier);

        if (foodSpawner != null)
        {
            foodSpawner.RespawnFood(this);
            foodSpawner = null;
        }

        Despawn();
    }
}
