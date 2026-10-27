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
    private readonly StringBuilder xpText = new();
    private TextMeshProUGUI xpColumnText;
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
        xpColumnText = Instantiate(topPlayersText, topPlayersText.transform.parent);
        xpColumnText.name = "Leaderboard XP";
        xpColumnText.alignment = TextAlignmentOptions.TopRight;
        xpColumnText.enableWordWrapping = false;
        xpColumnText.raycastTarget = false;
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
        if (xpColumnText != null)
        {
            Destroy(xpColumnText.gameObject);
            topPlayersText.rectTransform.anchoredPosition = originalTextPosition;
            topPlayersText.rectTransform.sizeDelta = originalTextSize;
            topPlayersText.enableWordWrapping = originalWordWrapping;
        }
        xpColumnText = null;
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
        if (leaderboard == null || topPlayersText == null || localPlayerRankText == null || xpColumnText == null)
            return;

        var entries = leaderboard.Entries;
        text.Clear();
        xpText.Clear();
        int topCount = Mathf.Min(Mathf.Max(1, topPlayersToShow), entries.Count);
        float xpWidth = 40f;
        for (int i = 0; i < topCount; i++)
            xpWidth = Mathf.Max(xpWidth,
                xpColumnText.GetPreferredValues($"{entries[i].TotalExp:0} XP").x + 6f);
        xpWidth = Mathf.Min(xpWidth, originalTextSize.x * 0.5f);
        float nameWidth = originalTextSize.x - xpWidth - 4f;
        RectTransform namesRect = topPlayersText.rectTransform;
        RectTransform xpRect = xpColumnText.rectTransform;
        namesRect.sizeDelta = new Vector2(nameWidth, originalTextSize.y);
        namesRect.anchoredPosition = originalTextPosition +
            Vector2.left * (xpWidth + 4f) * 0.5f;
        xpRect.sizeDelta = new Vector2(xpWidth, originalTextSize.y);
        xpRect.anchoredPosition = originalTextPosition +
            Vector2.right * (originalTextSize.x - xpWidth) * 0.5f;
        for (int i = 0; i < topCount; i++)
        {
            if (i > 0)
            {
                text.AppendLine();
                xpText.AppendLine();
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
            xpText.Append(entries[i].TotalExp.ToString("0")).Append(" XP");
        }
        topPlayersText.text = topCount > 0 ? text.ToString() : "No players yet";
        xpColumnText.text = xpText.ToString();

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

            localPlayerRankText.text = $"{prefix}: #{i + 1} / {entries.Count}  {entries[i].TotalExp:0} XP";
            break;
        }
    }
}
