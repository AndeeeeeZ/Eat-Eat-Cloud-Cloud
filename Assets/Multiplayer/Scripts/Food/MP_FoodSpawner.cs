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
        public readonly Vector2 Position;
        public readonly float Radius;

        public FoodRecord(Vector2 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    private sealed class SpawnRegion
    {
        public readonly Rect Bounds;
        public int FoodCount;
        public bool IsQueued;

        public SpawnRegion(Rect bounds)
        {
            Bounds = bounds;
        }
    }

    [SerializeField] private GameObject foodPrefab;
    [SerializeField, Min(1)] private int foodPerGrid = 1;
    [SerializeField, Min(1)] private int maxFoodSpawnsPerFrame = 16;

    [Header("Food Visibility")]
    [SerializeField, Min(0.1f)] private float cameraBaseOrthographicSize = 5f;
    [SerializeField, Min(1f)] private float maximumClientAspectRatio = 3f;
    [SerializeField, Min(0f)] private float visibilityMargin = 4f;
    [SerializeField, Min(0f)] private float visibilityExitPadding = 2f;
    [SerializeField, Min(0.05f)] private float visibilityRefreshInterval = 0.25f;

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
    private readonly Dictionary<PlayerID, HashSet<MP_Food>> nearbyFoodByPlayer = new();
    private readonly HashSet<MP_Food> visibilityCandidates = new();
    private readonly List<SpawnRegion> spawnRegions = new();
    private readonly Queue<int> underfilledRegions = new();
    private MP_CycleTimer cycleTimer;
    private NetworkManager visibilityNetworkManager;
    private List<MP_Food>[] foodByCell;
    private int[] cellRegionIndices;
    private int activeColumns;
    private int activeRows;
    private int activeGroupSize;
    private int indexedFoodPerGrid;
    private Vector2 indexedBoundaryCenter;
    private Vector2 indexedBoundarySize;
    private float largestFoodRadius;
    private double nextFillAttemptAt;
    private bool warnedNoSpawnPosition;
    private double nextVisibilityRefreshAt;

    public static MP_FoodSpawner Instance { get; private set; }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (!asServer)
            return;

        Instance = this;
        visibilityNetworkManager = networkManager;
        visibilityNetworkManager.onPlayerLeft += HandleVisibilityPlayerLeft;

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

        if (foodPrefab.GetComponent<MP_Food>().GetOverrideOrDefault(null) == null)
            Debug.LogWarning("Assign a food visibility Rule Set to the Food Prefab's MP_Food component", this);

        activeColumns = columns > 0 ? columns : 8;
        activeRows = rows > 0 ? rows : 8;
        foodByCell = new List<MP_Food>[activeColumns * activeRows];
        cellRegionIndices = new int[foodByCell.Length];

        cycleTimer = MP_CycleTimer.Instance;
        if (cycleTimer != null)
            cycleTimer.OnCycleChanged += HandleCycleChanged;
    }

    private void Update()
    {
        if (!isServer || !isSpawned)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        if (now >= nextFillAttemptAt)
        {
            int budget = maxFoodSpawnsPerFrame > 0 ? maxFoodSpawnsPerFrame : 16;
            TryFillTargetFood(budget);
        }

        if (now >= nextVisibilityRefreshAt)
        {
            RefreshFoodVisibility();
            nextVisibilityRefreshAt = now + Mathf.Max(0.05f, visibilityRefreshInterval);
        }
    }

    protected override void OnDespawned(bool asServer)
    {
        if (asServer)
        {
            if (cycleTimer != null)
                cycleTimer.OnCycleChanged -= HandleCycleChanged;

            if (visibilityNetworkManager != null)
                visibilityNetworkManager.onPlayerLeft -= HandleVisibilityPlayerLeft;
            visibilityNetworkManager = null;

            cycleTimer = null;
            liveFood.Clear();
            nearbyFoodByPlayer.Clear();
            visibilityCandidates.Clear();
            spawnRegions.Clear();
            underfilledRegions.Clear();
            foodByCell = null;
            cellRegionIndices = null;
            activeColumns = 0;
            activeRows = 0;
            activeGroupSize = 0;
            indexedFoodPerGrid = 0;
            largestFoodRadius = 0f;
            nextFillAttemptAt = 0d;
            warnedNoSpawnPosition = false;
            nextVisibilityRefreshAt = 0d;
            if (Instance == this)
                Instance = null;
        }

        base.OnDespawned(asServer);
    }

    protected override void OnDestroy()
    {
        if (cycleTimer != null)
            cycleTimer.OnCycleChanged -= HandleCycleChanged;

        if (visibilityNetworkManager != null)
            visibilityNetworkManager.onPlayerLeft -= HandleVisibilityPlayerLeft;

        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    public void RespawnFood(MP_Food consumedFood)
    {
        if (!isServer || !UnregisterFood(consumedFood))
            return;

        nextFillAttemptAt = 0d;
        warnedNoSpawnPosition = false;
        TryFillTargetFood(1);
    }

    public bool UnregisterFood(MP_Food food)
    {
        if (!isServer || ReferenceEquals(food, null) || !liveFood.TryGetValue(food, out FoodRecord record))
            return false;

        liveFood.Remove(food);
        if (foodByCell != null && cellRegionIndices != null && spawnRegions.Count > 0)
        {
            int cellIndex = GetCellIndex(record.Position, indexedBoundaryCenter, indexedBoundarySize);
            foodByCell[cellIndex]?.Remove(food);
            int regionIndex = cellRegionIndices[cellIndex];
            spawnRegions[regionIndex].FoodCount--;
            EnqueueUnderfilledRegion(regionIndex);
        }
        return true;
    }

    public bool ShouldPlayerSeeFood(PlayerID player, MP_Food food)
    {
        MP_PlayerManager playerManager = MP_PlayerManager.Instance;
        if (playerManager == null || food == null ||
            !playerManager.Sessions.TryGetValue(player, out MP_PlayerManager.PlayerSession session) ||
            !session.IsAlive)
            return false;

        float radius = GetVisibilityRadius(session.Cloud);
        if (food.IsObserver(player))
            radius += Mathf.Max(0f, visibilityExitPadding);
        radius += food.VisibilityRadius;

        Vector2 offset = food.transform.position - session.Cloud.transform.position;
        return offset.sqrMagnitude <= radius * radius;
    }

    private float GetVisibilityRadius(MP_Player cloud)
    {
        float scale = Mathf.Max(1f, cloud.transform.localScale.x);
        float aspect = Mathf.Max(1f, maximumClientAspectRatio);
        return Mathf.Max(0.1f, cameraBaseOrthographicSize) * scale *
            Mathf.Sqrt(1f + aspect * aspect) + Mathf.Max(0f, visibilityMargin);
    }

    private void RefreshFoodVisibility()
    {
        MP_PlayerManager playerManager = MP_PlayerManager.Instance;
        if (playerManager == null || foodByCell == null || indexedBoundarySize.x <= 0f ||
            indexedBoundarySize.y <= 0f)
            return;

        foreach (KeyValuePair<PlayerID, MP_PlayerManager.PlayerSession> entry in playerManager.Sessions)
        {
            PlayerID player = entry.Key;
            if (!nearbyFoodByPlayer.TryGetValue(player, out HashSet<MP_Food> previous))
            {
                previous = new HashSet<MP_Food>();
                nearbyFoodByPlayer.Add(player, previous);
            }

            visibilityCandidates.Clear();
            if (entry.Value.IsAlive)
            {
                MP_Player cloud = entry.Value.Cloud;
                float radius = GetVisibilityRadius(cloud) + Mathf.Max(0f, visibilityExitPadding) + largestFoodRadius;
                GatherFoodNear(cloud.transform.position, radius, visibilityCandidates);
            }

            foreach (MP_Food food in previous)
                if (food != null && food.isSpawned && !visibilityCandidates.Contains(food) &&
                    food.IsObserver(player))
                    food.EvaluateVisibility(player);

            foreach (MP_Food food in visibilityCandidates)
                if (food != null && food.isSpawned &&
                    food.IsObserver(player) != ShouldPlayerSeeFood(player, food))
                    food.EvaluateVisibility(player);

            previous.Clear();
            previous.UnionWith(visibilityCandidates);
        }
    }

    private void GatherFoodNear(Vector2 position, float radius, HashSet<MP_Food> results)
    {
        Vector2 minimum = indexedBoundaryCenter - indexedBoundarySize * 0.5f;
        float cellWidth = indexedBoundarySize.x / activeColumns;
        float cellHeight = indexedBoundarySize.y / activeRows;
        int firstColumn = Mathf.Clamp(Mathf.FloorToInt((position.x - radius - minimum.x) / cellWidth),
            0, activeColumns - 1);
        int lastColumn = Mathf.Clamp(Mathf.FloorToInt((position.x + radius - minimum.x) / cellWidth),
            0, activeColumns - 1);
        int firstRow = Mathf.Clamp(Mathf.FloorToInt((position.y - radius - minimum.y) / cellHeight),
            0, activeRows - 1);
        int lastRow = Mathf.Clamp(Mathf.FloorToInt((position.y + radius - minimum.y) / cellHeight),
            0, activeRows - 1);

        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                List<MP_Food> cell = foodByCell[row * activeColumns + column];
                if (cell == null)
                    continue;

                foreach (MP_Food food in cell)
                    if (food != null)
                        results.Add(food);
            }
        }
    }

    private void HandleVisibilityPlayerLeft(PlayerID player, bool asServer)
    {
        if (asServer)
            nearbyFoodByPlayer.Remove(player);
    }

    private void HandleCycleChanged(int cycle)
    {
        nextFillAttemptAt = 0d;
        warnedNoSpawnPosition = false;
        TryFillTargetFood(0);
    }

    private void TryFillTargetFood(int maxSpawns)
    {
        if (!isServer || foodByCell == null)
            return;

        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary == null || boundary.Size.x <= 0f || boundary.Size.y <= 0f)
            return;

        EnsureSpawnRegions(boundary);
        int targetCount = spawnRegions.Count * Mathf.Max(1, foodPerGrid);
        for (int spawned = 0; spawned < maxSpawns && liveFood.Count < targetCount; spawned++)
        {
            if (!TrySpawnFood(boundary))
            {
                if (!warnedNoSpawnPosition)
                {
                    Debug.LogWarning("No valid food position is available in the current grid", this);
                    warnedNoSpawnPosition = true;
                }

                nextFillAttemptAt = Time.realtimeSinceStartupAsDouble + 2d;
                return;
            }
        }

        if (liveFood.Count >= targetCount)
            warnedNoSpawnPosition = false;
    }

    private void EnsureSpawnRegions(MP_GameBoundary boundary)
    {
        int groupSize = GetGroupSize(GetCurrentCycle());
        if (spawnRegions.Count > 0 && activeGroupSize == groupSize &&
            indexedFoodPerGrid == Mathf.Max(1, foodPerGrid) &&
            indexedBoundaryCenter == boundary.Center && indexedBoundarySize == boundary.Size)
            return;

        activeGroupSize = groupSize;
        indexedFoodPerGrid = Mathf.Max(1, foodPerGrid);
        indexedBoundaryCenter = boundary.Center;
        indexedBoundarySize = boundary.Size;
        spawnRegions.Clear();
        underfilledRegions.Clear();

        int regionColumns = 1 + (activeColumns - 1) / groupSize;
        for (int row = 0; row < activeRows; row += groupSize)
        {
            for (int column = 0; column < activeColumns; column += groupSize)
            {
                Rect bounds = GetRegionBounds(boundary, column, row,
                    Mathf.Min(column + groupSize, activeColumns),
                    Mathf.Min(row + groupSize, activeRows), activeColumns, activeRows);
                spawnRegions.Add(new SpawnRegion(bounds));
            }
        }

        for (int row = 0; row < activeRows; row++)
            for (int column = 0; column < activeColumns; column++)
                cellRegionIndices[row * activeColumns + column] =
                    (row / groupSize) * regionColumns + column / groupSize;

        foreach (List<MP_Food> cell in foodByCell)
            cell?.Clear();

        foreach (KeyValuePair<MP_Food, FoodRecord> entry in liveFood)
        {
            FoodRecord record = entry.Value;
            int cellIndex = GetCellIndex(record.Position, indexedBoundaryCenter, indexedBoundarySize);
            AddFoodToCell(entry.Key, cellIndex);
            spawnRegions[cellRegionIndices[cellIndex]].FoodCount++;
        }

        for (int i = 0; i < spawnRegions.Count; i++)
            EnqueueUnderfilledRegion(i);
    }

    private void EnqueueUnderfilledRegion(int index)
    {
        SpawnRegion region = spawnRegions[index];
        if (region.IsQueued || region.FoodCount >= Mathf.Max(1, foodPerGrid))
            return;

        region.IsQueued = true;
        underfilledRegions.Enqueue(index);
    }

    private void AddFoodToCell(MP_Food food, int cellIndex)
    {
        foodByCell[cellIndex] ??= new List<MP_Food>();
        foodByCell[cellIndex].Add(food);
    }

    private bool TrySpawnFood(MP_GameBoundary boundary)
    {
        if (foodPrefab == null)
            return false;

        MP_CycleTimer timer = cycleTimer != null ? cycleTimer : MP_CycleTimer.Instance;
        float sizeMultiplier = timer != null ? timer.FoodSizeMultiplier : 1f;
        float radius = GetFoodRadius(sizeMultiplier);

        int attempts = placementAttemptsPerRegion > 0 ? placementAttemptsPerRegion : 8;
        int regionsToTry = underfilledRegions.Count;
        for (int regionAttempt = 0; regionAttempt < regionsToTry; regionAttempt++)
        {
            int regionIndex = underfilledRegions.Dequeue();
            SpawnRegion region = spawnRegions[regionIndex];
            region.IsQueued = false;
            if (region.FoodCount >= Mathf.Max(1, foodPerGrid))
                continue;

            if (region.Bounds.width < radius * 2f || region.Bounds.height < radius * 2f)
            {
                EnqueueUnderfilledRegion(regionIndex);
                continue;
            }

            for (int i = 0; i < attempts; i++)
            {
                Vector2 position = new(
                    UnityEngine.Random.Range(region.Bounds.xMin + radius, region.Bounds.xMax - radius),
                    UnityEngine.Random.Range(region.Bounds.yMin + radius, region.Bounds.yMax - radius)
                );

                if (!IsClear(position, radius, boundary))
                    continue;

                GameObject foodObject = Instantiate(foodPrefab, position, Quaternion.identity, transform);
                MP_Food food = foodObject.GetComponent<MP_Food>();
                if (food == null)
                {
                    Debug.LogError($"Missing MP_Food on {foodObject.name}", this);
                    Destroy(foodObject);
                    EnqueueUnderfilledRegion(regionIndex);
                    return false;
                }

                food.Initialize(this, sizeMultiplier);
                int cellIndex = GetCellIndex(position, indexedBoundaryCenter, indexedBoundarySize);
                liveFood.Add(food, new FoodRecord(position, radius));
                AddFoodToCell(food, cellIndex);
                spawnRegions[cellRegionIndices[cellIndex]].FoodCount++;
                largestFoodRadius = Mathf.Max(largestFoodRadius, radius);
                EnqueueUnderfilledRegion(regionIndex);
                return true;
            }

            EnqueueUnderfilledRegion(regionIndex);
        }

        return false;
    }

    private int GetCellIndex(Vector2 position, Vector2 center, Vector2 size)
    {
        Vector2 minimum = center - size * 0.5f;
        int column = Mathf.Clamp(Mathf.FloorToInt((position.x - minimum.x) / size.x * activeColumns),
            0, activeColumns - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt((position.y - minimum.y) / size.y * activeRows),
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

    private bool IsClear(Vector2 position, float radius, MP_GameBoundary boundary)
    {
        float clearance = Mathf.Max(0f, foodClearance);
        float searchRadius = radius + largestFoodRadius + clearance;
        Vector2 minimum = boundary.Center - boundary.Size * 0.5f;
        float cellWidth = boundary.Size.x / activeColumns;
        float cellHeight = boundary.Size.y / activeRows;
        int firstColumn = Mathf.Clamp(Mathf.FloorToInt((position.x - searchRadius - minimum.x) / cellWidth),
            0, activeColumns - 1);
        int lastColumn = Mathf.Clamp(Mathf.FloorToInt((position.x + searchRadius - minimum.x) / cellWidth),
            0, activeColumns - 1);
        int firstRow = Mathf.Clamp(Mathf.FloorToInt((position.y - searchRadius - minimum.y) / cellHeight),
            0, activeRows - 1);
        int lastRow = Mathf.Clamp(Mathf.FloorToInt((position.y + searchRadius - minimum.y) / cellHeight),
            0, activeRows - 1);

        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                List<MP_Food> cell = foodByCell[row * activeColumns + column];
                if (cell == null)
                    continue;

                foreach (MP_Food food in cell)
                {
                    FoodRecord record = liveFood[food];
                    float separation = radius + record.Radius + clearance;
                    if ((position - record.Position).sqrMagnitude < separation * separation)
                        return false;
                }
            }
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
