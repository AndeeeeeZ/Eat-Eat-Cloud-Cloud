using PurrNet;
using UnityEngine;

public class MP_PlayerStats : NetworkBehaviour
{
    public readonly SyncVar<int> Level = new(1);
}