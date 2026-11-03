using TMPro;
using UnityEngine;

public class MP_CostumePicker : MonoBehaviour
{
    [SerializeField] private MP_PlayerData playerData;
    [SerializeField] private PlayerCostume catalog;

    [Header("Preview")]
    [SerializeField] private UnityEngine.UI.Image cloudBase;
    [SerializeField] private UnityEngine.UI.Image bottom, rightSide, leftSide, face;
    [SerializeField] private UnityEngine.UI.Image ring;

    [Header("Index Labels")]
    [SerializeField] private TextMeshProUGUI cloudBaseIndex;
    [SerializeField] private TextMeshProUGUI bottomIndex, rightIndex, leftIndex, faceIndex;
    [SerializeField] private TextMeshProUGUI baseColorIndex, ringColorIndex;

    private void Start()
    {
        playerData?.RestoreSelection();
        RefreshPreview();
    }

    public void CycleCloudBase(int diff)
    {
        if (playerData == null || catalog == null)
            return;

        MP_CostumeIndices selection = playerData.Costume;
        selection.CloudBase = Cycle(selection.CloudBase, catalog.cloudBases, diff);
        SetSelection(selection);
    }

    public void CycleBottom(int diff)
    {
        if (playerData == null || catalog == null)
            return;

        MP_CostumeIndices selection = playerData.Costume;
        selection.Bottom = Cycle(selection.Bottom, catalog.bottoms, diff);
        SetSelection(selection);
    }

    public void CycleRight(int diff)
    {
        if (playerData == null || catalog == null)
            return;

        MP_CostumeIndices selection = playerData.Costume;
        selection.Right = Cycle(selection.Right, catalog.rightSides, diff);
        SetSelection(selection);
    }

    public void CycleLeft(int diff)
    {
        if (playerData == null || catalog == null)
            return;

        MP_CostumeIndices selection = playerData.Costume;
        selection.Left = Cycle(selection.Left, catalog.leftSides, diff);
        SetSelection(selection);
    }

    public void CycleFace(int diff)
    {
        if (playerData == null || catalog == null)
            return;

        MP_CostumeIndices selection = playerData.Costume;
        selection.Face = Cycle(selection.Face, catalog.faces, diff);
        SetSelection(selection);
    }

    public void CycleBaseColor(int diff)
    {
        if (playerData == null || catalog == null)
            return;

        MP_CostumeIndices selection = playerData.Costume;
        selection.BaseColor = Cycle(selection.BaseColor, catalog.baseColors, diff);
        SetSelection(selection);
    }

    public void CycleRingColor(int diff)
    {
        if (playerData == null || catalog == null)
            return;

        MP_CostumeIndices selection = playerData.Costume;
        selection.RingColor = Cycle(selection.RingColor, catalog.ringColors, diff);
        SetSelection(selection);
    }

    public void Randomize()
    {
        if (playerData == null || catalog == null)
            return;

        SetSelection(new MP_CostumeIndices
        {
            CloudBase = RandomIndex(catalog.cloudBases),
            Bottom = RandomIndex(catalog.bottoms),
            Right = RandomIndex(catalog.rightSides),
            Left = RandomIndex(catalog.leftSides),
            Face = RandomIndex(catalog.faces),
            BaseColor = RandomIndex(catalog.baseColors),
            RingColor = RandomIndex(catalog.ringColors)
        });
    }

    public void ResetSelection()
    {
        if (playerData != null)
            SetSelection(default);
    }

    private void SetSelection(MP_CostumeIndices selection)
    {
        playerData.Costume = selection;
        playerData.RememberSelection();
        RefreshPreview();
    }

    private static int Cycle(int current, Sprite[] sprites, int diff)
    {
        if (sprites == null || sprites.Length == 0)
            return 0;

        int next = (current + diff) % sprites.Length;
        return next < 0 ? next + sprites.Length : next;
    }

    private static int Cycle(int current, Color[] colors, int diff)
    {
        if (colors == null || colors.Length == 0)
            return 0;

        int next = (current + diff) % colors.Length;
        return next < 0 ? next + colors.Length : next;
    }

    private static int RandomIndex(Sprite[] sprites)
    {
        return sprites == null || sprites.Length == 0 ? 0 : Random.Range(0, sprites.Length);
    }

    private static int RandomIndex(Color[] colors)
    {
        return colors == null || colors.Length == 0 ? 0 : Random.Range(0, colors.Length);
    }

    private void RefreshPreview()
    {
        if (playerData == null || catalog == null)
        {
            Debug.LogError("Assign player data and a costume catalog to MP_CostumePicker", this);
            return;
        }

        MP_CostumeIndices selection = playerData.Costume;
        SetPreviewImage(cloudBase, GetSprite(catalog.cloudBases, selection.CloudBase));
        if (cloudBase != null)
            cloudBase.color = PlayerCostume.GetColor(catalog.baseColors, selection.BaseColor);
        SetPreviewImage(ring, catalog.ringSprite);
        if (ring != null)
            ring.color = PlayerCostume.GetColor(catalog.ringColors, selection.RingColor);
        SetPreviewImage(bottom, GetSprite(catalog.bottoms, selection.Bottom));
        SetPreviewImage(rightSide, GetSprite(catalog.rightSides, selection.Right));
        SetPreviewImage(leftSide, GetSprite(catalog.leftSides, selection.Left));
        SetPreviewImage(face, GetSprite(catalog.faces, selection.Face));

        if (cloudBaseIndex != null)
            cloudBaseIndex.text = selection.CloudBase.ToString();
        if (bottomIndex != null)
            bottomIndex.text = selection.Bottom.ToString();
        if (rightIndex != null)
            rightIndex.text = selection.Right.ToString();
        if (leftIndex != null)
            leftIndex.text = selection.Left.ToString();
        if (faceIndex != null)
            faceIndex.text = selection.Face.ToString();
        if (baseColorIndex != null)
            baseColorIndex.text = selection.BaseColor.ToString();
        if (ringColorIndex != null)
            ringColorIndex.text = selection.RingColor.ToString();
    }

    private static Sprite GetSprite(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    private static void SetPreviewImage(UnityEngine.UI.Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}
