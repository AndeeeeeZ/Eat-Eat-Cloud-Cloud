using PurrNet;
using UnityEngine;

public class MP_PlayerEating : NetworkBehaviour
{
    private MP_PlayerStats playerStats;
    private MP_PlayerGrowth playerGrowth;
    private void Awake()
    {
        playerStats = GetComponentInParent<MP_PlayerStats>();
        playerGrowth = GetComponentInParent<MP_PlayerGrowth>();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isServer)
            return;

        MP_PlayerStats otherStats = other.GetComponentInParent<MP_PlayerStats>();

        if (otherStats == null)
        {
            Debug.LogWarning($"Unable to find MP_PlayerStats in {other.transform.parent.name}'s parent", this);
            return;
        }

        if (otherStats.Level < playerStats.Level)
        {
            EatPlayer(otherStats);
        }
    }

    private void EatPlayer(MP_PlayerStats other)
    {
        playerGrowth.GainExperienceFromEating(other.TotalExp);

        Debug.Log($"{playerStats.name} ate {other.name}", this);

        Destroy(other.gameObject);
    }
}
