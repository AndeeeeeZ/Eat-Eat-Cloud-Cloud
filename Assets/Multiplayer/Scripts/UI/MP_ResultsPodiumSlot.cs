using TMPro;
using UnityEngine;

public class MP_ResultsPodiumSlot : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject localPlayerMarker;
    [SerializeField] private UnityEngine.UI.Image cloudBase;
    [SerializeField] private UnityEngine.UI.Image bottom;
    [SerializeField] private UnityEngine.UI.Image rightSide;
    [SerializeField] private UnityEngine.UI.Image leftSide;
    [SerializeField] private UnityEngine.UI.Image face;
    [SerializeField] private UnityEngine.UI.Image ring;

    [Header("Jump")]
    [SerializeField] private RectTransform jumpTarget;
    [SerializeField, Min(0f)] private float jumpHeight = 20f;
    [SerializeField, Min(0f)] private float jumpsPerSecond = 1f;

    private Vector2 restingPosition;
    private float jumpProgress;

    private void OnEnable()
    {
        if (jumpTarget == null)
            jumpTarget = transform as RectTransform;
        if (jumpTarget != null)
            restingPosition = jumpTarget.anchoredPosition;
        jumpProgress = 0f;
    }

    private void LateUpdate()
    {
        if (jumpTarget == null)
            return;

        jumpProgress = Mathf.Repeat(jumpProgress + Time.unscaledDeltaTime *
            Mathf.Max(0f, jumpsPerSecond), 1f);
        float offset = Mathf.Sin(jumpProgress * Mathf.PI) * Mathf.Max(0f, jumpHeight);
        jumpTarget.anchoredPosition = restingPosition + Vector2.up * offset;
    }

    private void OnDisable()
    {
        if (jumpTarget != null)
            jumpTarget.anchoredPosition = restingPosition;
    }

    public void Apply(MP_RoundStanding player, PlayerCostume catalog)
    {
        if (nameText != null)
        {
            nameText.richText = false;
            nameText.text = player.PlayerName ?? "";
        }
        if (localPlayerMarker != null)
        {
            if (transform.IsChildOf(localPlayerMarker.transform))
                Debug.LogWarning("Assign a separate child object as the podium's local player marker", this);
            else
                localPlayerMarker.SetActive(player.IsLocal);
        }

        MP_CostumeIndices look = player.Costume;
        SetSprite(cloudBase, catalog != null ? catalog.cloudBases : null, look.CloudBase);
        if (cloudBase != null)
            cloudBase.color = catalog != null
                ? PlayerCostume.GetColor(catalog.baseColors, look.BaseColor) : Color.white;
        if (ring != null)
        {
            ring.sprite = catalog != null ? catalog.ringSprite : null;
            ring.enabled = ring.sprite != null;
            ring.color = catalog != null
                ? PlayerCostume.GetColor(catalog.ringColors, look.RingColor) : Color.white;
        }
        SetSprite(bottom, catalog != null ? catalog.bottoms : null, look.Bottom);
        SetSprite(rightSide, catalog != null ? catalog.rightSides : null, look.Right);
        SetSprite(leftSide, catalog != null ? catalog.leftSides : null, look.Left);
        SetSprite(face, catalog != null ? catalog.faces : null, look.Face);
    }

    private static void SetSprite(UnityEngine.UI.Image image, Sprite[] sprites, int index)
    {
        if (image == null)
            return;
        bool valid = sprites != null && index >= 0 && index < sprites.Length;
        image.sprite = valid ? sprites[index] : null;
        image.enabled = valid && image.sprite != null;
    }
}
