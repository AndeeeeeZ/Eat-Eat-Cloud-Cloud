using System.Collections.Generic;
using UnityEngine;

// Local, pooled food sprites. The server owns the food records and collisions
internal sealed class MP_FoodClientVisuals
{
    private readonly struct VisualData
    {
        public readonly Vector2 Position;
        public readonly float Size;

        public VisualData(Vector2 position, float size)
        {
            Position = position;
            Size = size;
        }
    }

    private sealed class Visual
    {
        public GameObject GameObject;
        public SpriteRenderer Renderer;
        public float Elapsed;
        public float FadeStartAlpha;
        public bool IsFading;
        public bool IsFadingOut;
        public Vector3 GrowthStartPosition;
        public Vector3 GrowthTargetPosition;
        public Vector3 GrowthStartScale;
        public Vector3 GrowthTargetScale;
        public float GrowthElapsed;
        public bool IsGrowing;
    }

    private readonly Transform parent;
    private readonly SpriteRenderer source;
    private readonly Vector3 positionOffset;
    private readonly Vector3 visualScale;
    private readonly Quaternion visualRotation;
    private readonly Vector2 spriteExtents;
    private readonly Color baseColor;
    private readonly float fadeInDuration;
    private readonly Dictionary<uint, VisualData> records = new();
    private readonly Dictionary<uint, Visual> active = new();
    private readonly Dictionary<int, HashSet<uint>> idsByChunk = new();
    private readonly Stack<Visual> pool = new();
    private readonly List<Visual> fading = new();
    private readonly List<Visual> growing = new();
    private readonly HashSet<uint> incomingIds = new();
    private readonly HashSet<uint> retiredIds = new();
    private readonly List<uint> removedIds = new();
    private Camera localCamera;
    private float visibilityRefreshElapsed;
    private const int MaxPooledVisuals = 512;
    private const float VisibilityRefreshSeconds = 0.1f;
    private const float CameraEdgePadding = 1f;

    public MP_FoodClientVisuals(GameObject prefab, Transform parent)
    {
        this.parent = parent;
        source = prefab.GetComponentInChildren<SpriteRenderer>(true);
        MP_Food food = prefab.GetComponent<MP_Food>();
        positionOffset = Vector3.Scale(prefab.transform.localScale, source.transform.localPosition);
        visualScale = Vector3.Scale(prefab.transform.localScale, source.transform.localScale);
        visualRotation = prefab.transform.localRotation * source.transform.localRotation;
        spriteExtents = source.sprite.bounds.extents;
        baseColor = source.color;
        fadeInDuration = food != null ? Mathf.Max(0f, food.FadeInDuration) : 0f;
        localCamera = Camera.main;
    }

    public void ApplyChunk(int chunkIndex, uint[] ids, Vector2[] positions,
        float[] sizes, bool[] fadeIn, uint[] retiredIds)
    {
        if (ids == null || positions == null || sizes == null || fadeIn == null || retiredIds == null ||
            ids.Length != positions.Length || ids.Length != sizes.Length ||
            ids.Length != fadeIn.Length)
            return;

        bool firstSnapshot = !idsByChunk.TryGetValue(chunkIndex, out HashSet<uint> current);
        if (firstSnapshot)
        {
            current = new HashSet<uint>();
            idsByChunk.Add(chunkIndex, current);
        }

        incomingIds.Clear();
        for (int i = 0; i < ids.Length; i++)
        {
            incomingIds.Add(ids[i]);
            if (current.Add(ids[i]))
            {
                VisualData data = new(positions[i], sizes[i]);
                records.Add(ids[i], data);
                if (IsNearCamera(data))
                    Show(ids[i], positions[i], sizes[i], !firstSnapshot && fadeIn[i]);
            }
            else if (records.TryGetValue(ids[i], out VisualData previous) &&
                     !Mathf.Approximately(previous.Size, sizes[i]))
            {
                VisualData data = new(positions[i], sizes[i]);
                records[ids[i]] = data;
                if (active.TryGetValue(ids[i], out Visual visual))
                    Grow(visual, data);
            }
        }

        removedIds.Clear();
        this.retiredIds.Clear();
        this.retiredIds.UnionWith(retiredIds);
        foreach (uint id in current)
            if (!incomingIds.Contains(id))
                removedIds.Add(id);

        foreach (uint id in removedIds)
        {
            current.Remove(id);
            records.Remove(id);
            if (active.TryGetValue(id, out Visual visual))
            {
                active.Remove(id);
                if (this.retiredIds.Contains(id))
                    FadeOut(visual);
                else
                    Release(visual);
            }
        }
    }

    public void ClearChunk(int chunkIndex)
    {
        if (!idsByChunk.TryGetValue(chunkIndex, out HashSet<uint> ids))
            return;

        foreach (uint id in ids)
        {
            records.Remove(id);
            if (!active.TryGetValue(id, out Visual visual))
                continue;

            active.Remove(id);
            Release(visual);
        }

        idsByChunk.Remove(chunkIndex);
    }

