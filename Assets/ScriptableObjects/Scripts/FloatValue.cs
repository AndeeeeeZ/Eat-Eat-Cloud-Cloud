using UnityEngine;

// Has editor script FloatValueDrawer.cs

[CreateAssetMenu(menuName = "Values/FloatValue")]
public class FloatValue : ScriptableObject
{
    [SerializeField] private float value;

    public float Value => value;
}
