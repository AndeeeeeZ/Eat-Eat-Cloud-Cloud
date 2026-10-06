using System;
using PurrNet;
using UnityEngine;

public class MP_PlayerStats : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MP_PlayerGrowth playerGrowth;

    private SyncVar<string> playerName = new("NEW NAME");
    public event Action OnPlayerNameChanged; 

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
        HandlePlayerNameChanged(null); 
    }

    private void HandlePlayerNameChanged(string newName)
    {
        OnPlayerNameChanged?.Invoke(); 
    }

}