    public void Tick(float deltaTime)
    {
        visibilityRefreshElapsed += deltaTime;
        if (visibilityRefreshElapsed >= VisibilityRefreshSeconds)
        {
            visibilityRefreshElapsed = 0f;
            RefreshVisible();
        }

        for (int i = growing.Count - 1; i >= 0; i--)
        {
            Visual visual = growing[i];
            visual.GrowthElapsed += deltaTime;
            float progress = Mathf.Clamp01(visual.GrowthElapsed / MP_Food.GrowthDurationSeconds);
            visual.GameObject.transform.position = Vector3.Lerp(
                visual.GrowthStartPosition, visual.GrowthTargetPosition, progress);
            visual.GameObject.transform.localScale = Vector3.Lerp(
                visual.GrowthStartScale, visual.GrowthTargetScale, progress);
            if (progress < 1f)
                continue;

            visual.IsGrowing = false;
            growing.RemoveAt(i);
        }

        for (int i = fading.Count - 1; i >= 0; i--)
        {
            Visual visual = fading[i];
            visual.Elapsed += deltaTime;
            float duration = visual.IsFadingOut ? Mathf.Max(0.1f, fadeInDuration) : fadeInDuration;
            float progress = Mathf.Clamp01(visual.Elapsed / duration);
            SetAlpha(visual, visual.IsFadingOut ? visual.FadeStartAlpha * (1f - progress) : progress);
            if (progress < 1f)
                continue;

            visual.IsFading = false;
            fading.RemoveAt(i);
            if (visual.IsFadingOut)
                Release(visual);
        }
    }

    public void Dispose()
    {
        foreach (Visual visual in active.Values)
            Object.Destroy(visual.GameObject);
        foreach (Visual visual in pool)
            Object.Destroy(visual.GameObject);
        foreach (Visual visual in fading)
            if (visual.IsFadingOut)
                Object.Destroy(visual.GameObject);
        active.Clear();
        records.Clear();
        idsByChunk.Clear();
        fading.Clear();
        growing.Clear();
        pool.Clear();
    }

    private void Show(uint id, Vector2 position, float size, bool fadeIn)
    {
        Visual visual = pool.Count > 0 ? pool.Pop() : CreateVisual();
        visual.GameObject.transform.position = (Vector3)position + positionOffset * size;
        visual.GameObject.transform.localScale = visualScale * size;
        visual.GameObject.SetActive(true);
        active.Add(id, visual);
        bool animate = fadeIn && fadeInDuration > 0f;
        SetAlpha(visual, animate ? 0f : 1f);
        visual.IsFadingOut = false;
        visual.IsGrowing = false;
        if (animate)
        {
            visual.Elapsed = 0f;
            visual.IsFading = true;
            fading.Add(visual);
        }
    }

    private void RefreshVisible()
    {
        if (localCamera == null)
            localCamera = Camera.main;

        foreach (KeyValuePair<uint, VisualData> entry in records)
        {
            bool visible = IsNearCamera(entry.Value);
            if (visible && !active.ContainsKey(entry.Key))
                Show(entry.Key, entry.Value.Position, entry.Value.Size, false);
            else if (!visible && active.TryGetValue(entry.Key, out Visual visual))
            {
                active.Remove(entry.Key);
                Release(visual);
            }
        }
    }

    private bool IsNearCamera(VisualData data)
    {
        if (localCamera == null)
            return true;

        Vector2 center = data.Position + (Vector2)(positionOffset * data.Size);
        Vector2 offset = center - (Vector2)localCamera.transform.position;
        float halfWidth = localCamera.orthographicSize * localCamera.aspect;
        float halfHeight = localCamera.orthographicSize;
        return Mathf.Abs(offset.x) <= halfWidth +
                Mathf.Abs(visualScale.x) * spriteExtents.x * data.Size + CameraEdgePadding &&
            Mathf.Abs(offset.y) <= halfHeight +
                Mathf.Abs(visualScale.y) * spriteExtents.y * data.Size + CameraEdgePadding;
    }

    private Visual CreateVisual()
    {
        GameObject gameObject = new("Food visual");
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.rotation = visualRotation;
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        return new Visual { GameObject = gameObject, Renderer = renderer };
    }

    private void SetAlpha(Visual visual, float alpha)
    {
        Color color = baseColor;
        color.a *= alpha;
        visual.Renderer.color = color;
    }

    private void Grow(Visual visual, VisualData data)
    {
        visual.GrowthStartPosition = visual.GameObject.transform.position;
        visual.GrowthTargetPosition = (Vector3)data.Position + positionOffset * data.Size;
        visual.GrowthStartScale = visual.GameObject.transform.localScale;
        visual.GrowthTargetScale = visualScale * data.Size;
        visual.GrowthElapsed = 0f;
        if (visual.IsGrowing)
            return;

        visual.IsGrowing = true;
        growing.Add(visual);
    }

    private void FadeOut(Visual visual)
    {
        if (visual.IsFading)
            fading.Remove(visual);

        visual.Elapsed = 0f;
        visual.FadeStartAlpha = baseColor.a > 0f ? visual.Renderer.color.a / baseColor.a : 1f;
        visual.IsFading = true;
        visual.IsFadingOut = true;
        fading.Add(visual);
    }

    private void Release(Visual visual)
    {
        if (visual.IsGrowing)
        {
            visual.IsGrowing = false;
            growing.Remove(visual);
        }

        if (visual.IsFading)
        {
            visual.IsFading = false;
            fading.Remove(visual);
        }

        visual.IsFadingOut = false;
        visual.GameObject.SetActive(false);
        if (pool.Count < MaxPooledVisuals)
            pool.Push(visual);
        else
            Object.Destroy(visual.GameObject);
    }
}
