using TMPro;
using UnityEngine;

public class CostumeSelection : MonoBehaviour
{
    [SerializeField] private PlayerCostume costume;
    [SerializeField] private TextMeshProUGUI cloudBaseIndex, bottomIndex, rightIndex, leftIndex, faceIndex;
    [SerializeField] private TextMeshProUGUI baseColorIndex, ringColorIndex;

    private void Start()
    {
        costume.Reset(); 
        UpdateIndexIndications(); 
    }

    public void UpdateIndexIndications()
    {
        cloudBaseIndex.text = costume.cloudBaseIndex.ToString();
        bottomIndex.text    = costume.bottomIndex.ToString();
        rightIndex.text     = costume.rightIndex.ToString();
        leftIndex.text      = costume.leftIndex.ToString();
        faceIndex.text      = costume.faceIndex.ToString();
        if (baseColorIndex != null)
            baseColorIndex.text = costume.baseColorIndex.ToString();
        if (ringColorIndex != null)
            ringColorIndex.text = costume.ringColorIndex.ToString();
    }
}
