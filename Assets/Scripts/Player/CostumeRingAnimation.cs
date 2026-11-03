using UnityEngine;

public static class CostumeRingAnimation
{
    public static Color WithBreathingAlpha(Color color, float time, PlayerCostume catalog)
    {
        if (catalog == null)
            return color;

        float phase = time * Mathf.Max(0f, catalog.RingBreathingSpeed) * Mathf.PI * 2f;
        float blend = 0.5f - 0.5f * Mathf.Cos(phase);
        float minimum = Mathf.Clamp01(catalog.RingAlphaMinimum);
        float maximum = Mathf.Clamp01(catalog.RingAlphaMaximum);
        color.a = Mathf.Lerp(Mathf.Min(minimum, maximum), Mathf.Max(minimum, maximum), blend);
        return color;
    }
}
