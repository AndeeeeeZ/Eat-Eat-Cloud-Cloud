using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class MP_LeaderboardUI : MonoBehaviour
{
    [SerializeField] private MP_Leaderboard leaderboard;
    [SerializeField] private TextMeshProUGUI topPlayersText;
    [SerializeField] private TextMeshProUGUI localPlayerRankText;
    [SerializeField, Min(1)] private int topPlayersToShow = 5;
    [FormerlySerializedAs("localPlayerNameColor")]
    [SerializeField] private Color32 localPlayerHighlightColor = new(255, 220, 80, 255);

    private MP_LocalPlayerManager localPlayerManager;
    private MP_Player displayedPlayer;
    private bool displayingSpectator;
    private readonly StringBuilder text = new();
    private TextWrappingModes originalTextWrappingMode;
    private TextOverflowModes originalOverflowMode;
    private bool textConfigured;
    private int localRowStart = -1;
    private int localRowLength;

    public void SetTopPlayersToShow(int count)
    {
        int clampedCount = Mathf.Max(1, count);
        if (topPlayersToShow == clampedCount)
            return;

        topPlayersToShow = clampedCount;
        if (isActiveAndEnabled)
            Refresh();
    }

    private void OnEnable()
    {
        if (leaderboard == null || topPlayersText == null || localPlayerRankText == null)
        {
            Debug.LogError("Assign the leaderboard and both text fields on MP_LeaderboardUI.", this);
            return;
        }

        // Names are plain text and cannot inject TMP formatting
        topPlayersText.richText = false;
        localPlayerRankText.richText = false;
        originalTextWrappingMode = topPlayersText.textWrappingMode;
        originalOverflowMode = topPlayersText.overflowMode;
        topPlayersText.textWrappingMode = TextWrappingModes.NoWrap;
        topPlayersText.overflowMode = TextOverflowModes.Overflow;
        topPlayersText.OnPreRenderText += ColorLocalPlayerRow;
        textConfigured = true;
        leaderboard.OnLeaderboardChanged += Refresh;

        localPlayerManager = MP_LocalPlayerManager.Instance;
        if (localPlayerManager != null)
        {
            localPlayerManager.OnLocalPlayerReady += HandleLocalPlayerReady;
            localPlayerManager.OnLocalPlayerLost += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (textConfigured && topPlayersText != null)
        {
            topPlayersText.textWrappingMode = originalTextWrappingMode;
            topPlayersText.overflowMode = originalOverflowMode;
            topPlayersText.OnPreRenderText -= ColorLocalPlayerRow;
        }
        textConfigured = false;
        if (leaderboard != null)
            leaderboard.OnLeaderboardChanged -= Refresh;
        if (localPlayerManager != null)
        {
            localPlayerManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
            localPlayerManager.OnLocalPlayerLost -= Refresh;
        }
    }

    private void Update()
    {
        MP_SpectatorMode spectator = MP_SpectatorMode.Instance;
        bool spectating = spectator != null && spectator.IsSpectating;
        MP_Player player = spectating ? spectator.ViewedPlayer :
            localPlayerManager != null ? localPlayerManager.LocalPlayer : null;
        if (displayedPlayer != player || displayingSpectator != spectating)
            Refresh();
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (leaderboard == null || topPlayersText == null || localPlayerRankText == null)
            return;

        var entries = leaderboard.Entries;
        text.Clear();
        int topCount = Mathf.Min(Mathf.Max(1, topPlayersToShow), entries.Count);
        int rankWidth = Mathf.Max(1, topPlayersToShow).ToString().Length;
        MP_Player localPlayer = localPlayerManager != null ? localPlayerManager.LocalPlayer : null;
        localRowStart = -1;
        localRowLength = 0;
        for (int i = 0; i < topCount; i++)
        {
            if (i > 0)
                text.AppendLine();
            string name = string.IsNullOrWhiteSpace(entries[i].PlayerName)
                ? "Unnamed"
                : entries[i].PlayerName.Replace('\r', ' ').Replace('\n', ' ');
            int rowStart = text.Length;
            text.Append((i + 1).ToString().PadLeft(rankWidth)).Append(". ")
                .Append(name.PadRight(MP_PlayerNameRules.MaxLength))
                .Append("  Lvl. ").Append(entries[i].Level.ToString("D2"));
            if (localPlayer != null && entries[i].ObjectId == localPlayer.objectId)
            {
                localRowStart = rowStart;
                localRowLength = text.Length - rowStart;
            }
        }
        topPlayersText.text = topCount > 0 ? text.ToString() : "No players yet";
        topPlayersText.ForceMeshUpdate();

        MP_SpectatorMode spectator = MP_SpectatorMode.Instance;
        displayingSpectator = spectator != null && spectator.IsSpectating;
        displayedPlayer = displayingSpectator ? spectator.ViewedPlayer :
            localPlayerManager != null ? localPlayerManager.LocalPlayer : null;
        string prefix = displayingSpectator ? "Viewing" : "You";
        localPlayerRankText.text = $"{prefix}: Not ranked";
        if (displayedPlayer == null)
            return;

        // Search the complete snapshot so a cloud below the top few still sees its rank
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].ObjectId != displayedPlayer.objectId)
                continue;

            localPlayerRankText.text = $"{prefix}: #{i + 1} / {entries.Count} Lvl. {entries[i].Level:D2}";
            break;
        }
    }

    private void ColorLocalPlayerRow(TMP_TextInfo textInfo)
    {
        if (localRowStart < 0)
            return;

        int end = localRowStart + localRowLength;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            if (!character.isVisible || character.index < localRowStart || character.index >= end)
                continue;

            Color32[] colors = textInfo.meshInfo[character.materialReferenceIndex].colors32;
            for (int vertex = 0; vertex < 4; vertex++)
                colors[character.vertexIndex + vertex] = localPlayerHighlightColor;
        }
    }
}
