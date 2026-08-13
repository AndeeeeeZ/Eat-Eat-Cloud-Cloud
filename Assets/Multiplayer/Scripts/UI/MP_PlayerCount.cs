using TMPro;
using UnityEngine;

public class MP_PlayerCount : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI numberText;

    private void Start()
    {
        if (MP_PlayerManager.Instance == null)
        {
            Debug.LogError("MP_PlayerManager doesn't exist", this);
            return;
        }

        MP_PlayerManager.Instance.OnPlayerCountChanged += UpdateUI;
        UpdateUI(MP_PlayerManager.Instance.PlayerCount);
    }

    private void OnDestroy()
    {
        if (MP_PlayerManager.Instance != null)
            MP_PlayerManager.Instance.OnPlayerCountChanged -= UpdateUI;
    }

    private void UpdateUI(int playerCount)
    {
        numberText.text = playerCount.ToString();
    }
}
