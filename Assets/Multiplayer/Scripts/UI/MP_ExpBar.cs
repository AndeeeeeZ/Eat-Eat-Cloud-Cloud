using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MP_ExpBar : MonoBehaviour
{
    [SerializeField] private Image bar;
    [SerializeField] private TextMeshProUGUI barText;
    private MP_PlayerGrowth playerGrowth;
    private MP_Player displayedPlayer;

    private void OnEnable()
    {
        ClearPlayer();
        RefreshDisplayedPlayer();
    }

    private void Update()
    {
        RefreshDisplayedPlayer();
    }

    private void OnDisable()
    {
        ClearPlayer();
    }

    private void RefreshDisplayedPlayer()
    {
        MP_SpectatorMode spectator = MP_SpectatorMode.Instance;
        MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
        MP_Player player = spectator != null && spectator.IsSpectating
            ? spectator.ViewedPlayer : local != null ? local.LocalPlayer : null;
        if (ReferenceEquals(displayedPlayer, player))
            return;

        ClearPlayer();
        displayedPlayer = player;
        if (player == null)
            return;

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
        displayedPlayer = null;
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
