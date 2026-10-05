using PurrNet;
using UnityEngine;

public class MP_PlayerStats : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MP_PlayerGrowth playerGrowth;
    [SerializeField] private MP_PlayerUI playerUI;

    private SyncVar<string> playerName = new("PlayerName");

    public string PlayerName => playerName.value;
    public int Level => playerGrowth.Level;
    public float Exp => playerGrowth.Exp;
    public float TotalExp => playerGrowth.TotalExp;

    [ServerRpc]
    public void SetPlayerName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            playerName.value = "MissingName";
            return;
        }

        newName = newName.Trim();

        if (newName.Length > 16)
            newName = newName.Substring(0, 16);

        playerName.value = newName;
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        playerName.onChanged += HandlePlayerNameChanged;

        if (asServer)
        {
            if (MP_PlayerManager.Instance == null)
            {
                Debug.LogError("MP_PlayerManager doesn't exist when player spawned", this);
                return;
            }
            return;
        }

        if (isOwner)
        {
            if (MP_LocalPlayerManager.Instance == null)
            {
                Debug.LogError("MP_LocalPlayerManager doesn't exist when player spawned", this);
                return;
            }
        }
    }

    private void HandlePlayerNameChanged(string newName)
    {
        playerUI.UpdateNameUI();
    }

}