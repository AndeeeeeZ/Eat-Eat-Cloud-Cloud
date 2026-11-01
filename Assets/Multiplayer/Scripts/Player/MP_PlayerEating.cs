using PurrNet;
using UnityEngine;

public class MP_PlayerEating : NetworkBehaviour
{
    private MP_Player player;
    private CircleCollider2D eaterCollider;
    private void Awake()
    {
        player = GetComponentInParent<MP_Player>();
        eaterCollider = GetComponent<CircleCollider2D>();
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

    public void TryEatAlongPath(Vector2 from, Vector2 to)
    {
        if (!isServer || player == null || !player.IsAlive ||
            player.IsSpawnProtected || eaterCollider == null ||
            !eaterCollider.enabled || (to - from).sqrMagnitude <= 0f)
            return;

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager == null || manager.RoundEnded || manager.Players.Count <= 1)
            return;

        Vector3 eaterScale = eaterCollider.transform.lossyScale;
        float eaterRadius = eaterCollider.radius *
            Mathf.Max(Mathf.Abs(eaterScale.x), Mathf.Abs(eaterScale.y));
        Vector2 centerOffset = (Vector2)eaterCollider.transform.TransformPoint(
            eaterCollider.offset) - (Vector2)player.transform.position;
        Vector2 start = from + centerOffset;
        Vector2 movement = to - from;
        float movementLengthSquared = movement.sqrMagnitude;

        for (int i = manager.Players.Count - 1; i >= 0; i--)
        {
            MP_PlayerStats stats = manager.Players[i];
            if (stats == null || !stats.TryGetComponent(out MP_Player victim) ||
                victim == player || !victim.IsAlive || victim.IsSpawnProtected ||
                stats.Level >= player.Stats.Level)
                continue;

            CircleCollider2D victimCollider = victim.GetComponent<CircleCollider2D>();
            if (victimCollider == null || !victimCollider.enabled)
                continue;

            Vector3 victimScale = victimCollider.transform.lossyScale;
            float victimRadius = victimCollider.radius *
                Mathf.Max(Mathf.Abs(victimScale.x), Mathf.Abs(victimScale.y));
            Vector2 victimCenter = victimCollider.transform.TransformPoint(victimCollider.offset);
            float progress = movementLengthSquared > 0f
                ? Mathf.Clamp01(Vector2.Dot(victimCenter - start, movement) /
                    movementLengthSquared) : 0f;
            Vector2 nearest = start + movement * progress;
            float reach = eaterRadius + victimRadius;
            if ((victimCenter - nearest).sqrMagnitude <= reach * reach)
                victim.TryDieFromEating(player);
        }
    }
}
