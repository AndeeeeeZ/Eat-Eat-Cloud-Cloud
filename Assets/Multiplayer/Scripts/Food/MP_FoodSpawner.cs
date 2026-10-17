using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

public class MP_FoodSpawner : NetworkBehaviour
{
    [Serializable]
    private struct MergeStage
    {
        [Min(1)] public int firstCycle;
        [Min(1)] public int cellsPerSide;

        public MergeStage(int firstCycle, int cellsPerSide)
        {
            this.firstCycle = firstCycle;
            this.cellsPerSide = cellsPerSide;
        }
    }

    private readonly struct FoodRecord
    {
        public readonly int CellIndex;
        public readonly Vector2 Position;
        public readonly float Radius;

        public FoodRecord(int cellIndex, Vector2 position, float radius)
        {
            CellIndex = cellIndex;
            Position = position;
            Radius = radius;
        }
    }

    private readonly struct SpawnRegion
    {
        public readonly Rect Bounds;
        public readonly float Density;
        public readonly float TieBreaker;

        public SpawnRegion(Rect bounds, float density)
        {
            Bounds = bounds;
            Density = density;
            TieBreaker = UnityEngine.Random.value;
        }
    }

    [SerializeField] private GameObject foodPrefab;
    [SerializeField, Min(0)] private int startingFoodCount = 100;

    [Header("Food Grid")]
    [SerializeField, Min(1)] private int columns = 8;
    [SerializeField, Min(1)] private int rows = 8;
    [SerializeField, Min(1)] private int placementAttemptsPerRegion = 8;
    [SerializeField, Min(0f)] private float foodClearance = 0.1f;
    [SerializeField] private MergeStage[] mergeStages =
    {
        new(1, 1),
        new(4, 2),
        new(7, 4)
    };

    [Header("Grid Gizmos")]
    [SerializeField] private bool hideGridGizmos;
    [SerializeField, Min(1)] private int previewCycle = 1;
    [SerializeField] private Color baseGridColor = new(1f, 0.2f, 0.8f, 1f);
    [SerializeField] private Color mergedRegionColor = new(1f, 0.65f, 0.2f, 1f);

    private readonly Dictionary<MP_Food, FoodRecord> liveFood = new();
    private readonly List<SpawnRegion> spawnRegions = new();
    private MP_CycleTimer cycleTimer;
    private int[] cellFoodCounts;
    private int activeColumns;
    private int activeRows;
    private int pendingFoodCount;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (!isServer)
            return;

        if (MP_GameBoundary.Instance == null)
        {
            Debug.LogError("Add MP_GameBoundary to the scene before spawning food", this);
            return;
        }

        if (foodPrefab == null || foodPrefab.GetComponent<MP_Food>() == null ||
            foodPrefab.GetComponent<CircleCollider2D>() == null)
        {
            Debug.LogError("Assign a food prefab with MP_Food and CircleCollider2D", this);
            return;
        }

        activeColumns = columns > 0 ? columns : 8;
        activeRows = rows > 0 ? rows : 8;
        cellFoodCounts = new int[activeColumns * activeRows];
        pendingFoodCount = Mathf.Max(0, startingFoodCount);

        cycleTimer = MP_CycleTimer.Instance;
        if (cycleTimer != null)
            cycleTimer.OnCycleChanged += HandleCycleChanged;

