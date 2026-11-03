using PurrNet;
using UnityEngine;
using UnityEngine.Rendering;

public class MP_PlayerCostume : NetworkBehaviour
{
    [SerializeField] private PlayerCostume catalog;
    [SerializeField] private SpriteRenderer cloudBase, bottom, rightSide, leftSide, face;
    [SerializeField] private SpriteRenderer ring;

    private readonly SyncVar<MP_CostumeIndices> indices = new(default);
    public MP_CostumeIndices CurrentIndices => indices.value;
    public Sprite CloudBaseSprite => cloudBase != null ? cloudBase.sprite : null;
    public Color CloudBaseColor => cloudBase != null ? cloudBase.color : Color.white;

    public bool IsValid(MP_CostumeIndices selection)
    {
        return catalog != null && cloudBase != null && bottom != null &&
            rightSide != null && leftSide != null && face != null &&
            IsIndexValid(catalog.cloudBases, selection.CloudBase) &&
            IsIndexValid(catalog.bottoms, selection.Bottom) &&
            IsIndexValid(catalog.rightSides, selection.Right) &&
            IsIndexValid(catalog.leftSides, selection.Left) &&
            IsIndexValid(catalog.faces, selection.Face) &&
            PlayerCostume.IsColorIndexValid(catalog.baseColors, selection.BaseColor) &&
            PlayerCostume.IsColorIndexValid(catalog.ringColors, selection.RingColor);
    }

    public bool SetCostumeOnServer(MP_CostumeIndices selection)
    {
        if (!isServer || !IsValid(selection))
            return false;

        indices.value = selection;
        return true;
    }

    public bool RandomizeOnServer()
    {
        if (!isServer || !IsValid(default))
            return false;

        return SetCostumeOnServer(new MP_CostumeIndices
        {
            CloudBase = Random.Range(0, catalog.cloudBases.Length),
            Bottom = Random.Range(0, catalog.bottoms.Length),
            Right = Random.Range(0, catalog.rightSides.Length),
            Left = Random.Range(0, catalog.leftSides.Length),
            Face = Random.Range(0, catalog.faces.Length),
            BaseColor = RandomColorIndex(catalog.baseColors),
            RingColor = RandomColorIndex(catalog.ringColors)
        });
    }

    private static bool IsIndexValid(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length;
    }

    private static int RandomColorIndex(Color[] colors)
    {
        return colors == null || colors.Length == 0 ? 0 : Random.Range(0, colors.Length);
    }

    private void Awake()
    {
        if (GetComponent<SortingGroup>() == null)
        {
            SortingGroup sortingGroup = gameObject.AddComponent<SortingGroup>();
            if (cloudBase != null)
            {
                sortingGroup.sortingLayerID = cloudBase.sortingLayerID;
                sortingGroup.sortingOrder = cloudBase.sortingOrder;
            }
        }

        indices.onChanged += ApplyCostume;
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (isClient)
            ApplyCostume(indices.value);
    }

    private void ApplyCostume(MP_CostumeIndices selection)
    {
        if (!isClient || !IsValid(selection))
            return;

        if (cloudBase != null)
        {
            cloudBase.sprite = catalog.cloudBases[selection.CloudBase];
            cloudBase.color = PlayerCostume.GetColor(catalog.baseColors, selection.BaseColor);
        }
        if (ring != null)
        {
            ring.sprite = catalog.ringSprite;
            ring.color = PlayerCostume.GetColor(catalog.ringColors, selection.RingColor);
        }
        if (bottom != null)
            bottom.sprite = catalog.bottoms[selection.Bottom];
        if (rightSide != null)
            rightSide.sprite = catalog.rightSides[selection.Right];
        if (leftSide != null)
            leftSide.sprite = catalog.leftSides[selection.Left];
        if (face != null)
            face.sprite = catalog.faces[selection.Face];
    }

    protected override void OnDestroy()
    {
        indices.onChanged -= ApplyCostume;
        base.OnDestroy();
    }
}
