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
    private readonly SyncVar<bool> fadeInOnSpawn = new(false);
    private MP_FoodSpawner foodSpawner;
    private Vector3 baseScale;
    private CircleCollider2D foodCollider;
    private SpriteRenderer[] sprites;
    private Color[] baseColors;
    private float spawnExpMultiplier = 1f;
    private int spawnCycle = 1;
    private bool isFadingIn;
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
        fadeInOnSpawn.onChanged -= HandleFadeInChanged;
        fadeInOnSpawn.onChanged += HandleFadeInChanged;
        isFadingIn = false;
        ApplySizeMultiplier(spawnSizeMultiplier.value);
        HandleRetiringChanged(retiring.value);
    }

    protected override void OnDespawned(bool asServer)
    {
        spawnSizeMultiplier.onChanged -= ApplySizeMultiplier;
        retiring.onChanged -= HandleRetiringChanged;
        fadeInOnSpawn.onChanged -= HandleFadeInChanged;
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
        fadeInOnSpawn.onChanged -= HandleFadeInChanged;
        if (foodSpawner != null)
            foodSpawner.UnregisterFood(this);

        base.OnDestroy();
    }

    private void ApplySizeMultiplier(float multiplier)
    {
        transform.localScale = baseScale * multiplier;
    }

    public void Initialize(MP_FoodSpawner spawner, int cycle, float sizeMultiplier,
        float expMultiplier, bool visibleAtSpawn)
    {
        foodSpawner = spawner;
        spawnCycle = Mathf.Max(1, cycle);
        spawnExpMultiplier = Mathf.Max(1f, expMultiplier);
        edibleAt = Time.realtimeSinceStartupAsDouble +
            (visibleAtSpawn ? Mathf.Max(0f, fadeInDuration) : 0f);
        if (foodCollider != null)
            foodCollider.enabled = !visibleAtSpawn || fadeInDuration <= 0f;
        fadeInOnSpawn.value = visibleAtSpawn;
        spawnSizeMultiplier.value = sizeMultiplier;
        ApplySizeMultiplier(sizeMultiplier);
    }

    public void ExpireIfOutdated(int currentCycle)
    {
        if (!isServer || consumed || retiring.value || spawnCycle > currentCycle - 2)
            return;

        consumed = true;
        if (foodSpawner != null)
        {
            foodSpawner.RespawnFood(this);
            foodSpawner = null;
        }

        Despawn();
    }

    private void HandleFadeInChanged(bool shouldFadeIn)
    {
        if (retiring.value)
            return;

        if (!shouldFadeIn && isFadingIn)
            return;

        isFadingIn = shouldFadeIn && fadeInDuration > 0f;
        fadeInElapsed = 0f;
        SetFade(isFadingIn ? 0f : 1f);
    }

    private void HandleRetiringChanged(bool isRetiring)
    {
        fadeElapsed = 0f;
        if (foodCollider != null)
            foodCollider.enabled = !isRetiring &&
                (!isServer || Time.realtimeSinceStartupAsDouble >= edibleAt);

        if (isRetiring)
        {
            isFadingIn = false;
            fadeStartFraction = currentFade;
        }
        else
        {
            HandleFadeInChanged(fadeInOnSpawn.value);
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
        else if (!Application.isBatchMode && isFadingIn)
        {
            fadeInElapsed += Time.deltaTime;
            SetFade(Mathf.Clamp01(fadeInElapsed / fadeInDuration));
            if (fadeInElapsed >= fadeInDuration)
                isFadingIn = false;
        }

        if (isServer && !retiring.value && !consumed && foodCollider != null &&
            !foodCollider.enabled && Time.realtimeSinceStartupAsDouble >= edibleAt)
            foodCollider.enabled = true;

        if (isServer && fadeInOnSpawn.value && Time.realtimeSinceStartupAsDouble >= edibleAt)
            fadeInOnSpawn.value = false;
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

        bool fadeOut = foodSpawner != null && fadeOutDuration > 0f &&
            foodSpawner.IsFoodVisibleToAnyPlayer(this);

        if (foodSpawner != null)
        {
            foodSpawner.RespawnFood(this);
            foodSpawner = null;
        }

        if (fadeOut)
        {
            if (foodCollider != null)
                foodCollider.enabled = false;
            retireAt = Time.realtimeSinceStartupAsDouble + fadeOutDuration +
                Mathf.Max(0f, despawnGraceSeconds);
            retiring.value = true;
        }
        else
        {
            Despawn();
        }
    }
}
