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

    private void Update()
    {
        if (costume == null || ring == null || ring.sprite == null)
            return;

        ring.transform.Rotate(0f, 0f, costume.RingSpinSpeed * Time.deltaTime);
        ring.color = CostumeRingAnimation.WithBreathingAlpha(
            ring.color, Time.time, costume);
    }

    public void UpdateCostume()
    {
        cloudBase.sprite = costume.GetCloudBase();
        cloudBase.color = costume.GetBaseColor();
        if (ring != null)
        {
            ring.sprite = costume.ringSprite;
            ring.color = CostumeRingAnimation.WithBreathingAlpha(
                costume.GetRingColor(), Time.time, costume);
        }
        bottom.sprite = costume.GetBottom();
        rightSide.sprite = costume.GetRight();
        leftSide.sprite = costume.GetLeft();
        face.sprite = costume.GetFace();
    }
}
