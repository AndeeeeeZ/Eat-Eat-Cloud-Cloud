using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MP_ExpBar : MonoBehaviour
{
    [SerializeField] private Image bar;
    [SerializeField] private TextMeshProUGUI barText;
    private MP_PlayerGrowth playerGrowth;
    private MP_LocalPlayerManager localPlayerManager;
    private void OnEnable()
    {
        localPlayerManager = MP_LocalPlayerManager.Instance;
        ClearPlayer();
        if (localPlayerManager == null)
            return;

        localPlayerManager.OnLocalPlayerReady += HandleLocalPlayerReady;
        localPlayerManager.OnLocalPlayerLost += ClearPlayer;

        // In case player spawned before this object subscribe to the event
        if (localPlayerManager.LocalPlayer != null)
            HandleLocalPlayerReady(localPlayerManager.LocalPlayer);
    }

    private void OnDisable()
    {
        if (localPlayerManager != null)
        {
            localPlayerManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
            localPlayerManager.OnLocalPlayerLost -= ClearPlayer;
        }
        ClearPlayer();
    }

    private void HandleLocalPlayerReady(MP_Player player)
    {
        ClearPlayer();
        playerGrowth = player.GetComponent<MP_PlayerGrowth>();
        if (playerGrowth != null)
        {
            playerGrowth.OnExpChanged += UpdateUI;
            UpdateUI();
        }
    }

    private void ClearPlayer()
    {
        if (!ReferenceEquals(playerGrowth, null))
            playerGrowth.OnExpChanged -= UpdateUI;
        playerGrowth = null;
        if (bar != null)
            bar.fillAmount = 0f;
        if (barText != null)
            barText.text = "";
    }

    private void UpdateUI()
    {
        if (playerGrowth == null)
            return;

        float exp = playerGrowth.Exp;
        float expCap = playerGrowth.ExpCap;

        float percentage = Mathf.Clamp01(exp / expCap);
        
        // NOTE: the exp and exp cap display are rounded to integers
        barText.text = $"{Mathf.Round(exp)}/{Mathf.Round(expCap)}";
        bar.fillAmount = percentage;
    }
}
