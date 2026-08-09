using PurrNet;
using UnityEngine;

public class MP_PlayerStats : NetworkBehaviour
{
    [SerializeField] private FloatValue baseExpGap;
    [SerializeField] private FloatValue expScaleRatio; 
    [SerializeField] private FloatValue sizeScaleRatio; 
    
    private int level = 1;
    private float experience = 0f;
    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (!asServer)
            return; 
        
        UpdateScale(); 
    }

    public void GainExperience(float amount)
    {
        if (!isServer)
            return;
        
        Debug.Log($"Gained {amount} exp", this); 

        experience += amount;

        while (experience >= GetExperienceRequired())
        {
            experience -= GetExperienceRequired();
            LevelUp();
        }
    }

    private void LevelUp()
    {
        level++;

        UpdateScale();
    }

    private void UpdateScale()
    {
        transform.localScale = Vector3.one * GetScaleFactor();
    }

    private float GetExperienceRequired()
    {
        return baseExpGap.Value * Mathf.Pow(expScaleRatio.Value, level - 1);
    }

    private float GetScaleFactor()
    {
        return Mathf.Pow(sizeScaleRatio.Value, level - 1);
    }
}