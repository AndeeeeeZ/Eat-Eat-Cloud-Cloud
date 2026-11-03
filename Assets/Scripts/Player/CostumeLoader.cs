using UnityEngine;

public class CostumeLoader : MonoBehaviour
{
    [SerializeField] private PlayerCostume costume;
    [SerializeField] private SpriteRenderer cloudBase, bottom, rightSide, leftSide, face;
    [SerializeField] private SpriteRenderer ring;

    private void Start()
    {
        UpdateCostume();
    }

    public void UpdateCostume()
    {
        cloudBase.sprite = costume.GetCloudBase();
        cloudBase.color = costume.GetBaseColor();
        if (ring != null)
        {
            ring.sprite = costume.ringSprite;
            ring.color = costume.GetRingColor();
        }
        bottom.sprite = costume.GetBottom();
        rightSide.sprite = costume.GetRight();
        leftSide.sprite = costume.GetLeft();
        face.sprite = costume.GetFace();
    }
}
