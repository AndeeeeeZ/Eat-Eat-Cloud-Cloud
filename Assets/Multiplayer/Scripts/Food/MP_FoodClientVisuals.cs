using System.Collections.Generic;
using UnityEngine;

// Local, pooled food sprites. The server owns the food records and collisions
internal sealed class MP_FoodClientVisuals
{
    private sealed class Visual
    {
        public GameObject GameObject;
        public SpriteRenderer Renderer;
        public float Elapsed;
        public bool IsFading;
    }

    private readonly Transform parent;
    private readonly SpriteRenderer source;
    private readonly Vector3 positionOffset;
    private readonly Vector3 visualScale;
    private readonly Quaternion visualRotation;
    private readonly Color baseColor;
    private readonly float fadeInDuration;
    private readonly Dictionary<uint, Visual> active = new();
    private readonly Dictionary<int, HashSet<uint>> idsByChunk = new();
    private readonly Stack<Visual> pool = new();
    private readonly List<Visual> fading = new();
    private readonly HashSet<uint> incomingIds = new();
    private readonly List<uint> removedIds = new();
    private const int MaxPooledVisuals = 512;

    public MP_FoodClientVisuals(GameObject prefab, Transform parent)
    {
        this.parent = parent;
        source = prefab.GetComponentInChildren<SpriteRenderer>(true);
        MP_Food food = prefab.GetComponent<MP_Food>();
        positionOffset = Vector3.Scale(prefab.transform.localScale, source.transform.localPosition);
        visualScale = Vector3.Scale(prefab.transform.localScale, source.transform.localScale);
        visualRotation = prefab.transform.localRotation * source.transform.localRotation;
        baseColor = source.color;
        fadeInDuration = food != null ? Mathf.Max(0f, food.FadeInDuration) : 0f;
    }

    public void ApplyChunk(int chunkIndex, uint[] ids, Vector2[] positions,
        float[] sizes, bool[] fadeIn)
    {
        if (ids == null || positions == null || sizes == null || fadeIn == null ||
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
                Show(ids[i], positions[i], sizes[i], !firstSnapshot && fadeIn[i]);
        }

        removedIds.Clear();
        foreach (uint id in current)
            if (!incomingIds.Contains(id))
                removedIds.Add(id);

        foreach (uint id in removedIds)
        {
            current.Remove(id);
            if (active.TryGetValue(id, out Visual visual))
            {
                active.Remove(id);
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
            if (!active.TryGetValue(id, out Visual visual))
                continue;

            active.Remove(id);
            Release(visual);
        }

        idsByChunk.Remove(chunkIndex);
    }

    public void Tick(float deltaTime)
    {
        for (int i = fading.Count - 1; i >= 0; i--)
        {
            Visual visual = fading[i];
            visual.Elapsed += deltaTime;
            float progress = Mathf.Clamp01(visual.Elapsed / fadeInDuration);
            SetAlpha(visual, progress);
            if (progress < 1f)
                continue;

            visual.IsFading = false;
            fading.RemoveAt(i);
        }
    }

    public void Dispose()
    {
        foreach (Visual visual in active.Values)
            Object.Destroy(visual.GameObject);
        foreach (Visual visual in pool)
            Object.Destroy(visual.GameObject);
        active.Clear();
        idsByChunk.Clear();
        fading.Clear();
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
        if (animate)
        {
            visual.Elapsed = 0f;
            visual.IsFading = true;
            fading.Add(visual);
        }
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

    private void Release(Visual visual)
    {
        if (visual.IsFading)
        {
            visual.IsFading = false;
            fading.Remove(visual);
        }

        visual.GameObject.SetActive(false);
        if (pool.Count < MaxPooledVisuals)
            pool.Push(visual);
        else
            Object.Destroy(visual.GameObject);
    }
}
