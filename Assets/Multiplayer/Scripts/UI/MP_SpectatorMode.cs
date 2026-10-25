using System.Collections.Generic;
using PurrNet;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keep this component active outside the panel it hides
public class MP_SpectatorMode : MonoBehaviour
{
    [SerializeField] private GameObject spectatorPanel;
    [SerializeField] private TextMeshProUGUI viewedPlayerText;
    [SerializeField] private MP_CameraController cameraController;
    [SerializeField] private bool includeAI = true;

    private const float TargetRefreshInterval = 0.25f;
    private const string LobbySceneName = "LobbyScene";
    private readonly List<MP_Player> players = new();
    private MP_Player viewedPlayer;
    private int viewedIndex;
    private float nextTargetRefresh;

    public static MP_SpectatorMode Instance { get; private set; }
    public bool IsSpectating { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one MP_SpectatorMode should be active in a scene", this);
            enabled = false;
            return;
        }

        Instance = this;
        if (viewedPlayerText != null)
            viewedPlayerText.richText = false;
    }

    private void OnEnable()
    {
        if (spectatorPanel == null || spectatorPanel == gameObject ||
            transform.IsChildOf(spectatorPanel.transform) || cameraController == null ||
            viewedPlayerText == null)
        {
            Debug.LogError("Assign a spectator panel outside this component, its text, and a camera controller.", this);
            enabled = false;
            return;
        }

        spectatorPanel.SetActive(false);
    }

    private void OnDisable()
    {
        ReturnToWaitingRoom();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsSpectating)
            return;

        MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
        if (local != null && local.LocalPlayer != null)
        {
            ReturnToWaitingRoom();
            return;
        }

        if (Time.unscaledTime >= nextTargetRefresh)
        {
            nextTargetRefresh = Time.unscaledTime + TargetRefreshInterval;
            RefreshPlayers();
        }

        UpdateViewedPlayerText();
    }

    public void EnterSpectatorMode()
    {
        MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
        if (!isActiveAndEnabled || IsSpectating || (local != null && local.LocalPlayer != null))
            return;

        IsSpectating = true;
        viewedPlayer = null;
        viewedIndex = 0;
        nextTargetRefresh = 0f;
        spectatorPanel.SetActive(true);
        RefreshPlayers();
    }

    public void PreviousPlayer()
    {
        SelectPlayer(-1);
    }

    public void NextPlayer()
    {
        SelectPlayer(1);
    }

    private void SelectPlayer(int direction)
    {
        if (!IsSpectating)
            return;

        RefreshPlayers();
        if (players.Count == 0)
            return;

        viewedIndex = (viewedIndex + direction + players.Count) % players.Count;
        FollowPlayer(players[viewedIndex]);
    }

    private void RefreshPlayers()
    {
        players.Clear();
        foreach (MP_Player player in FindObjectsByType<MP_Player>(FindObjectsSortMode.None))
        {
            if (player == null || !player.IsSpawned(false))
                continue;

            bool isAI = player.GetComponent<MP_EnemyAI>() != null;
            if ((isAI && includeAI) || (!isAI && player.owner.HasValue))
                players.Add(player);
        }

        players.Sort((left, right) => left.objectId.CompareTo(right.objectId));
        if (players.Count == 0)
        {
            viewedPlayer = null;
            viewedIndex = 0;
            cameraController.RemoveTarget();
            UpdateViewedPlayerText();
            return;
        }

        int currentIndex = players.IndexOf(viewedPlayer);
        viewedIndex = currentIndex >= 0 ? currentIndex : Mathf.Clamp(viewedIndex, 0, players.Count - 1);
        if (viewedPlayer != players[viewedIndex])
            FollowPlayer(players[viewedIndex]);
    }

    private void FollowPlayer(MP_Player player)
    {
        viewedPlayer = player;
        cameraController.SetTarget(player.transform);
        UpdateViewedPlayerText();
    }

    private void UpdateViewedPlayerText()
    {
        if (viewedPlayerText == null)
            return;

        string name = viewedPlayer != null && viewedPlayer.Stats != null
            ? viewedPlayer.Stats.PlayerName : "";
        string label = viewedPlayer == null ? "No clouds to spectate" :
            $"Spectating: {(string.IsNullOrWhiteSpace(name) ? "Unnamed cloud" : name)}";
        if (viewedPlayerText.text != label)
            viewedPlayerText.text = label;
    }

    public void ReturnToWaitingRoom()
    {
        if (!IsSpectating)
            return;

        IsSpectating = false;
        if (spectatorPanel != null)
            spectatorPanel.SetActive(false);
        players.Clear();
        viewedPlayer = null;
        if (cameraController == null)
            return;

        MP_Player localPlayer = MP_LocalPlayerManager.Instance != null
            ? MP_LocalPlayerManager.Instance.LocalPlayer : null;
        if (localPlayer == null)
        {
            cameraController.RemoveTarget();
            return;
        }

        MP_PlayerMovement movement = localPlayer.GetComponent<MP_PlayerMovement>();
        cameraController.SetTarget(movement != null ? movement.PresentationTarget : localPlayer.transform);
    }

    public void ReturnToLobby()
    {
        NetworkManager manager = NetworkManager.main;
        if (manager != null && manager.isClient)
            manager.StopClient();
        SceneManager.LoadScene(LobbySceneName, LoadSceneMode.Single);
    }
}
