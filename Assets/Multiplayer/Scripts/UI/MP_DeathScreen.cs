using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keep this component on an active object outside the panel it hides.
public class MP_DeathScreen : MonoBehaviour
{
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button respawnButton;

    private MP_LocalPlayerManager localPlayerManager;

    private void OnEnable()
    {
        if (deathPanel == null || deathPanel == gameObject || transform.IsChildOf(deathPanel.transform))
        {
            Debug.LogError("Assign a death panel that does not contain MP_DeathScreen itself.", this);
            return;
        }

        // Respawn requests will be implemented in the next step.
        if (respawnButton != null)
            respawnButton.interactable = false;

        localPlayerManager = MP_LocalPlayerManager.Instance;
        if (localPlayerManager == null)
        {
            deathPanel.SetActive(false);
            Debug.LogError("MP_DeathScreen requires an active MP_LocalPlayerManager.", this);
            return;
        }

        localPlayerManager.OnLocalPlayerReady += HandleLocalPlayerReady;
        localPlayerManager.OnLocalPlayerDied += ShowDeath;

        if (localPlayerManager.HasDied)
            ShowDeath(localPlayerManager.KillerName);
        else
            deathPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (localPlayerManager != null)
        {
            localPlayerManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
            localPlayerManager.OnLocalPlayerDied -= ShowDeath;
        }
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        deathPanel.SetActive(false);
    }

    private void ShowDeath(string killerName)
    {
        if (messageText != null)
        {
            // Player names are text, not TMP formatting tags.
            messageText.richText = false;
            messageText.text = string.IsNullOrWhiteSpace(killerName)
                ? "You were eaten!"
                : $"You were eaten by {killerName}!";
        }

        deathPanel.SetActive(true);
    }
}
