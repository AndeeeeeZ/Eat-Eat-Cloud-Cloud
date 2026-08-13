using PurrNet;
using UnityEngine;

public class MP_PlayerStats : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MP_PlayerGrowth playerGrowth; 
    public string PlayerName { get; private set; }
    public int Level => playerGrowth.Level; 
    public float Exp => playerGrowth.Exp; 
}