using System.Collections.Generic;
using UnityEngine;

public static class MP_SpawnSafety 
{
    public readonly struct Space
    {
        public readonly Vector2 Center;
        public readonly float Radius;

        public Space(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    public static bool IsClear(Vector2 position, float radius, float clearance, IReadOnlyList<Space> occupied)
    {
        foreach (Space space in occupied)
        {
            float separation = radius + space.Radius + clearance;
            if ((position - space.Center).sqrMagnitude <= separation * separation)
                return false;
        }
        return true;
    }

}
