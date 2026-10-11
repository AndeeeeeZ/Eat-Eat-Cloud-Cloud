using PurrNet;
using UnityEngine;

public class MP_PlayerEating : NetworkBehaviour
{
    private MP_Player player;
    private void Awake()
    {
        player = GetComponentInParent<MP_Player>();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryEat(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Recheck overlaps when protection expires or a player's level changes
        TryEat(other);
    }

    private void TryEat(Collider2D other)
    {
        if (!isServer || player == null || !player.IsAlive)
            return;

        MP_Player victim = other.GetComponentInParent<MP_Player>();
        if (victim != null)
            victim.TryDieFromEating(player);
    }
}
