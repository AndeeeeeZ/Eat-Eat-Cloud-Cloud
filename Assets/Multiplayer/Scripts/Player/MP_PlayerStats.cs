using System;
using PurrNet;
using UnityEngine;

public class MP_PlayerStats : NetworkBehaviour
{
    [SerializeField] private FloatValue baseExpGap;
    [SerializeField] private FloatValue expScaleRatio; 
    [SerializeField] private FloatValue sizeScaleRatio; 

    public event Action<float> OnScaleChanged; 
    private readonly SyncVar<int> level = new(1); 

    public float Scale => GetScaleFactor(); 
    private float experience = 0f;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        level.onChanged += HandleLevelChanged; 
        
        HandleLevelChanged(level.value); 
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
        
        // Debug.Log($"Gained {amount} exp", this); 

        experience += amount;

        while (experience >= GetExperienceRequired())
        {
            experience -= GetExperienceRequired();
            level.value++; 
        }
    }

    // Only the server changes the value directly
    // The clients get it from Network Transform but also invoke event to objects local to the client (ex. camera controller)
    private void HandleLevelChanged(int newLevel)
    {
        float scaleFactor = GetScaleFactor(); 

        if (isServer)
            transform.localScale = Vector3.one * scaleFactor;

        OnScaleChanged?.Invoke(scaleFactor);
    }

    private float GetExperienceRequired()
    {
        return baseExpGap.Value * Mathf.Pow(expScaleRatio.Value, level.value - 1);
    }

    private float GetScaleFactor()
    {
        return Mathf.Pow(sizeScaleRatio.Value, level.value - 1);
    }
}