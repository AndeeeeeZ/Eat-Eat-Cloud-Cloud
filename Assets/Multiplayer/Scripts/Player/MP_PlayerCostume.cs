using PurrNet;
using UnityEngine;

public class MP_PlayerCostume : NetworkBehaviour
{
    [SerializeField] private PlayerCostume catalog;
    [SerializeField] private SpriteRenderer cloudBase, bottom, rightSide, leftSide, face;

    private readonly SyncVar<MP_CostumeIndices> indices = new(default);

    public bool IsValid(MP_CostumeIndices selection)
    {
        return catalog != null && cloudBase != null && bottom != null &&
            rightSide != null && leftSide != null && face != null &&
            IsIndexValid(catalog.cloudBases, selection.CloudBase) &&
            IsIndexValid(catalog.bottoms, selection.Bottom) &&
            IsIndexValid(catalog.rightSides, selection.Right) &&
            IsIndexValid(catalog.leftSides, selection.Left) &&
            IsIndexValid(catalog.faces, selection.Face);
    }

    public bool SetCostumeOnServer(MP_CostumeIndices selection)
    {
        if (!isServer || !IsValid(selection))
            return false;

        indices.value = selection;
        return true;
    }

    private static bool IsIndexValid(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length;
    }

    private void Awake()
    {
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
            cloudBase.sprite = catalog.cloudBases[selection.CloudBase];
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
