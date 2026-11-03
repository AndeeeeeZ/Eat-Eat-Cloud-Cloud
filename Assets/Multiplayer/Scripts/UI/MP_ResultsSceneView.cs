using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MP_ResultsSceneView : MonoBehaviour
{
    [SerializeField] private PlayerCostume costumeCatalog;
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private MP_ResultsPodiumSlot[] podiumSlots = new MP_ResultsPodiumSlot[3];
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private void Start()
    {
        if (!MP_ResultsSceneData.HasResults)
        {
            Debug.LogWarning("No round results were received before loading this scene", this);
            return;
        }

        if (winnerNameText != null)
        {
            winnerNameText.richText = false;
            winnerNameText.text = string.IsNullOrWhiteSpace(MP_ResultsSceneData.WinnerName)
                ? "No winner" : MP_ResultsSceneData.WinnerName;
        }

        if (podiumSlots == null)
            return;

        var topPlayers = MP_ResultsSceneData.TopPlayers;
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
}
