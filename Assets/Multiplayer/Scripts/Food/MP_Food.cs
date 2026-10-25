using PurrNet;
using UnityEngine;

public class MP_Food : NetworkBehaviour
{
    public const float GrowthDurationSeconds = 0.35f;

    [SerializeField] private float expAmount = 1f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;

    [Header("Appearance")]
    [SerializeField] private Color[] colorOptions =
    {
        new(1f, 0.65f, 0.75f),
        new(0.65f, 0.85f, 1f),
        new(1f, 0.9f, 0.6f),
        new(0.7f, 1f, 0.75f),
        new(0.85f, 0.7f, 1f)
    };
    [SerializeField] private Sprite[] shapeOptions = new Sprite[0];

    public float BaseExperience => expAmount;
    public float FadeInDuration => fadeInDuration;
    public Sprite[] ShapeOptions => shapeOptions;

    public Color GetColor(uint foodId, Color fallback)
    {
        if (colorOptions == null || colorOptions.Length == 0)
            return fallback;

        Color color = colorOptions[GetOptionIndex(foodId, 0x9e3779b9u, colorOptions.Length)];
        color.a *= fallback.a;
        return color;
    }

    public Sprite GetShape(uint foodId, Sprite fallback)
    {
        if (shapeOptions == null || shapeOptions.Length == 0)
            return fallback;

        Sprite shape = shapeOptions[GetOptionIndex(foodId, 0x85ebca6bu, shapeOptions.Length)];
        return shape != null ? shape : fallback;
    }

    private static int GetOptionIndex(uint foodId, uint salt, int count)
    {
        unchecked
        {
            uint hash = foodId ^ salt;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
            return (int)(hash % (uint)count);
        }
    }
}
