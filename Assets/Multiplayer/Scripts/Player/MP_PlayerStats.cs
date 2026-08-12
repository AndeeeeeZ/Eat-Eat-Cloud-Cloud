using PurrNet;
using UnityEngine;

public class MP_PlayerStats : NetworkBehaviour
{
    public string PlayerName { get; private set; }
    public readonly SyncVar<int> Level = new(1);
}