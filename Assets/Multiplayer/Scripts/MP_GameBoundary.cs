using UnityEngine;

public class MP_GameBoundary : MonoBehaviour
{
    [SerializeField] private Vector2 size = new(30f, 30f);
    [SerializeField] private bool showBoundaryGizmo = true;
    [SerializeField] private bool showBoundaryInGame = true;
    [SerializeField, Min(0.01f)] private float boundaryLineWidth = 0.1f;
    [SerializeField] private Color boundaryColor = Color.cyan;
    [SerializeField] private int boundarySortingOrder = 100;

    private SpriteRenderer[] boundaryLines;
    private Sprite boundarySprite;
    private Texture2D boundaryTexture;

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

    private void OnValidate()
    {
        size.x = Mathf.Max(0f, size.x);
        size.y = Mathf.Max(0f, size.y);
        boundaryLineWidth = Mathf.Max(0.01f, boundaryLineWidth);
        if (Application.isPlaying)
            UpdateBoundaryLines();
    }

    public void SetInGameBoundaryVisible(bool visible)
    {
        showBoundaryInGame = visible;
        UpdateBoundaryLines();
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
        line.enabled = showBoundaryInGame && Size.x > 0f && Size.y > 0f;
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
