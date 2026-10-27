using PurrNet;
using PurrNet.Transports;
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
    [SerializeField] private Button spectatorButton;
    [SerializeField] private TextMeshProUGUI spectatorButtonText;
    [SerializeField] private Button retryButton;

    private NetworkManager connectionManager;
    private ConnectionState? observedClientState;
    private bool subscribedToConnectionState;
    private bool connectionAttempted;
    private bool connectionFailed;
    private bool retryRequested;

    private void OnEnable()
    {
        if (startPanel == null || startPanel == gameObject || transform.IsChildOf(startPanel.transform))
        {
            Debug.LogError("Assign a start panel that does not contain MP_StartScreen itself.", this);
            enabled = false;
            return;
        }

        TrackConnectionManager();
        Refresh();
    }

    private void Update()
    {
        TrackConnectionManager();
        Refresh();
    }

    private void OnDisable()
    {
        if (subscribedToConnectionState && connectionManager != null)
            connectionManager.onClientConnectionState -= HandleClientConnectionState;

        subscribedToConnectionState = false;
    }

    private void TrackConnectionManager()
    {
        NetworkManager current = NetworkManager.main;
        if (connectionManager != current)
        {
            if (subscribedToConnectionState && connectionManager != null)
                connectionManager.onClientConnectionState -= HandleClientConnectionState;

            connectionManager = current;
            subscribedToConnectionState = false;
            observedClientState = null;
            connectionAttempted = false;
            connectionFailed = false;
            retryRequested = false;
        }

        if (connectionManager == null)
            return;

        if (!subscribedToConnectionState)
        {
            connectionManager.onClientConnectionState += HandleClientConnectionState;
            subscribedToConnectionState = true;
        }

        ConnectionState state = connectionManager.clientState;
        if (observedClientState != state)
            HandleClientConnectionState(state);
    }

    private void HandleClientConnectionState(ConnectionState state)
    {
        observedClientState = state;
        if (state is ConnectionState.Connecting or ConnectionState.Connected)
        {
            connectionAttempted = true;
            connectionFailed = false;
            retryRequested = false;
        }
        else if (state == ConnectionState.Disconnected && connectionAttempted)
        {
            connectionFailed = true;
            retryRequested = false;
        }
    }

    private void Refresh()
    {
        MP_PlayerManager manager = MP_PlayerManager.Instance;
        MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
        bool spectating = MP_SpectatorMode.Instance != null && MP_SpectatorMode.Instance.IsSpectating;
        bool show = !spectating && (local == null ||
            (local.LocalPlayer == null && !local.HasDied));
        if (startPanel.activeSelf != show)
            startPanel.SetActive(show);

        ConnectionState clientState = connectionManager != null
            ? connectionManager.clientState : ConnectionState.Disconnected;
        bool showRetry = show && connectionFailed && !retryRequested &&
            clientState == ConnectionState.Disconnected;
        if (retryButton != null && retryButton.gameObject.activeSelf != showRetry)
            retryButton.gameObject.SetActive(showRetry);

        if (!show)
            return;

        string status = clientState != ConnectionState.Connected
            ? connectionFailed ? "Failed to connect to server." : "Connecting to server..."
            : manager != null ? manager.StartStatus : "Connecting to server...";
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

        bool canSpectate = manager != null && manager.CanSpectate;
        if (spectatorButton != null)
            spectatorButton.interactable = canSpectate;
        if (spectatorButtonText != null)
        {
            string buttonLabel = canSpectate ? "Spectate" : "Connecting";
            if (spectatorButtonText.text != buttonLabel)
                spectatorButtonText.text = buttonLabel;
        }
    }

    public void RequestStart()
    {
        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager != null && manager.TryRequestStart())
            Refresh();
    }

    public void RetryConnection()
    {
        TrackConnectionManager();
        if (!connectionFailed || retryRequested || connectionManager == null ||
            connectionManager.clientState != ConnectionState.Disconnected)
            return;

        retryRequested = true;
        connectionFailed = false;
        connectionManager.StartClient();
        Refresh();
    }
}
