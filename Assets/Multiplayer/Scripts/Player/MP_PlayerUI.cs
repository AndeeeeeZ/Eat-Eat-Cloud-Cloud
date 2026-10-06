using UnityEngine;
using TMPro;

public class MP_PlayerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private MP_PlayerStats playerStats;

    private void OnEnable()
    {
        playerStats.OnPlayerNameChanged += UpdateNameUI; 
        UpdateNameUI(); 
    }

    private void OnDisable()
    {
        playerStats.OnPlayerNameChanged -= UpdateNameUI; 
    }

    public void UpdateNameUI()
    {
        if (playerStats.PlayerName != "")
            nameText.text = playerStats.PlayerName;
        else
            nameText.text = "MissingName"; 
    }
}