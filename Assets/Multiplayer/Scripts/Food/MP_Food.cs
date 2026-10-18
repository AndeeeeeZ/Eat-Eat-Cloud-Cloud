using PurrNet;
using UnityEngine;

public class MP_Food : NetworkBehaviour
{
    public const float GrowthDurationSeconds = 0.35f;

    [SerializeField] private float expAmount = 1f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;

    public float BaseExperience => expAmount;
    public float FadeInDuration => fadeInDuration;
}
