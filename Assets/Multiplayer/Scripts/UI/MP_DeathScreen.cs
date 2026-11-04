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
    private string deathMessage = "";
    private string respawnStatus = "";
    private int displayedJoinSeconds = int.MinValue;
    private bool displayedJoiningOpen;
    private bool displayedUnlimitedJoining;
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
        {
            roundManager.OnRoundEnded += HideDeathScreen;
            roundManager.OnRoundRestarted += HideDeathScreen;
        }
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
        {
            roundManager.OnRoundEnded -= HideDeathScreen;
            roundManager.OnRoundRestarted -= HideDeathScreen;
        }
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
        deathMessage = string.IsNullOrWhiteSpace(killerName)
            ? "You were eaten!"
            : $"You were eaten by \n{killerName}!";
        respawnStatus = "";
        displayedJoinSeconds = int.MinValue;
        if (messageText != null)
            messageText.richText = false;
        RefreshDeathMessage();

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
            timer != null && timer.IsJoiningOpen;
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
        respawnStatus = "Respawning...";
        displayedJoinSeconds = int.MinValue;
        RefreshDeathMessage();

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
            roundManager.OnRoundRestarted += HideDeathScreen;
        }

        bool spectating = MP_SpectatorMode.Instance != null && MP_SpectatorMode.Instance.IsSpectating;
        if (spectating && deathPanel.activeSelf)
            deathPanel.SetActive(false);
        else if (roundManager != null && roundManager.RoundEnded && deathPanel.activeSelf)
            HideDeathScreen();
        else if (localPlayerManager != null && !localPlayerManager.HasDied && deathPanel.activeSelf)
            HideDeathScreen();
        else if (!spectating && localPlayerManager != null && localPlayerManager.HasDied &&
            (roundManager == null || !roundManager.RoundEnded) && !deathPanel.activeSelf)
            ShowDeath(localPlayerManager.KillerName);
        else if (deathPanel.activeSelf && respawnButton != null &&
            respawnButton.gameObject.activeSelf != CanRespawn())
            respawnButton.gameObject.SetActive(CanRespawn());

        if (deathPanel.activeSelf)
            RefreshDeathMessage();

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
        respawnStatus = message;
        displayedJoinSeconds = int.MinValue;
        RefreshDeathMessage();
    }

    private void RefreshDeathMessage()
    {
        if (messageText == null)
            return;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        bool joiningOpen = (roundManager == null || !roundManager.RoundEnded) &&
            timer != null && timer.IsJoiningOpen;
        bool unlimitedJoining = joiningOpen && timer.JoiningOpenForWholeRound;
        int seconds = joiningOpen ? timer.SecondsUntilJoiningCloses : 0;
        if (seconds == displayedJoinSeconds && joiningOpen == displayedJoiningOpen &&
            unlimitedJoining == displayedUnlimitedJoining)
            return;

        displayedJoinSeconds = seconds;
        displayedJoiningOpen = joiningOpen;
        displayedUnlimitedJoining = unlimitedJoining;
        string text = deathMessage;
        if (unlimitedJoining)
            text += "\nRespawning stays open for the whole round.";
        else if (joiningOpen)
            text += $"\n{seconds / 60:00}:{seconds % 60:00} until respawning closes.";
        if (!string.IsNullOrEmpty(respawnStatus))
            text += $"\n{respawnStatus}";
        messageText.text = text;
    }
}
