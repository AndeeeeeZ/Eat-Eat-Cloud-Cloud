using UnityEngine;

public class MP_MiniMap : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image mapImage;
    [SerializeField] private RectTransform playerIndicatorRoot;
    [SerializeField] private UnityEngine.UI.Image playerIndicator;

    private MP_Player displayedPlayer;
    private MP_PlayerMovement displayedMovement;
    private MP_PlayerCostume displayedCostume;

    private void OnEnable()
    {
        if (mapImage == null || mapImage.sprite == null ||
            playerIndicatorRoot == null || playerIndicator == null)
        {
            Debug.LogError("Assign a map Image with a sprite, player indicator root, and player indicator Image on MP_MiniMap", this);
            enabled = false;
            return;
        }

        if (playerIndicatorRoot.parent != mapImage.rectTransform ||
            playerIndicator.rectTransform.parent != playerIndicatorRoot)
        {
            Debug.LogError("Place the player indicator root under the map Image and the player indicator Image under that root", this);
            enabled = false;
            return;
        }

        playerIndicatorRoot.anchorMin = new Vector2(0.5f, 0.5f);
        playerIndicatorRoot.anchorMax = new Vector2(0.5f, 0.5f);
        playerIndicatorRoot.pivot = new Vector2(0.5f, 0.5f);
        playerIndicator.enabled = false;
    }

    private void LateUpdate()
    {
        MP_LocalPlayerManager manager = MP_LocalPlayerManager.Instance;
        MP_SpectatorMode spectator = MP_SpectatorMode.Instance;
        MP_Player player = spectator != null && spectator.IsSpectating
            ? spectator.ViewedPlayer : manager != null ? manager.LocalPlayer : null;
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (player == null || boundary == null || boundary.Size.x <= 0f || boundary.Size.y <= 0f)
        {
            playerIndicator.enabled = false;
            displayedPlayer = null;
            displayedMovement = null;
            displayedCostume = null;
            return;
        }

        if (player != displayedPlayer)
        {
            displayedPlayer = player;
            displayedMovement = player.GetComponent<MP_PlayerMovement>();
            displayedCostume = player.GetComponent<MP_PlayerCostume>();
        }

        Sprite cloudBase = displayedCostume != null ? displayedCostume.CloudBaseSprite : null;
        if (playerIndicator.sprite != cloudBase)
            playerIndicator.sprite = cloudBase;
        playerIndicator.enabled = cloudBase != null;

        Vector2 worldPosition = displayedMovement != null
            ? displayedMovement.PresentationPosition : (Vector2)player.transform.position;
        Vector2 normalized = worldPosition - boundary.Center;
        Vector2 size = boundary.Size;
        normalized.x = Mathf.Clamp01(normalized.x / size.x + 0.5f);
        normalized.y = Mathf.Clamp01(normalized.y / size.y + 0.5f);

        RectTransform mapRect = mapImage.rectTransform;
        playerIndicatorRoot.anchoredPosition = new Vector2(
            (normalized.x - 0.5f) * mapRect.rect.width,
            (normalized.y - 0.5f) * mapRect.rect.height);
    }

    private void OnDisable()
    {
        if (playerIndicator != null)
            playerIndicator.enabled = false;
        displayedPlayer = null;
        displayedMovement = null;
        displayedCostume = null;
    }
}
