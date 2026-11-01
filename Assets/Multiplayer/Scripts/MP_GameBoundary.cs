using UnityEngine;

public class MP_GameBoundary : MonoBehaviour
{
    [SerializeField] private Vector2 size = new(30f, 30f);
    [SerializeField] private bool showBoundaryGizmo = true;
    [SerializeField, Min(0.01f)] private float boundaryLineWidth = 0.1f;
    [SerializeField] private Color boundaryColor = Color.cyan;
    [SerializeField] private int boundarySortingOrder = 100;
    [SerializeField, Min(0f)] private float warningDistance = 25f;
    [SerializeField, Min(0.01f)] private float fadeDistance = 10f;
    [SerializeField, Min(0.01f)] private float pulseDuration = 1.5f;

    private bool showBoundaryInGame = true;
    private SpriteRenderer[] boundaryLines;
    private Sprite boundarySprite;
    private Texture2D boundaryTexture;
    private MP_Player trackedPlayer;
    private CircleCollider2D trackedCollider;
    private MP_PlayerMovement trackedMovement;

    public static MP_GameBoundary Instance { get; private set; }
    public Vector2 Center => transform.position;
    public Vector2 Size => new(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one MP_GameBoundary should be active in a scene", this);
            enabled = false;
            return;
        }

        Instance = this;
        if (!Application.isBatchMode)
            CreateBoundaryLines();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        boundaryLines = null;
        if (boundarySprite != null)
            Destroy(boundarySprite);
        if (boundaryTexture != null)
            Destroy(boundaryTexture);
    }

    private void Update()
    {
        UpdateBoundaryVisibility();
    }

    private void OnValidate()
    {
        size.x = Mathf.Max(0f, size.x);
        size.y = Mathf.Max(0f, size.y);
        boundaryLineWidth = Mathf.Max(0.01f, boundaryLineWidth);
        warningDistance = Mathf.Max(0f, warningDistance);
        fadeDistance = Mathf.Max(0.01f, fadeDistance);
        pulseDuration = Mathf.Max(0.01f, pulseDuration);
        if (Application.isPlaying)
        {
            UpdateBoundaryLines();
            UpdateBoundaryVisibility();
        }
    }

    public void SetInGameBoundaryVisible(bool visible)
    {
        showBoundaryInGame = visible;
        UpdateBoundaryVisibility();
    }

    private void CreateBoundaryLines()
    {
        boundaryTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        boundaryTexture.SetPixel(0, 0, Color.white);
        boundaryTexture.Apply();
        boundarySprite = Sprite.Create(boundaryTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        SpriteRenderer[] lines = new SpriteRenderer[4];
        string[] names = { "Top", "Bottom", "Left", "Right" };
        for (int i = 0; i < lines.Length; i++)
        {
            GameObject line = new GameObject($"Boundary {names[i]}");
            line.transform.SetParent(transform, false);
            lines[i] = line.AddComponent<SpriteRenderer>();
            lines[i].sprite = boundarySprite;
        }

        boundaryLines = lines;
        UpdateBoundaryLines();
        UpdateBoundaryVisibility();
    }

    private void UpdateBoundaryLines()
    {
        if (boundaryLines == null || boundaryLines.Length != 4)
            return;

        Vector2 extent = Size * 0.5f;
        float width = Mathf.Max(0.01f, boundaryLineWidth);
        SetBoundaryLine(0, new Vector2(0f, extent.y), new Vector2(Size.x + width, width));
        SetBoundaryLine(1, new Vector2(0f, -extent.y), new Vector2(Size.x + width, width));
        SetBoundaryLine(2, new Vector2(-extent.x, 0f), new Vector2(width, Size.y + width));
        SetBoundaryLine(3, new Vector2(extent.x, 0f), new Vector2(width, Size.y + width));
    }

    private void SetBoundaryLine(int index, Vector2 position, Vector2 lineSize)
    {
        SpriteRenderer line = boundaryLines[index];
        if (line == null)
            return;

        line.transform.localPosition = position;
        line.transform.localScale = new Vector3(lineSize.x, lineSize.y, 1f);
        line.color = boundaryColor;
        line.sortingOrder = boundarySortingOrder;
    }

    private void UpdateBoundaryVisibility()
    {
        if (boundaryLines == null || boundaryLines.Length != 4)
            return;

        float alpha = 0f;
        MP_LocalPlayerManager local = MP_LocalPlayerManager.Instance;
        MP_Player player = local != null ? local.LocalPlayer : null;
        if (player != trackedPlayer)
        {
            trackedPlayer = player;
            trackedCollider = player != null ? player.GetComponent<CircleCollider2D>() : null;
            trackedMovement = player != null ? player.GetComponent<MP_PlayerMovement>() : null;
        }

        if (showBoundaryInGame && Size.x > 0f && Size.y > 0f && player != null)
        {
            Vector2 position = trackedMovement != null
                ? trackedMovement.PresentationPosition : (Vector2)player.transform.position;
            Vector2 offset = position - Center;
            Vector2 extent = Size * 0.5f;
            Vector3 scale = player.transform.lossyScale;
            float radius = trackedCollider != null
                ? trackedCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) : 0f;
            float edgeDistance = Mathf.Min(extent.x - Mathf.Abs(offset.x),
                extent.y - Mathf.Abs(offset.y)) - radius;
            float proximity = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((warningDistance - edgeDistance) / fadeDistance));
            float pulse = 0.5f + 0.5f *
                Mathf.Sin(Time.time * Mathf.PI * 2f / pulseDuration);
            alpha = proximity * pulse;
        }

        for (int i = 0; i < boundaryLines.Length; i++)
        {
            SpriteRenderer line = boundaryLines[i];
            if (line == null)
                continue;

            Color color = boundaryColor;
            color.a *= alpha;
            line.color = color;
            line.enabled = color.a > 0f;
        }
    }

    public bool CanFit(float margin)
    {
        Vector2 extent = Size * 0.5f;
        margin = Mathf.Max(0f, margin);
        return extent.x >= margin && extent.y >= margin && size.x > 0f && size.y > 0f;
    }

    public bool Contains(Vector2 position, float margin = 0f)
    {
        if (!CanFit(margin))
            return false;

        Vector2 extent = Size * 0.5f - Vector2.one * Mathf.Max(0f, margin);
        Vector2 offset = position - Center;
        return Mathf.Abs(offset.x) <= extent.x && Mathf.Abs(offset.y) <= extent.y;
    }

    public Vector3 ClampPosition(Vector3 position, float margin = 0f)
    {
        Vector2 extent = Size * 0.5f - Vector2.one * Mathf.Max(0f, margin);
        extent.x = Mathf.Max(0f, extent.x);
        extent.y = Mathf.Max(0f, extent.y);
        Vector2 center = Center;
        position.x = Mathf.Clamp(position.x, center.x - extent.x, center.x + extent.x);
        position.y = Mathf.Clamp(position.y, center.y - extent.y, center.y + extent.y);
        return position;
    }

    public bool TryGetRandomPosition(float margin, out Vector2 position)
    {
        if (!CanFit(margin))
        {
            position = Center;
            return false;
        }

        Vector2 extent = Size * 0.5f - Vector2.one * Mathf.Max(0f, margin);
        Vector2 center = Center;
        position = new Vector2(
            Random.Range(center.x - extent.x, center.x + extent.x),
            Random.Range(center.y - extent.y, center.y + extent.y)
        );
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!showBoundaryGizmo)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(Size.x, Size.y, 0f));
    }
}
