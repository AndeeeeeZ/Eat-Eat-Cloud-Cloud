using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MP_ResultsSceneView : MonoBehaviour
{
    [SerializeField] private PlayerCostume costumeCatalog;
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private MP_ResultsPodiumSlot[] podiumSlots = new MP_ResultsPodiumSlot[3];
    [SerializeField] private string lobbySceneName = "LobbyScene";
    private const string MultiplayerSceneName = "MultiplayerScene";

    private void Start()
    {
        if (podiumSlots != null)
        {
            foreach (MP_ResultsPodiumSlot slot in podiumSlots)
            {
                if (slot != null)
                    slot.SetCatalog(costumeCatalog);
            }
        }

        if (!MP_ResultsSceneData.HasResults)
        {
            Debug.LogWarning("No round results were received before loading this scene", this);
            return;
        }

        var topPlayers = MP_ResultsSceneData.TopPlayers;
        if (winnerNameText != null)
        {
            winnerNameText.richText = false;
            int localPlace = 0;
            for (int i = 0; i < topPlayers.Count && i < 3; i++)
            {
                if (!topPlayers[i].IsLocal)
                    continue;
                localPlace = i + 1;
                break;
            }

            winnerNameText.text = localPlace switch
            {
                1 => "You are the 1st place",
                2 => "You are the 2nd place",
                3 => "You are the 3rd place",
                _ => string.IsNullOrWhiteSpace(MP_ResultsSceneData.WinnerName)
                    ? "No winner"
                    : $"{MP_ResultsSceneData.WinnerName} is the winner"
            };
        }

        if (podiumSlots == null)
            return;

        for (int i = 0; i < podiumSlots.Length; i++)
        {
            MP_ResultsPodiumSlot slot = podiumSlots[i];
            if (slot == null)
                continue;
            bool filled = i < topPlayers.Count;
            slot.gameObject.SetActive(filled);
            if (!filled)
                continue;

            slot.Apply(topPlayers[i], costumeCatalog);
        }
    }

    public void ReturnToLobby()
    {
        MP_ResultsSceneData.Clear();
        SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    public void JoinNextRound()
    {
        if (!Application.CanStreamedLevelBeLoaded(MultiplayerSceneName))
        {
            Debug.LogError($"Add '{MultiplayerSceneName}' to the build scenes", this);
            return;
        }

        MP_ResultsSceneData.Clear();
        SceneManager.LoadScene(MultiplayerSceneName, LoadSceneMode.Single);
    }
}
