using System;
using PurrNet;
using UnityEngine;

public class MP_PlayerGrowth : NetworkBehaviour
{
    [SerializeField] private MP_PlayerStats playerStats;

    [SerializeField] private FloatValue baseExpGap;
    [SerializeField] private FloatValue expScaleRatio;
    [SerializeField] private FloatValue sizeScaleRatio;

    public event Action<float> OnScaleChanged;

    public float Scale => GetScaleFactor();     

    private float experience;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        playerStats.Level.onChanged += HandleLevelChanged;

        HandleLevelChanged(playerStats.Level.value);
    }

    protected override void OnDespawned(bool asServer)
    {
        base.OnDespawned(asServer);

        playerStats.Level.onChanged -= HandleLevelChanged;
    }

    public void GainExperience(float amount)
    {
        if (!isServer)
            return;

        experience += amount;

        while (experience >= GetExperienceRequired())
        {
            experience -= GetExperienceRequired();
            playerStats.Level.value++;
        }
    }

    private void HandleLevelChanged(int newLevel)
    {
        float scale = GetScaleFactor();

        if (isServer)
            transform.localScale = Vector3.one * scale;

        OnScaleChanged?.Invoke(scale);
    }

    private float GetExperienceRequired()
    {
        return baseExpGap.Value *
               Mathf.Pow(expScaleRatio.Value, playerStats.Level.value - 1);
    }

    private float GetScaleFactor()
    {
        return Mathf.Pow(sizeScaleRatio.Value, playerStats.Level.value - 1);
    }
}