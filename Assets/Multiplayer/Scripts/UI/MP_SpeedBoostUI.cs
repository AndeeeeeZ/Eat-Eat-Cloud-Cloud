using System.Globalization;
using TMPro;
using UnityEngine;

// Keep this component on an active object outside the display it hides
public class MP_SpeedBoostUI : MonoBehaviour
{
    [SerializeField] private GameObject abilityDisplay;
    [SerializeField] private UnityEngine.UI.Image radialOverlay;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Color activeOverlayColor = new(1f, 0.95f, 0.4f, 0.7f);
    [SerializeField] private Color cooldownOverlayColor = new(0.15f, 0.15f, 0.15f, 0.75f);
    [SerializeField] private Color cooldownAttemptTextColor = new(1f, 0.3f, 0.3f, 1f);
    [SerializeField, Min(0f)] private float shakeDurationSeconds = 0.35f;
    [SerializeField, Min(0f)] private float shakeDistance = 8f;
    [SerializeField, Min(0f)] private float shakeCyclesPerSecond = 20f;

    private MP_Player displayedPlayer;
    private MP_PlayerMovement playerMovement;
    private int shownCountdownStep = -1;
    private Color normalTextColor;
    private Vector2 textRestPosition;
    private float shakeStartedAt;
    private bool isShaking;

    private void OnEnable()
    {
        if (abilityDisplay == null || abilityDisplay == gameObject ||
            transform.IsChildOf(abilityDisplay.transform) ||
            radialOverlay == null || countdownText == null)
        {
            Debug.LogError("Assign a child ability display, radial overlay, and countdown text on MP_SpeedBoostUI", this);
            enabled = false;
            return;
        }

        radialOverlay.type = UnityEngine.UI.Image.Type.Filled;
        radialOverlay.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        shownCountdownStep = -1;
        abilityDisplay.SetActive(false);
    }

    private void OnDisable()
    {
        if (playerMovement != null)
            playerMovement.OnSpeedBoostCooldownAttempt -= HandleCooldownAttempt;
        StopShake();
        displayedPlayer = null;
        playerMovement = null;
        shownCountdownStep = -1;
        if (abilityDisplay != null && abilityDisplay != gameObject)
            abilityDisplay.SetActive(false);
    }

    private void Update()
    {
        MP_LocalPlayerManager manager = MP_LocalPlayerManager.Instance;
        MP_Player localPlayer = manager != null ? manager.LocalPlayer : null;
        if (!ReferenceEquals(displayedPlayer, localPlayer))
        {
            if (playerMovement != null)
                playerMovement.OnSpeedBoostCooldownAttempt -= HandleCooldownAttempt;
            StopShake();
            displayedPlayer = localPlayer;
            playerMovement = localPlayer != null
                ? localPlayer.GetComponent<MP_PlayerMovement>() : null;
            if (playerMovement != null)
                playerMovement.OnSpeedBoostCooldownAttempt += HandleCooldownAttempt;
            shownCountdownStep = -1;
            abilityDisplay.SetActive(playerMovement != null);
        }

        if (playerMovement == null)
            return;

        if (playerMovement.IsSpeedBoostActive)
        {
            StopShake();
            radialOverlay.color = activeOverlayColor;
            radialOverlay.fillAmount = 1f - Mathf.Clamp01(
                playerMovement.SpeedBoostRemaining / playerMovement.SpeedBoostDuration);
            countdownText.enabled = false;
            shownCountdownStep = -1;
            return;
        }

        float remaining = playerMovement.SpeedBoostCooldownRemaining;
        radialOverlay.color = cooldownOverlayColor;
        radialOverlay.fillAmount = playerMovement.SpeedBoostCooldown > 0f
            ? Mathf.Clamp01(remaining / playerMovement.SpeedBoostCooldown) : 0f;
        int countdownStep = remaining >= 1f
            ? Mathf.RoundToInt(remaining) * 10
            : Mathf.FloorToInt(Mathf.Max(0f, remaining) * 10f);
        if (countdownStep == 0 && (!isShaking || remaining <= 0f))
        {
            StopShake();
            countdownText.enabled = false;
            shownCountdownStep = -1;
            return;
        }

        if (countdownStep == 0)
            countdownStep = 1;

        countdownText.enabled = true;
        if (countdownStep != shownCountdownStep)
        {
            shownCountdownStep = countdownStep;
            countdownText.text = FormatCountdown(countdownStep);
        }

        UpdateShake();
    }

    private void HandleCooldownAttempt()
    {
        if (playerMovement == null || playerMovement.SpeedBoostCooldownRemaining <= 0f)
            return;

        if (!isShaking)
        {
            normalTextColor = countdownText.color;
            textRestPosition = countdownText.rectTransform.anchoredPosition;
        }

        shakeStartedAt = Time.unscaledTime;
        isShaking = true;
        countdownText.color = cooldownAttemptTextColor;
        countdownText.enabled = true;
    }

    private void UpdateShake()
    {
        if (!isShaking)
            return;

        float elapsed = Time.unscaledTime - shakeStartedAt;
        if (shakeDurationSeconds <= 0f || elapsed >= shakeDurationSeconds)
        {
            StopShake();
            return;
        }

        float fade = 1f - elapsed / shakeDurationSeconds;
        float phase = elapsed * Mathf.Max(0f, shakeCyclesPerSecond) * Mathf.PI * 2f;
        float offset = Mathf.Sin(phase) * Mathf.Max(0f, shakeDistance) * fade;
        countdownText.rectTransform.anchoredPosition = textRestPosition + new Vector2(offset, 0f);
        countdownText.color = cooldownAttemptTextColor;
    }

    private void StopShake()
    {
        if (!isShaking)
            return;

        isShaking = false;
        countdownText.rectTransform.anchoredPosition = textRestPosition;
        countdownText.color = normalTextColor;
    }

    private static string FormatCountdown(int countdownStep)
    {
        if (countdownStep >= 10)
            return (countdownStep / 10).ToString(CultureInfo.InvariantCulture);

        return (countdownStep / 10f).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
