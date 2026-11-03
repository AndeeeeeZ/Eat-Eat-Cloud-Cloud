using UnityEngine;

[CreateAssetMenu(menuName = "PlayerCostume")]
public class PlayerCostume : ScriptableObject
{
    // index 0 = none for optional sprite arrays
    public int cloudBaseIndex, bottomIndex, rightIndex, leftIndex, faceIndex;
    public Sprite[] cloudBases, bottoms, rightSides, leftSides, faces;
    public int baseColorIndex, ringColorIndex;
    public Color[] baseColors, ringColors;
    public Sprite ringSprite;

    [Header("Ring Animation")]
    [SerializeField] private FloatValue ringSpinSpeed;
    [SerializeField] private FloatValue ringBreathingSpeed;
    [SerializeField] private FloatValue ringAlphaMinimum;
    [SerializeField] private FloatValue ringAlphaMaximum;

    public float RingSpinSpeed => ringSpinSpeed != null ? ringSpinSpeed.Value : 15f;
    public float RingBreathingSpeed => ringBreathingSpeed != null ? ringBreathingSpeed.Value : 0.5f;
    public float RingAlphaMinimum => ringAlphaMinimum != null ? ringAlphaMinimum.Value : 0.25f;
    public float RingAlphaMaximum => ringAlphaMaximum != null ? ringAlphaMaximum.Value : 1f;

    [SerializeField] private VoidEvent OnCostumeChanged;

    public Sprite GetCloudBase() => GetFrom(cloudBases, cloudBaseIndex);
    public Sprite GetBottom() => GetFrom(bottoms, bottomIndex);
    public Sprite GetRight() => GetFrom(rightSides, rightIndex);
    public Sprite GetLeft() => GetFrom(leftSides, leftIndex);
    public Sprite GetFace() => GetFrom(faces, faceIndex);
    public Color GetBaseColor() => GetColor(baseColors, baseColorIndex);
    public Color GetRingColor() => GetColor(ringColors, ringColorIndex);

    // diff = 1 => get next
    // diff = -1 => get prev
    // diff = 0 => current
    public void CycleCloudBase(int diff) => CycleIndex(cloudBases, ref cloudBaseIndex, diff);
    public void CycleBottom(int diff) => CycleIndex(bottoms, ref bottomIndex, diff);
    public void CycleRight(int diff) => CycleIndex(rightSides, ref rightIndex, diff);
    public void CycleLeft(int diff) => CycleIndex(leftSides, ref leftIndex, diff);
    public void CycleFace(int diff) => CycleIndex(faces, ref faceIndex, diff);
    public void CycleBaseColor(int diff) => CycleColorIndex(baseColors, ref baseColorIndex, diff);
    public void CycleRingColor(int diff) => CycleColorIndex(ringColors, ref ringColorIndex, diff);

    public static Color GetColor(Color[] colors, int index)
    {
        return colors != null && index >= 0 && index < colors.Length
            ? colors[index] : Color.white;
    }

    public static bool IsColorIndexValid(Color[] colors, int index)
    {
        return colors == null || colors.Length == 0
            ? index == 0 : index >= 0 && index < colors.Length;
    }

    private Sprite GetFrom(Sprite[] arr, int index)
    {
        if (arr == null || arr.Length == 0) return null;
        return arr[index];
    }

    private void CycleIndex(Sprite[] arr, ref int index, int diff)
    {
        if (arr == null || arr.Length == 0) return;

        index += diff;

        if (index < 0)
            index = arr.Length - 1;
        else if (index >= arr.Length)
            index = 0;

        OnCostumeChanged.Raise();
    }

    private void CycleColorIndex(Color[] colors, ref int index, int diff)
    {
        int count = colors == null || colors.Length == 0 ? 1 : colors.Length;
        index = ((index + diff) % count + count) % count;
        OnCostumeChanged.Raise();
    }

    public void GetRandom()
    {
        cloudBaseIndex = Random.Range(0, cloudBases.Length); 
        bottomIndex = Random.Range(0, bottoms.Length); 
        faceIndex = Random.Range(0, faces.Length); 
        leftIndex = Random.Range(0, leftSides.Length); 
        rightIndex = Random.Range(0, rightSides.Length); 
        baseColorIndex = baseColors == null || baseColors.Length == 0 ? 0 : Random.Range(0, baseColors.Length);
        ringColorIndex = ringColors == null || ringColors.Length == 0 ? 0 : Random.Range(0, ringColors.Length);

        OnCostumeChanged.Raise(); 
    }


    public void Reset()
    {
        cloudBaseIndex = bottomIndex = rightIndex = leftIndex = faceIndex = 0;
        baseColorIndex = ringColorIndex = 0;
        OnCostumeChanged.Raise();
    }
}
