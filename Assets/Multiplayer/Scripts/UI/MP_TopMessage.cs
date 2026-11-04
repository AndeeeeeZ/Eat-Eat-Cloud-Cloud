using System.Collections;
using TMPro;
using UnityEngine;

public class MP_TopMessage : MonoBehaviour
{
    private const float BreathPeriodSeconds = 1.5f;
    private const float BreathMinimumAlpha = 0.3f;

    [SerializeField] private CanvasGroup messageGroup;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.3f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.3f;

    private Coroutine animation;

    private void Awake()
    {
        if (messageGroup == null)
            messageGroup = GetComponent<CanvasGroup>();

        if (messageGroup == null || messageText == null)
        {
            Debug.LogError("Assign a CanvasGroup and message text on MP_TopMessage", this);
            enabled = false;
            return;
        }

        messageGroup.alpha = 0f;
        messageGroup.interactable = false;
        messageGroup.blocksRaycasts = false;
    }

    private void OnDisable()
    {
        if (animation != null)
        {
            StopCoroutine(animation);
            animation = null;
        }

        if (messageGroup != null)
            messageGroup.alpha = 0f;
    }

    public void ShowMessage(string message, float duration)
    {
        if (!isActiveAndEnabled || messageGroup == null || messageText == null)
            return;

        StopAnimation();
        messageText.text = message ?? string.Empty;
        animation = StartCoroutine(ShowRoutine(Mathf.Max(0f, duration)));
    }

    public void FadeIn()
    {
        if (!isActiveAndEnabled || messageGroup == null)
            return;

        StopAnimation();
        animation = StartCoroutine(FadeTo(1f, fadeInDuration));
    }

    public void FadeOut()
    {
        if (!isActiveAndEnabled || messageGroup == null)
            return;

        StopAnimation();
        animation = StartCoroutine(FadeTo(0f, fadeOutDuration));
    }

    private void StopAnimation()
    {
        if (animation == null)
            return;

        StopCoroutine(animation);
        animation = null;
    }

    private IEnumerator ShowRoutine(float duration)
    {
        yield return FadeTo(1f, fadeInDuration);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float breath = 0.5f + 0.5f * Mathf.Cos(
                elapsed * Mathf.PI * 2f / BreathPeriodSeconds);
            messageGroup.alpha = Mathf.Lerp(BreathMinimumAlpha, 1f, breath);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeTo(0f, fadeOutDuration);
        animation = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = messageGroup.alpha;
        if (duration <= 0f)
        {
            messageGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            messageGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        messageGroup.alpha = targetAlpha;
    }
}
