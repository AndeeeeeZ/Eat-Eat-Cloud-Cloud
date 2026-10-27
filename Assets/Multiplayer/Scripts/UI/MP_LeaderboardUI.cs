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
    private MP_Player displayedPlayer;
    private bool displayingSpectator;
    private readonly StringBuilder text = new();
    private readonly StringBuilder levelText = new();
    private TextMeshProUGUI levelColumnText;
    private Vector2 originalTextPosition;
    private Vector2 originalTextSize;
    private bool originalWordWrapping;

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
        originalTextPosition = topPlayersText.rectTransform.anchoredPosition;
        originalTextSize = topPlayersText.rectTransform.sizeDelta;
        originalWordWrapping = topPlayersText.enableWordWrapping;
        levelColumnText = Instantiate(topPlayersText, topPlayersText.transform.parent);
        levelColumnText.name = "Leaderboard Level";
        levelColumnText.alignment = TextAlignmentOptions.TopRight;
        levelColumnText.enableWordWrapping = false;
        levelColumnText.raycastTarget = false;
        topPlayersText.enableWordWrapping = false;
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
        if (levelColumnText != null)
        {
            Destroy(levelColumnText.gameObject);
            topPlayersText.rectTransform.anchoredPosition = originalTextPosition;
            topPlayersText.rectTransform.sizeDelta = originalTextSize;
            topPlayersText.enableWordWrapping = originalWordWrapping;
        }
        levelColumnText = null;
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
        if (leaderboard == null || topPlayersText == null || localPlayerRankText == null || levelColumnText == null)
            return;

        var entries = leaderboard.Entries;
        text.Clear();
        levelText.Clear();
        int topCount = Mathf.Min(Mathf.Max(1, topPlayersToShow), entries.Count);
        float levelWidth = 40f;
        for (int i = 0; i < topCount; i++)
            levelWidth = Mathf.Max(levelWidth,
                levelColumnText.GetPreferredValues($"Lvl. {entries[i].Level}").x + 6f);
        levelWidth = Mathf.Min(levelWidth, originalTextSize.x * 0.5f);
        float nameWidth = originalTextSize.x - levelWidth - 4f;
        RectTransform namesRect = topPlayersText.rectTransform;
        RectTransform levelRect = levelColumnText.rectTransform;
        namesRect.sizeDelta = new Vector2(nameWidth, originalTextSize.y);
        namesRect.anchoredPosition = originalTextPosition +
            Vector2.left * (levelWidth + 4f) * 0.5f;
        levelRect.sizeDelta = new Vector2(levelWidth, originalTextSize.y);
        levelRect.anchoredPosition = originalTextPosition +
            Vector2.right * (originalTextSize.x - levelWidth) * 0.5f;
        for (int i = 0; i < topCount; i++)
        {
            if (i > 0)
            {
                text.AppendLine();
                levelText.AppendLine();
            }
            string name = string.IsNullOrWhiteSpace(entries[i].PlayerName)
                ? "Unnamed"
                : entries[i].PlayerName.Replace('\r', ' ').Replace('\n', ' ');
            string rank = $"{i + 1}. ";
            if (topPlayersText.GetPreferredValues(rank + name).x > nameWidth)
            {
                while (name.Length > 0 &&
                       topPlayersText.GetPreferredValues(rank + name + "...").x > nameWidth)
                    name = name.Substring(0, name.Length - 1);
                name += "...";
            }
            text.Append(rank).Append(name);
            levelText.Append("Lvl. ").Append(entries[i].Level);
        }
        topPlayersText.text = topCount > 0 ? text.ToString() : "No players yet";
        levelColumnText.text = levelText.ToString();

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

            localPlayerRankText.text = $"{prefix}: #{i + 1} / {entries.Count}  Lvl. {entries[i].Level}";
            break;
        }
    }
}
