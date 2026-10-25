using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keep this component on an active object outside the panel it hides
public class MP_StartScreen : MonoBehaviour
{
    [SerializeField] private GameObject startPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI startButtonText;

    private void OnEnable()
    {
        if (startPanel == null || startPanel == gameObject || transform.IsChildOf(startPanel.transform))
        {
            Debug.LogError("Assign a start panel that does not contain MP_StartScreen itself.", this);
            enabled = false;
            return;
        }

        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        MP_PlayerManager manager = MP_PlayerManager.Instance;
        MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
        bool show = local == null || (local.LocalPlayer == null && !local.HasDied);
        if (startPanel.activeSelf != show)
            startPanel.SetActive(show);
        if (!show)
            return;

        string status = manager != null ? manager.StartStatus : "Connecting to server...";
        if (statusText != null && statusText.text != status)
            statusText.text = status;

        bool joiningOpen = manager == null || (!manager.RoundEnded &&
            (MP_CycleTimer.Instance == null || MP_CycleTimer.Instance.CurrentCycle <= 1));
        bool canStart = manager != null && manager.CanStart;
        if (startButton != null)
        {
            if (startButton.gameObject.activeSelf != joiningOpen)
                startButton.gameObject.SetActive(joiningOpen);
            startButton.interactable = canStart;
        }

        if (startButtonText != null)
        {
            string buttonLabel = canStart ? "Start" : "Connecting";
            if (startButtonText.text != buttonLabel)
                startButtonText.text = buttonLabel;
        }
    }

    public void RequestStart()
    {
        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager != null && manager.TryRequestStart())
            Refresh();
    }
}
