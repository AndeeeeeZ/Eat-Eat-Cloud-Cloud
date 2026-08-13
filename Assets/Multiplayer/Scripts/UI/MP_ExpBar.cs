using System.Globalization;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI; 

public class MP_ExpBar : MonoBehaviour
{
    [SerializeField] private Image bar; 
    [SerializeField] private TextMeshProUGUI barText; 
    private MP_PlayerGrowth playerGrowth; 
    private void OnEnable()
    {
        MP_LocalPlayerManager manager = MP_LocalPlayerManager.Instance;
        manager.OnLocalPlayerReady += HandleLocalPlayerReady;

        // In case player spawned before this object subscribe to the event
        if (manager.LocalPlayer != null)
            HandleLocalPlayerReady(manager.LocalPlayer);
    }

    private void OnDisable()
    {
        MP_LocalPlayerManager.Instance.OnLocalPlayerReady -= HandleLocalPlayerReady;

        if (playerGrowth != null)
        {
            playerGrowth.OnExpChanged -= UpdateUI; 
        }
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        playerGrowth = player.GetComponent<MP_PlayerGrowth>(); 
        playerGrowth.OnExpChanged += UpdateUI;  
    }

    private void UpdateUI()
    {
        float exp = playerGrowth.Exp; 
        float expCap = playerGrowth.ExpCap; 
        float percentage = Mathf.Clamp01(exp / expCap); 
        barText.text = $"{exp}/{expCap}"; 
        bar.fillAmount = percentage; 
    }
}
