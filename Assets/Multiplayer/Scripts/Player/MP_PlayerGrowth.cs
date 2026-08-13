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
    public event Action OnExpChanged;

    public float Scale => GetScaleFactor();
    public float Exp => experience.value;
    public float ExpCap => GetExperienceRequired();

    public readonly SyncVar<float> experience = new(0f);

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (playerStats == null)
        {
            Debug.LogError("Missing player stats reference", this);
            return;
        }

        playerStats.Level.onChanged += HandleLevelChanged;
        experience.onChanged += HandleExpChanged;

        HandleLevelChanged(playerStats.Level.value);
        HandleExpChanged(experience.value);
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

        experience.value += amount;

        while (experience.value >= GetExperienceRequired())
        {
            experience.value -= GetExperienceRequired();
            playerStats.Level.value++;
        }
    }

    private void HandleLevelChanged(int newLevel)
    {
        float scale = GetScaleFactor();

        if (isServer)
            transform.localScale = Vector3.one * scale;

        OnScaleChanged?.Invoke(scale);
        OnExpChanged?.Invoke(); 
    }

    private void HandleExpChanged(float newExp)
    {
        OnExpChanged?.Invoke();
    }

    private float GetExperienceRequired()
    {
        return Mathf.CeilToInt(
            baseExpGap.Value *
            Mathf.Pow(expScaleRatio.Value, playerStats.Level.value - 1)
        );
    }

    private float GetScaleFactor()
    {
        return Mathf.Pow(sizeScaleRatio.Value, playerStats.Level.value - 1);
    }
}