using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MP_TopMessage : MonoBehaviour
{
    [SerializeField] private CanvasGroup messageGroup;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.3f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float breathPeriodSeconds = 1.5f;
    [SerializeField, Range(0f, 1f)] private float breathMinimumAlpha = 0.3f;
    [Header("Cycle Announcements")]
    [SerializeField, Min(0f)] private float cycleMessageDuration = 4f;

    private Coroutine messageCoroutine;
    private int lastAnnouncedCycle;
    private readonly Queue<(string text, float duration)> pendingMessages = new();
    private string currentMessage;

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
        lastAnnouncedCycle = 0;
        StopAnimation();
        pendingMessages.Clear();
        currentMessage = null;

        if (messageGroup != null)
            messageGroup.alpha = 0f;
    }

    private void Update()
    {
        MP_CycleTimer timer = MP_CycleTimer.Instance;
        int cycle = timer != null ? timer.CurrentCycle : 0;
        if (cycle == lastAnnouncedCycle)
            return;

        lastAnnouncedCycle = cycle;
        if (cycle <= 0)
        {
            FadeOut();
            return;
        }

        if (!timer.JoiningOpenForWholeRound && cycle > 1 &&
            cycle - 1 == timer.JoiningCycleLimit)
            ShowMessage("Joining and respawning are now closed", cycleMessageDuration);

        if (cycle == timer.FinalCycle - 1)
            ShowMessage("Last round before the final round", cycleMessageDuration);
        else if (cycle == timer.FinalCycle)
            ShowMessage("biggest player wins.", cycleMessageDuration);

        if (!timer.JoiningOpenForWholeRound && cycle == timer.JoiningCycleLimit)
            ShowMessage("Joining and respawning will close after this round", cycleMessageDuration);
    }

    public void ShowMessage(string message, float duration)
    {
        if (!isActiveAndEnabled || messageGroup == null || messageText == null)
            return;

        string requestedMessage = message ?? string.Empty;
        if (currentMessage == requestedMessage)
            return;

        foreach (var pending in pendingMessages)
            if (pending.text == requestedMessage)
                return;

        pendingMessages.Enqueue((requestedMessage, Mathf.Max(0f, duration)));
        if (messageCoroutine == null)
            messageCoroutine = StartCoroutine(ShowQueuedMessages());
    }

    public void FadeIn()
    {
        if (!isActiveAndEnabled || messageGroup == null)
            return;

        StopAnimation();
        pendingMessages.Clear();
        currentMessage = null;
        messageCoroutine = StartCoroutine(FadeAndResume(1f, fadeInDuration));
    }

    public void FadeOut()
    {
        if (!isActiveAndEnabled || messageGroup == null)
            return;

        StopAnimation();
        pendingMessages.Clear();
        currentMessage = null;
        messageCoroutine = StartCoroutine(FadeAndResume(0f, fadeOutDuration));
    }

    private void StopAnimation()
    {
        if (messageCoroutine == null)
            return;

        StopCoroutine(messageCoroutine);
        messageCoroutine = null;
    }

    private IEnumerator ShowQueuedMessages()
    {
        while (pendingMessages.Count > 0)
        {
            (string text, float duration) = pendingMessages.Dequeue();
            currentMessage = text;
            messageText.text = text;
            yield return FadeTo(1f, fadeInDuration);

            float elapsed = 0f;
            float breathPeriod = Mathf.Max(0.01f, breathPeriodSeconds);
            while (elapsed < duration)
            {
                float breath = 0.5f + 0.5f * Mathf.Cos(
                    elapsed * Mathf.PI * 2f / breathPeriod);
                messageGroup.alpha = Mathf.Lerp(breathMinimumAlpha, 1f, breath);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return FadeTo(0f, fadeOutDuration);
            currentMessage = null;
        }

        messageCoroutine = null;
    }

    private IEnumerator FadeAndResume(float targetAlpha, float duration)
    {
        yield return FadeTo(targetAlpha, duration);
        messageCoroutine = null;
        if (pendingMessages.Count > 0)
            messageCoroutine = StartCoroutine(ShowQueuedMessages());
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
