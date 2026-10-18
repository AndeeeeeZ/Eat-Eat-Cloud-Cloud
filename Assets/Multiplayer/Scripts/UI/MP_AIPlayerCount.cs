using TMPro;
using UnityEngine;

public class MP_AIPlayerCount : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI aiPlayerText;

    private MP_PlayerManager playerManager;

    private void Start()
    {
        playerManager = MP_PlayerManager.Instance;
        if (playerManager == null || aiPlayerText == null)
        {
            Debug.LogError("Assign an AI player count text and add MP_PlayerManager to the scene", this);
            return;
        }

        playerManager.OnAIPlayerCountChanged += UpdateUI;
        UpdateUI(playerManager.AIPlayerCount);
    }

    private void OnDestroy()
    {
        if (playerManager != null)
            playerManager.OnAIPlayerCountChanged -= UpdateUI;
    }

    private void UpdateUI(int count)
    {
        aiPlayerText.text = $"{count} AI Player{(count == 1 ? "" : "s")}";
    }
}
