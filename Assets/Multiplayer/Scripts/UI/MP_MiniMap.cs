using System.Collections.Generic;
using UnityEngine;

public class MP_MiniMap : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image mapImage;
    [SerializeField] private RectTransform playerIndicatorRoot;
    [SerializeField] private UnityEngine.UI.Image playerIndicator;
    [SerializeField] private UnityEngine.UI.Image eventZoneTemplate;
    [SerializeField, Min(0f)] private float minimumEventZoneDiameter = 10f;
    [SerializeField, Min(0f)] private float eventZoneFadeDuration = 0.4f;

    private const float EventZoneBreathPeriodSeconds = 1.2f;

    private sealed class EventZoneIndicator
    {
        public UnityEngine.UI.Image Image;
        public Vector2 Center;
        public bool Seen;
        public bool IsFading;
        public float FadeStartTime;
        public float FadeStartAlpha;
    }

    private readonly List<EventZoneIndicator> eventZoneIndicators = new();
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
        RectTransform mapRect = mapImage.rectTransform;
        Vector2 halfSize = boundary.Size * 0.5f;
        Vector2 offset = worldPosition - boundary.Center;
        offset.x = Mathf.Clamp(offset.x, -halfSize.x, halfSize.x);
        offset.y = Mathf.Clamp(offset.y, -halfSize.y, halfSize.y);
        playerIndicatorRoot.anchoredPosition = offset * GetMapScale(mapRect, boundary);
    }

    private static float GetMapScale(RectTransform mapRect, MP_GameBoundary boundary)
    {
        return Mathf.Min(mapRect.rect.width / boundary.Size.x,
            mapRect.rect.height / boundary.Size.y);
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

        foreach (EventZoneIndicator marker in eventZoneIndicators)
            marker.Seen = false;

        for (int i = 0; i < count; i++)
        {
            MP_FoodEventZone zone = zones[i];
            EventZoneIndicator marker = null;
            foreach (EventZoneIndicator candidate in eventZoneIndicators)
            {
                if (!candidate.Seen && candidate.Image.gameObject.activeSelf &&
                    candidate.Center == zone.Center)
                {
                    marker = candidate;
                    break;
                }
            }

            if (marker == null)
                foreach (EventZoneIndicator candidate in eventZoneIndicators)
                    if (!candidate.Seen && !candidate.Image.gameObject.activeSelf)
                    {
                        marker = candidate;
                        break;
                    }

            if (marker == null)
            {
                UnityEngine.UI.Image image = Instantiate(eventZoneTemplate, mapImage.rectTransform);
                image.name = "Event Zone Indicator";
                image.raycastTarget = false;
                image.transform.SetAsFirstSibling();
                image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                marker = new EventZoneIndicator { Image = image };
                eventZoneIndicators.Add(marker);
            }

            marker.Center = zone.Center;
            marker.Seen = true;
            marker.IsFading = false;
            UnityEngine.UI.Image indicator = marker.Image;
            RectTransform mapRect = mapImage.rectTransform;
            RectTransform markerRect = indicator.rectTransform;
            Vector2 offset = zone.Center - boundary.Center;
            float mapUnitsPerWorldUnit = GetMapScale(mapRect, boundary);
            Vector2 position = offset * mapUnitsPerWorldUnit;
            float diameter = Mathf.Max(minimumEventZoneDiameter,
                2f * zone.Radius * mapUnitsPerWorldUnit);
            Vector2 markerSize = Vector2.one * diameter;
            if (markerRect.anchoredPosition != position)
                markerRect.anchoredPosition = position;
            if (markerRect.sizeDelta != markerSize)
                markerRect.sizeDelta = markerSize;
            Color color = zone.Color;
            color.a *= eventZoneTemplate.color.a;
            if (zone.IsBreathing)
            {
                float breath = 0.5f + 0.5f * Mathf.Sin(
                    Time.unscaledTime * Mathf.PI * 2f / EventZoneBreathPeriodSeconds);
                color.a *= Mathf.Lerp(0.2f, 1f, breath);
            }
            if (indicator.color != color)
                indicator.color = color;
            if (!indicator.enabled)
                indicator.enabled = true;
            if (!indicator.gameObject.activeSelf)
                indicator.gameObject.SetActive(true);
        }

        foreach (EventZoneIndicator marker in eventZoneIndicators)
        {
            UnityEngine.UI.Image indicator = marker.Image;
            if (marker.Seen || !indicator.gameObject.activeSelf)
                continue;

            if (!marker.IsFading)
            {
                marker.IsFading = true;
                marker.FadeStartTime = Time.unscaledTime;
                marker.FadeStartAlpha = indicator.color.a;
            }

            float progress = eventZoneFadeDuration > 0f
                ? (Time.unscaledTime - marker.FadeStartTime) / eventZoneFadeDuration : 1f;
            if (progress >= 1f)
            {
                indicator.gameObject.SetActive(false);
                continue;
            }

            Color color = indicator.color;
            color.a = marker.FadeStartAlpha * (1f - progress);
            indicator.color = color;
        }
    }

    private void OnDisable()
    {
        if (playerIndicator != null)
            playerIndicator.enabled = false;
        foreach (EventZoneIndicator marker in eventZoneIndicators)
        {
            marker.IsFading = false;
            if (marker.Image != null)
                marker.Image.gameObject.SetActive(false);
        }
        displayedPlayer = null;
        displayedMovement = null;
        displayedCostume = null;
    }
}
