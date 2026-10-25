using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keep this component on an active object outside the panel it hides
public class MP_DeathScreen : MonoBehaviour
{
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button respawnButton;

    private MP_LocalPlayerManager localPlayerManager;
    private MP_PlayerManager roundManager;
    private bool respawnPending;
    private float respawnRequestedAt;
    private const float RespawnTimeout = 10f;

    private void OnEnable()
    {
        if (deathPanel == null || deathPanel == gameObject || transform.IsChildOf(deathPanel.transform))
        {
            Debug.LogError("Assign a death panel that does not contain MP_DeathScreen itself.", this);
            return;
        }

        respawnPending = false;
        if (respawnButton != null)
            respawnButton.onClick.AddListener(RequestRespawn);

        localPlayerManager = MP_LocalPlayerManager.Instance;
        roundManager = MP_PlayerManager.Instance;
        if (roundManager != null)
            roundManager.OnRoundEnded += HideDeathScreen;
        if (localPlayerManager == null)
        {
            deathPanel.SetActive(false);
            Debug.LogError("MP_DeathScreen requires an active MP_LocalPlayerManager.", this);
            return;
        }

        localPlayerManager.OnLocalPlayerReady += HandleLocalPlayerReady;
        localPlayerManager.OnLocalPlayerDied += ShowDeath;
        localPlayerManager.OnRespawnFailed += ShowRespawnFailure;

        if (roundManager != null && roundManager.RoundEnded)
            HideDeathScreen();
        else if (localPlayerManager.HasDied)
            ShowDeath(localPlayerManager.KillerName);
        else
            deathPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (respawnButton != null)
            respawnButton.onClick.RemoveListener(RequestRespawn);
        if (roundManager != null)
            roundManager.OnRoundEnded -= HideDeathScreen;
        if (localPlayerManager != null)
        {
            localPlayerManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
            localPlayerManager.OnLocalPlayerDied -= ShowDeath;
            localPlayerManager.OnRespawnFailed -= ShowRespawnFailure;
        }
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        respawnPending = false;
        deathPanel.SetActive(false);
    }

    private void ShowDeath(string killerName)
    {
        if (roundManager != null && roundManager.RoundEnded)
        {
            HideDeathScreen();
            return;
        }

        respawnPending = false;
        if (respawnButton != null)
        {
            bool canRespawn = CanRespawn();
            respawnButton.gameObject.SetActive(canRespawn);
            respawnButton.interactable = canRespawn;
        }
        if (messageText != null)
        {
            // Player names are text, not TMP formatting tags
            messageText.richText = false;
            messageText.text = string.IsNullOrWhiteSpace(killerName)
                ? "You were eaten!"
                : $"You were eaten by {killerName}!";
        }

        deathPanel.SetActive(true);
    }

    private void HideDeathScreen()
    {
        respawnPending = false;
        if (respawnButton != null)
            respawnButton.interactable = false;
        deathPanel.SetActive(false);
    }

    private bool CanRespawn()
    {
        MP_CycleTimer timer = MP_CycleTimer.Instance;
        return (roundManager == null || !roundManager.RoundEnded) &&
            timer != null && timer.CurrentCycle == 1;
    }

    public void RequestRespawn()
    {
        if (respawnPending || !CanRespawn() || localPlayerManager == null ||
            !localPlayerManager.HasDied)
            return;

        respawnPending = true;
        respawnRequestedAt = Time.unscaledTime;
        if (respawnButton != null)
            respawnButton.interactable = false;
        if (messageText != null)
            messageText.text = "Respawning...";

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager == null || !manager.TryRequestRespawn())
            ShowRespawnFailure("Unable to contact the server. Please try again.");
    }

    private void Update()
    {
        if (roundManager == null && MP_PlayerManager.Instance != null)
        {
            roundManager = MP_PlayerManager.Instance;
            roundManager.OnRoundEnded += HideDeathScreen;
        }

        bool spectating = MP_SpectatorMode.Instance != null && MP_SpectatorMode.Instance.IsSpectating;
        if (spectating && deathPanel.activeSelf)
            deathPanel.SetActive(false);
        else if (roundManager != null && roundManager.RoundEnded && deathPanel.activeSelf)
            HideDeathScreen();
        else if (!spectating && localPlayerManager != null && localPlayerManager.HasDied &&
            (roundManager == null || !roundManager.RoundEnded) && !deathPanel.activeSelf)
            ShowDeath(localPlayerManager.KillerName);
        else if (deathPanel.activeSelf && respawnButton != null &&
            respawnButton.gameObject.activeSelf != CanRespawn())
            respawnButton.gameObject.SetActive(CanRespawn());

        if (respawnPending && Time.unscaledTime - respawnRequestedAt >= RespawnTimeout)
            ShowRespawnFailure("Respawn has not completed. Please try again.");
    }

    private void ShowRespawnFailure(string message)
    {
        if (localPlayerManager == null || !localPlayerManager.HasDied)
            return;

        if (roundManager != null && roundManager.RoundEnded)
        {
            HideDeathScreen();
            return;
        }

        respawnPending = false;
        if (respawnButton != null)
            respawnButton.interactable = CanRespawn();
        if (messageText != null)
            messageText.text = message;
    }
}
