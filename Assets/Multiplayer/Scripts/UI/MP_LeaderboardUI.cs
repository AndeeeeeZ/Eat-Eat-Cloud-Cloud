using System.Text;
using TMPro;
using UnityEngine;

public class MP_LeaderboardUI : MonoBehaviour
{
    [SerializeField] private MP_Leaderboard leaderboard;
    [SerializeField] private TextMeshProUGUI topPlayersText;
    [SerializeField] private TextMeshProUGUI localPlayerRankText;
    [SerializeField, Min(1)] private int topPlayersToShow = 5;

    private MP_LocalPlayerManager localPlayerManager;
    private readonly StringBuilder text = new();

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
        if (leaderboard != null)
            leaderboard.OnLeaderboardChanged -= Refresh;
        if (localPlayerManager != null)
        {
            localPlayerManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
            localPlayerManager.OnLocalPlayerLost -= Refresh;
        }
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        Refresh();
    }

    private void Refresh()
    {
        var entries = leaderboard.Entries;
        text.Clear();
        int topCount = Mathf.Min(Mathf.Max(1, topPlayersToShow), entries.Count);
        for (int i = 0; i < topCount; i++)
        {
            if (i > 0)
                text.AppendLine();
            string name = string.IsNullOrWhiteSpace(entries[i].PlayerName)
                ? "Unnamed"
                : entries[i].PlayerName.Replace('\r', ' ').Replace('\n', ' ');
            text.Append(i + 1).Append(". ").Append(name)
                .Append("  ").Append(entries[i].TotalExp.ToString("0")).Append(" XP");
        }
        topPlayersText.text = topCount > 0 ? text.ToString() : "No players yet";

        MP_Player localPlayer = localPlayerManager != null ? localPlayerManager.LocalPlayer : null;
        localPlayerRankText.text = "You: Not ranked";
        if (localPlayer == null || !localPlayer.owner.HasValue)
            return;

        // Search the complete snapshot so a player below the top few still sees their rank
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsAI || entries[i].PlayerId != localPlayer.owner.Value)
                continue;

            localPlayerRankText.text = $"You: #{i + 1} / {entries.Count}  {entries[i].TotalExp:0} XP";
            break;
        }
    }
}
