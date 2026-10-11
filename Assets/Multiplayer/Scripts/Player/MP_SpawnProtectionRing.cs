using UnityEngine;

// Visual only, driven by the parent player's synchronized protection state
public class MP_SpawnProtectionRing : MonoBehaviour
{
    [SerializeField] private SpriteRenderer ringSprite;
    [SerializeField] private Vector2 alphaRange = new(0.25f, 0.8f);
    [SerializeField, Min(0.01f)] private float pulseDuration = 1.5f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;

    private bool initialized;
    private bool protectedState;
    private float pulseTime;
    private float fadeTime;
    private float fadeStartAlpha;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        if (ringSprite == null)
            ringSprite = GetComponent<SpriteRenderer>();
        SetAlpha(0f);
    }

    public void SetProtected(bool isProtected)
    {
        // Initialization also works when the ring child starts inactive
        Initialize();
        if (isProtected == protectedState)
            return;

        protectedState = isProtected;
        if (isProtected)
        {
            gameObject.SetActive(true);
            pulseTime = 0f;
            SetAlpha(Mathf.Clamp01(Mathf.Min(alphaRange.x, alphaRange.y)));
        }
        else
        {
            // Fade from the current alpha without changing the server's protection timer
            fadeTime = 0f;
            fadeStartAlpha = ringSprite != null ? ringSprite.color.a : 0f;
            if (fadeOutDuration <= 0f || fadeStartAlpha <= 0f)
                HideImmediately();
        }
    }

    public void HideImmediately()
    {
        Initialize();
        protectedState = false;
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (protectedState)
        {
            float duration = Mathf.Max(0.01f, pulseDuration);
            pulseTime = Mathf.Repeat(pulseTime + Time.deltaTime, duration);
            float blend = 0.5f - 0.5f * Mathf.Cos(pulseTime / duration * Mathf.PI * 2f);
            float minAlpha = Mathf.Clamp01(Mathf.Min(alphaRange.x, alphaRange.y));
            float maxAlpha = Mathf.Clamp01(Mathf.Max(alphaRange.x, alphaRange.y));
            SetAlpha(Mathf.Lerp(minAlpha, maxAlpha, blend));
        }
        else
        {
            fadeTime += Time.deltaTime;
            float progress = fadeOutDuration > 0f ? Mathf.Clamp01(fadeTime / fadeOutDuration) : 1f;
            SetAlpha(Mathf.SmoothStep(fadeStartAlpha, 0f, progress));
            if (progress >= 1f)
                HideImmediately();
        }
    }

    private void OnDisable()
    {
        protectedState = false;
        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        if (ringSprite == null)
            return;

        Color color = ringSprite.color;
        color.a = alpha;
        ringSprite.color = color;
    }
}
