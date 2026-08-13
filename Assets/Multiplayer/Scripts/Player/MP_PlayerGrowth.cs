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
    public int Level => level.value;
    public float Exp => experience.value;
    public float ExpCap => GetExperienceRequired();

    private readonly SyncVar<int> level = new(1);
    private readonly SyncVar<float> experience = new(0f);

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (playerStats == null)
        {
            Debug.LogError("Missing player stats reference", this);
            return;
        }

        level.onChanged += HandleLevelChanged;
        experience.onChanged += HandleExpChanged;

        HandleLevelChanged(level.value);
        HandleExpChanged(experience.value);
    }

    protected override void OnDespawned(bool asServer)
    {
        base.OnDespawned(asServer);

        level.onChanged -= HandleLevelChanged;
    }

    public void GainExperience(float amount)
    {
        if (!isServer)
            return;

        experience.value += amount;

        while (experience.value >= GetExperienceRequired())
        {
            experience.value -= GetExperienceRequired();
            level.value++;
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
            Mathf.Pow(expScaleRatio.Value, level.value - 1)
        );
    }

    private float GetScaleFactor()
    {
        return Mathf.Pow(sizeScaleRatio.Value, level.value - 1);
    }
}