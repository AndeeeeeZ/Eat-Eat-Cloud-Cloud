using TMPro;
using UnityEngine;

// Keep this component on an active object outside the panel it hides
public class MP_WinScreen : MonoBehaviour
{
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TextMeshProUGUI messageText;

    private MP_PlayerManager roundManager;
    private MP_LocalPlayerManager localPlayerManager;
    private bool configured;

    private void OnEnable()
    {
        configured = winPanel != null && messageText != null && winPanel != gameObject &&
            !transform.IsChildOf(winPanel.transform);
        if (!configured)
        {
            Debug.LogError("Assign a win panel outside MP_WinScreen's object and its message text.", this);
            return;
        }

        roundManager = MP_PlayerManager.Instance;
        if (roundManager != null)
            roundManager.OnRoundEnded += ShowResult;

        localPlayerManager = MP_LocalPlayerManager.Instance;
        if (localPlayerManager != null)
            localPlayerManager.OnLocalPlayerReady += HandleLocalPlayerReady;

        if (roundManager != null && roundManager.RoundEnded)
            ShowResult();
        else
            winPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (roundManager != null)
            roundManager.OnRoundEnded -= ShowResult;
        if (localPlayerManager != null)
            localPlayerManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
        if (configured && winPanel != null)
            winPanel.SetActive(false);
    }

    private void Update()
    {
        if (!configured)
            return;

        if (roundManager == null && MP_PlayerManager.Instance != null)
        {
            roundManager = MP_PlayerManager.Instance;
            roundManager.OnRoundEnded += ShowResult;
        }

        if (localPlayerManager == null && MP_LocalPlayerManager.Instance != null)
        {
            localPlayerManager = MP_LocalPlayerManager.Instance;
            localPlayerManager.OnLocalPlayerReady += HandleLocalPlayerReady;
            if (roundManager != null && roundManager.RoundEnded)
                ShowResult();
        }

        if (roundManager != null && roundManager.RoundEnded && !winPanel.activeSelf)
            ShowResult();
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        if (roundManager != null && roundManager.RoundEnded)
            ShowResult();
    }

    private void ShowResult()
    {
        if (!configured || roundManager == null || !roundManager.RoundEnded)
            return;

        string winnerName = roundManager.WinnerName;
        messageText.richText = false;
        messageText.text = localPlayerManager != null &&
            roundManager.IsWinner(localPlayerManager.LocalPlayer)
            ? "You won!"
            : $"{(string.IsNullOrWhiteSpace(winnerName) ? "A cloud" : winnerName)} won!";
        winPanel.SetActive(true);
    }
}