        TryFillPendingFood();
    }

    protected override void OnDespawned(bool asServer)
    {
        if (asServer)
        {
            if (cycleTimer != null)
                cycleTimer.OnCycleChanged -= HandleCycleChanged;

            cycleTimer = null;
            liveFood.Clear();
            spawnRegions.Clear();
            cellFoodCounts = null;
            pendingFoodCount = 0;
            activeColumns = 0;
            activeRows = 0;
        }

        base.OnDespawned(asServer);
    }

    protected override void OnDestroy()
    {
        if (cycleTimer != null)
            cycleTimer.OnCycleChanged -= HandleCycleChanged;

        base.OnDestroy();
    }

    public void RespawnFood(MP_Food consumedFood)
    {
        if (!isServer || !UnregisterFood(consumedFood))
            return;

        pendingFoodCount++;
        TryFillPendingFood();
    }

    public bool UnregisterFood(MP_Food food)
    {
        if (!isServer || ReferenceEquals(food, null) || !liveFood.TryGetValue(food, out FoodRecord record))
            return false;

        liveFood.Remove(food);
        if (cellFoodCounts != null && record.CellIndex < cellFoodCounts.Length)
            cellFoodCounts[record.CellIndex]--;
        return true;
    }

    private void HandleCycleChanged(int cycle)
    {
        TryFillPendingFood();
    }

    private void TryFillPendingFood()
    {
        if (!isServer || cellFoodCounts == null)
            return;

        while (pendingFoodCount > 0)
        {
            if (!TrySpawnFood())
            {
                Debug.LogWarning("No valid food position is available in the current grid", this);
                return;
            }

            pendingFoodCount--;
        }
    }

    private bool TrySpawnFood()
    {
        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary == null || foodPrefab == null)
            return false;

        MP_CycleTimer timer = cycleTimer != null ? cycleTimer : MP_CycleTimer.Instance;
        float sizeMultiplier = timer != null ? timer.FoodSizeMultiplier : 1f;
        float radius = GetFoodRadius(sizeMultiplier);
        BuildSpawnRegions(boundary, radius);

        int attempts = placementAttemptsPerRegion > 0 ? placementAttemptsPerRegion : 8;
        foreach (SpawnRegion region in spawnRegions)
        {
            for (int i = 0; i < attempts; i++)
            {
                Vector2 position = new(
                    UnityEngine.Random.Range(region.Bounds.xMin + radius, region.Bounds.xMax - radius),
                    UnityEngine.Random.Range(region.Bounds.yMin + radius, region.Bounds.yMax - radius)
                );

                if (!IsClear(position, radius))
                    continue;

                GameObject foodObject = Instantiate(foodPrefab, position, Quaternion.identity, transform);
                MP_Food food = foodObject.GetComponent<MP_Food>();
                if (food == null)
                {
                    Debug.LogError($"Missing MP_Food on {foodObject.name}", this);
                    Destroy(foodObject);
                    return false;
                }

                food.Initialize(this, sizeMultiplier);
                int cellIndex = GetCellIndex(position, boundary);
                liveFood.Add(food, new FoodRecord(cellIndex, position, radius));
                cellFoodCounts[cellIndex]++;
                return true;
            }
        }

        return false;
    }

    private void BuildSpawnRegions(MP_GameBoundary boundary, float radius)
    {
        spawnRegions.Clear();
        int groupSize = GetGroupSize(GetCurrentCycle());
        for (int row = 0; row < activeRows; row += groupSize)
        {
            for (int column = 0; column < activeColumns; column += groupSize)
            {
                int endRow = Mathf.Min(row + groupSize, activeRows);
                int endColumn = Mathf.Min(column + groupSize, activeColumns);
                Rect bounds = GetRegionBounds(boundary, column, row, endColumn, endRow,
                    activeColumns, activeRows);
                if (bounds.width < radius * 2f || bounds.height < radius * 2f)
                    continue;

                int foodCount = 0;
                for (int y = row; y < endRow; y++)
                    for (int x = column; x < endColumn; x++)
                        foodCount += cellFoodCounts[y * activeColumns + x];

                spawnRegions.Add(new SpawnRegion(bounds, foodCount / (bounds.width * bounds.height)));
            }
        }

        spawnRegions.Sort(CompareRegions);
    }

    private static int CompareRegions(SpawnRegion a, SpawnRegion b)
    {
        int byDensity = a.Density.CompareTo(b.Density);
        return byDensity != 0 ? byDensity : a.TieBreaker.CompareTo(b.TieBreaker);
    }

    private int GetCellIndex(Vector2 position, MP_GameBoundary boundary)
    {
        Vector2 minimum = boundary.Center - boundary.Size * 0.5f;
        int column = Mathf.Clamp(Mathf.FloorToInt((position.x - minimum.x) / boundary.Size.x * activeColumns),
            0, activeColumns - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt((position.y - minimum.y) / boundary.Size.y * activeRows),
            0, activeRows - 1);
        return row * activeColumns + column;
    }

    private float GetFoodRadius(float sizeMultiplier)
    {
        CircleCollider2D collider = foodPrefab.GetComponent<CircleCollider2D>();
        Vector3 scale = foodPrefab.transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return (collider.radius + collider.offset.magnitude) * largestScale * sizeMultiplier;
    }

    private bool IsClear(Vector2 position, float radius)
    {
        float clearance = Mathf.Max(0f, foodClearance);
        foreach (FoodRecord record in liveFood.Values)
        {
            float separation = radius + record.Radius + clearance;
            if ((position - record.Position).sqrMagnitude < separation * separation)
                return false;
        }

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager == null)
            return true;

        foreach (MP_PlayerStats stats in manager.Players)
        {
            if (stats == null)
                continue;

            CircleCollider2D body = stats.GetComponent<CircleCollider2D>();
            if (body == null || !body.enabled)
                continue;

            Vector3 scale = body.transform.lossyScale;
            float playerRadius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            Vector2 center = body.transform.TransformPoint(body.offset);
            float separation = radius + playerRadius + clearance;
            if ((position - center).sqrMagnitude < separation * separation)
                return false;
        }

        return true;
    }

    private int GetCurrentCycle()
    {
        MP_CycleTimer timer = cycleTimer != null ? cycleTimer : MP_CycleTimer.Instance;
        return timer != null ? Mathf.Max(1, timer.CurrentCycle) : 1;
    }

    private int GetGroupSize(int cycle)
    {
        if (mergeStages == null || mergeStages.Length == 0)
            return cycle >= 7 ? 4 : cycle >= 4 ? 2 : 1;

        int groupSize = 1;
        int selectedCycle = 0;
        foreach (MergeStage stage in mergeStages)
        {
            int firstCycle = Mathf.Max(1, stage.firstCycle);
            if (firstCycle > cycle || firstCycle < selectedCycle)
                continue;

            selectedCycle = firstCycle;
            groupSize = Mathf.Max(1, stage.cellsPerSide);
        }

        return groupSize;
    }

    private static Rect GetRegionBounds(MP_GameBoundary boundary, int startColumn, int startRow,
        int endColumn, int endRow, int columnCount, int rowCount)
    {
        Vector2 size = boundary.Size;
        Vector2 minimum = boundary.Center - size * 0.5f;
        float cellWidth = size.x / columnCount;
        float cellHeight = size.y / rowCount;
        return new Rect(minimum.x + startColumn * cellWidth, minimum.y + startRow * cellHeight,
            (endColumn - startColumn) * cellWidth, (endRow - startRow) * cellHeight);
    }

    private void OnDrawGizmos()
    {
        if (hideGridGizmos)
            return;

        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary == null)
            boundary = FindFirstObjectByType<MP_GameBoundary>();
        if (boundary == null || boundary.Size.x <= 0f || boundary.Size.y <= 0f)
            return;

        int columnCount = activeColumns > 0 ? activeColumns : columns > 0 ? columns : 8;
        int rowCount = activeRows > 0 ? activeRows : rows > 0 ? rows : 8;
        Vector2 minimum = boundary.Center - boundary.Size * 0.5f;
        float z = boundary.transform.position.z;

        Gizmos.color = baseGridColor.a > 0f ? baseGridColor : new Color(1f, 0.2f, 0.8f, 1f);
        Gizmos.DrawWireCube(new Vector3(boundary.Center.x, boundary.Center.y, z),
            new Vector3(boundary.Size.x, boundary.Size.y, 0f));
        for (int column = 1; column < columnCount; column++)
        {
            float x = minimum.x + boundary.Size.x * column / columnCount;
            Gizmos.DrawLine(new Vector3(x, minimum.y, z), new Vector3(x, minimum.y + boundary.Size.y, z));
        }

        for (int row = 1; row < rowCount; row++)
        {
            float y = minimum.y + boundary.Size.y * row / rowCount;
            Gizmos.DrawLine(new Vector3(minimum.x, y, z), new Vector3(minimum.x + boundary.Size.x, y, z));
        }

        int cycle = Application.isPlaying ? GetCurrentCycle() : Mathf.Max(1, previewCycle);
        int groupSize = GetGroupSize(cycle);
        if (groupSize <= 1)
            return;

        Gizmos.color = mergedRegionColor.a > 0f ? mergedRegionColor : new Color(1f, 0.65f, 0.2f, 1f);
        for (int row = 0; row < rowCount; row += groupSize)
        {
            for (int column = 0; column < columnCount; column += groupSize)
            {
                Rect bounds = GetRegionBounds(boundary, column, row,
                    Mathf.Min(column + groupSize, columnCount), Mathf.Min(row + groupSize, rowCount),
                    columnCount, rowCount);
                Gizmos.DrawWireCube(new Vector3(bounds.center.x, bounds.center.y, z),
                    new Vector3(bounds.width, bounds.height, 0f));
            }
        }
    }
}
