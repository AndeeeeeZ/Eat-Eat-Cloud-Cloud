using System.Collections.Generic;
using UnityEngine;

public class MP_MiniMap : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image mapImage;
    [SerializeField] private RectTransform playerIndicatorRoot;
    [SerializeField] private UnityEngine.UI.Image playerIndicator;
    [SerializeField] private UnityEngine.UI.Image eventZoneTemplate;
    [SerializeField, Min(0f)] private float minimumEventZoneDiameter = 10f;

    private readonly List<UnityEngine.UI.Image> eventZoneIndicators = new();
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

        if (eventZoneTemplate == null || eventZoneTemplate.sprite == null ||
            eventZoneTemplate.transform.parent != mapImage.rectTransform ||
            eventZoneTemplate == playerIndicator)
            Debug.LogError("Assign a circular event zone Image directly under the map Image on MP_MiniMap", this);
        else
            eventZoneTemplate.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        MP_LocalPlayerManager manager = MP_LocalPlayerManager.Instance;
        MP_SpectatorMode spectator = MP_SpectatorMode.Instance;
        MP_Player player = spectator != null && spectator.IsSpectating
            ? spectator.ViewedPlayer : manager != null ? manager.LocalPlayer : null;
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        UpdateEventZones(boundary);
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
        Color cloudColor = displayedCostume != null
            ? displayedCostume.CloudBaseColor : Color.white;
        if (playerIndicator.color != cloudColor)
            playerIndicator.color = cloudColor;
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

    private void UpdateEventZones(MP_GameBoundary boundary)
    {
        MP_FoodSpawner spawner = MP_FoodSpawner.Instance;
        IReadOnlyList<MP_FoodEventZone> zones = spawner != null
            ? spawner.ActiveEventZones : null;
        int count = eventZoneTemplate != null && eventZoneTemplate.sprite != null &&
            eventZoneTemplate.transform.parent == mapImage.rectTransform &&
            boundary != null && boundary.Size.x > 0f && boundary.Size.y > 0f &&
            zones != null ? zones.Count : 0;

        for (int i = 0; i < count; i++)
        {
            if (i >= eventZoneIndicators.Count)
            {
                UnityEngine.UI.Image marker = Instantiate(eventZoneTemplate, mapImage.rectTransform);
                marker.name = "Event Zone Indicator";
                marker.raycastTarget = false;
                marker.transform.SetAsFirstSibling();
                marker.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                eventZoneIndicators.Add(marker);
            }

            UnityEngine.UI.Image indicator = eventZoneIndicators[i];
            MP_FoodEventZone zone = zones[i];
            RectTransform mapRect = mapImage.rectTransform;
            RectTransform markerRect = indicator.rectTransform;
            Vector2 offset = zone.Center - boundary.Center;
            Vector2 position = new Vector2(
                offset.x / boundary.Size.x * mapRect.rect.width,
                offset.y / boundary.Size.y * mapRect.rect.height);
            Vector2 diameter = new Vector2(
                2f * zone.Radius / boundary.Size.x * mapRect.rect.width,
                2f * zone.Radius / boundary.Size.y * mapRect.rect.height);
            diameter.x = Mathf.Max(diameter.x, minimumEventZoneDiameter);
            diameter.y = Mathf.Max(diameter.y, minimumEventZoneDiameter);
            if (markerRect.anchoredPosition != position)
                markerRect.anchoredPosition = position;
            if (markerRect.sizeDelta != diameter)
                markerRect.sizeDelta = diameter;
            Color color = zone.Color;
            color.a *= eventZoneTemplate.color.a;
            if (indicator.color != color)
                indicator.color = color;
            if (!indicator.enabled)
                indicator.enabled = true;
            if (!indicator.gameObject.activeSelf)
                indicator.gameObject.SetActive(true);
        }

        for (int i = count; i < eventZoneIndicators.Count; i++)
            if (eventZoneIndicators[i].gameObject.activeSelf)
                eventZoneIndicators[i].gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (playerIndicator != null)
            playerIndicator.enabled = false;
        foreach (UnityEngine.UI.Image indicator in eventZoneIndicators)
            if (indicator != null)
                indicator.gameObject.SetActive(false);
        displayedPlayer = null;
        displayedMovement = null;
        displayedCostume = null;
    }
}
