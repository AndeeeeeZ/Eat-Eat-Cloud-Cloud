using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using UnityEngine.Serialization;

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
        public readonly uint Id;
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly Vector2 HitCenter;
        public readonly float HitRadius;
        public readonly int SpawnCycle;
        public readonly float SizeMultiplier;
        public readonly float Experience;
        public readonly double EdibleAt;
        public readonly bool FadeInOnSpawn;
        public readonly int CellIndex;

        public FoodRecord(uint id, Vector2 position, float radius, Vector2 hitCenter,
            float hitRadius, int spawnCycle, float sizeMultiplier, float experience,
            double edibleAt, bool fadeInOnSpawn, int cellIndex)
        {
            Id = id;
            Position = position;
            Radius = radius;
            HitCenter = hitCenter;
            HitRadius = hitRadius;
            SpawnCycle = spawnCycle;
            SizeMultiplier = sizeMultiplier;
            Experience = experience;
            EdibleAt = edibleAt;
            FadeInOnSpawn = fadeInOnSpawn;
            CellIndex = cellIndex;
        }

        public FoodRecord WithCellIndex(int cellIndex) =>
            new(Id, Position, Radius, HitCenter, HitRadius, SpawnCycle, SizeMultiplier,
                Experience, EdibleAt, FadeInOnSpawn, cellIndex);

        public FoodRecord WithCycle(int cycle, float sizeMultiplier, float experience,
            float baseRadius, Vector2 baseHitOffset, float baseHitRadius, double edibleAt) =>
            new(Id, Position, baseRadius * sizeMultiplier,
                Position + baseHitOffset * sizeMultiplier, baseHitRadius * sizeMultiplier,
                cycle, sizeMultiplier, experience, edibleAt, FadeInOnSpawn, CellIndex);
    }

    private sealed class SpawnRegion
    {
        public readonly Rect Bounds;
        public int FoodCount;
        public int InitialRemaining;
        public int BucketPosition;
        public bool IsQueued;

        public SpawnRegion(Rect bounds)
        {
            Bounds = bounds;
        }
    }

    [SerializeField] private GameObject foodPrefab;
    [FormerlySerializedAs("foodPerGrid")]
    [SerializeField, Min(1)] private int maxFoodPerGrid = 1;
    [SerializeField, Min(1)] private int maxFoodSpawnsPerFrame = 16;
    [FormerlySerializedAs("outdatedFoodDespawnsPerSecond")]
    [SerializeField, Min(1)] private int outdatedFoodTransitionsPerSecond = 50;

    [Header("Food Visibility")]
    [SerializeField, Min(0.1f)] private float cameraBaseOrthographicSize = 5f;
    [SerializeField, Min(1f)] private float maximumClientAspectRatio = 3f;
    [SerializeField, Min(0f)] private float visibilityPadding = 1.5f;
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

    private readonly Dictionary<uint, FoodRecord> foodRecords = new();
    private readonly List<uint> reindexFoodIds = new();
    private readonly Dictionary<PlayerID, HashSet<int>> nearbyChunksByPlayer = new();
    private readonly Dictionary<PlayerID, float> clientAspectRatios = new();
    private readonly HashSet<PlayerID> readyPlayers = new();
    private readonly HashSet<int> visibilityCandidates = new();
    private readonly HashSet<int> dirtyChunks = new();
    private readonly Dictionary<int, (uint[] ids, Vector2[] positions, float[] sizes, bool[] fadeIn, uint[] retiredIds)> snapshotCache = new();
    private readonly Dictionary<int, HashSet<uint>> retiredIdsByChunk = new();
    private readonly Dictionary<int, uint> receivedChunkVersions = new();
    private readonly List<SpawnRegion> spawnRegions = new();
    private readonly List<List<int>> regionsByFoodCount = new();
    private readonly Queue<int> initialRegions = new();
    private readonly Queue<Vector2> pendingRespawns = new();
    private readonly Queue<uint> retiringFood = new();
    private readonly List<int> redistributionCandidates = new();
    private MP_CycleTimer cycleTimer;
    private NetworkManager visibilityNetworkManager;
    private List<uint>[] foodRecordIdsByCell;
    private List<uint>[] foodIdsByChunk;
    private uint[] chunkVersions;
    private int chunkColumns;
    private int chunkRows;
    private MP_FoodClientVisuals clientVisuals;
    private int[] cellRegionIndices;
    private uint nextFoodId = 1;
    private int activeColumns;
    private int activeRows;
    private int activeGroupSize;
    private int indexedMaxFoodPerGrid;
    private Vector2 indexedBoundaryCenter;
    private Vector2 indexedBoundarySize;
    private float largestFoodRadius;
    private float baseFoodRadius;
    private float baseFoodHitRadius;
    private Vector2 baseFoodHitOffset;
    private float baseFoodExperience;
    private bool initialDistributionStarted;
    private double nextFillAttemptAt;
    private double outdatedFoodTransitionAllowance;
    private double lastOutdatedFoodTransitionBudgetAt;
    private bool warnedNoSpawnPosition;
    private double nextVisibilityRefreshAt;
    private double nextAspectCheckAt;
    private float lastReportedAspect;
    private const int CellsPerChunk = 4;
    private const int FailedRegionChecksPerFill = 64;
    private const double FailedFillRetrySeconds = 1d;

    public static MP_FoodSpawner Instance { get; private set; }
    public int ActiveFoodCount => foodRecords.Count;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        Instance = this;

        if (!asServer)
        {
            if (!Application.isBatchMode && foodPrefab != null &&
                foodPrefab.GetComponentInChildren<SpriteRenderer>(true) != null)
                clientVisuals = new MP_FoodClientVisuals(foodPrefab, transform);
            lastReportedAspect = GetLocalCameraAspect();
            RequestFoodChunks(lastReportedAspect);
            return;
        }

        visibilityNetworkManager = networkManager;
        visibilityNetworkManager.onPlayerLeft += HandleVisibilityPlayerLeft;

        if (MP_GameBoundary.Instance == null)
        {
            Debug.LogError("Add MP_GameBoundary to the scene before spawning food", this);
            return;
        }

        MP_Food foodComponent = foodPrefab != null ? foodPrefab.GetComponent<MP_Food>() : null;
        CircleCollider2D foodCollider = foodPrefab != null ? foodPrefab.GetComponent<CircleCollider2D>() : null;
        SpriteRenderer foodSprite = foodPrefab != null ?
            foodPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (foodComponent == null || foodCollider == null || foodSprite == null)
        {
            Debug.LogError("Assign a food prefab with MP_Food, CircleCollider2D, and SpriteRenderer", this);
            return;
        }

        Vector3 prefabScale = foodPrefab.transform.lossyScale;
        baseFoodRadius = (foodCollider.radius + foodCollider.offset.magnitude) *
            Mathf.Max(Mathf.Abs(prefabScale.x), Mathf.Abs(prefabScale.y));
        baseFoodHitRadius = foodCollider.radius *
            Mathf.Max(Mathf.Abs(prefabScale.x), Mathf.Abs(prefabScale.y));
        baseFoodHitOffset = new Vector2(foodCollider.offset.x * prefabScale.x,
            foodCollider.offset.y * prefabScale.y);
        baseFoodExperience = foodComponent.BaseExperience;

        activeColumns = columns > 0 ? columns : 8;
        activeRows = rows > 0 ? rows : 8;
        foodRecordIdsByCell = new List<uint>[activeColumns * activeRows];
        cellRegionIndices = new int[foodRecordIdsByCell.Length];
        chunkColumns = (activeColumns + CellsPerChunk - 1) / CellsPerChunk;
        chunkRows = (activeRows + CellsPerChunk - 1) / CellsPerChunk;
        foodIdsByChunk = new List<uint>[chunkColumns * chunkRows];
        chunkVersions = new uint[foodIdsByChunk.Length];

        cycleTimer = MP_CycleTimer.Instance;
        if (cycleTimer != null)
            cycleTimer.OnCycleChanged += HandleCycleChanged;
    }

    private void Update()
    {
        if (isClient && clientVisuals != null)
            clientVisuals.Tick(Time.deltaTime);

        if (isClient && isSpawned && Time.realtimeSinceStartupAsDouble >= nextAspectCheckAt)
        {
            nextAspectCheckAt = Time.realtimeSinceStartupAsDouble +
                Mathf.Max(0.05f, visibilityRefreshInterval);
            float aspect = GetLocalCameraAspect();
            if (Mathf.Abs(aspect - lastReportedAspect) > 0.01f)
            {
                lastReportedAspect = aspect;
                RequestFoodChunks(aspect);
            }
        }

        if (!isServer || !isSpawned)
            return;

        if (cycleTimer == null && MP_CycleTimer.Instance != null)
        {
            cycleTimer = MP_CycleTimer.Instance;
            cycleTimer.OnCycleChanged += HandleCycleChanged;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        int retirementBudget = maxFoodSpawnsPerFrame > 0 ? maxFoodSpawnsPerFrame : 16;
        RefreshOutdatedFood(now, retirementBudget);

        if (now >= nextFillAttemptAt)
        {
            int budget = maxFoodSpawnsPerFrame > 0 ? maxFoodSpawnsPerFrame : 16;
            TryFillTargetFood(budget);
        }

        if (now >= nextVisibilityRefreshAt)
        {
            RefreshFoodChunks();
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
            foodRecords.Clear();
            reindexFoodIds.Clear();
            nearbyChunksByPlayer.Clear();
            clientAspectRatios.Clear();
            readyPlayers.Clear();
            visibilityCandidates.Clear();
            dirtyChunks.Clear();
            snapshotCache.Clear();
            retiredIdsByChunk.Clear();
            spawnRegions.Clear();
            regionsByFoodCount.Clear();
            initialRegions.Clear();
            pendingRespawns.Clear();
            retiringFood.Clear();
            redistributionCandidates.Clear();
            foodRecordIdsByCell = null;
            foodIdsByChunk = null;
            chunkVersions = null;
            chunkColumns = 0;
            chunkRows = 0;
            cellRegionIndices = null;
            nextFoodId = 1;
            activeColumns = 0;
            activeRows = 0;
            activeGroupSize = 0;
            indexedMaxFoodPerGrid = 0;
            largestFoodRadius = 0f;
            baseFoodRadius = 0f;
            baseFoodHitRadius = 0f;
            baseFoodHitOffset = default;
            baseFoodExperience = 0f;
            initialDistributionStarted = false;
            nextFillAttemptAt = 0d;
            outdatedFoodTransitionAllowance = 0d;
            lastOutdatedFoodTransitionBudgetAt = 0d;
            warnedNoSpawnPosition = false;
            nextVisibilityRefreshAt = 0d;
        }

        if (!asServer)
        {
            clientVisuals?.Dispose();
            clientVisuals = null;
            receivedChunkVersions.Clear();
            nextAspectCheckAt = 0d;
            lastReportedAspect = 0f;
        }

        if (Instance == this)
            Instance = null;

        base.OnDespawned(asServer);
    }

    protected override void OnDestroy()
    {
        if (cycleTimer != null)
            cycleTimer.OnCycleChanged -= HandleCycleChanged;

        if (visibilityNetworkManager != null)
            visibilityNetworkManager.onPlayerLeft -= HandleVisibilityPlayerLeft;

        clientVisuals?.Dispose();
        clientVisuals = null;

        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    private void QueueReplacement(FoodRecord record)
    {
        if ((long)foodRecords.Count + pendingRespawns.Count >=
            (long)spawnRegions.Count * indexedMaxFoodPerGrid)
            return;

        spawnRegions[cellRegionIndices[record.CellIndex]].InitialRemaining = 0;
        pendingRespawns.Enqueue(record.Position);
        nextFillAttemptAt = 0d;
        warnedNoSpawnPosition = false;
    }

    private void RemoveFoodRecord(FoodRecord record, bool fadeOut = false)
    {
        foodRecords.Remove(record.Id);
        if (foodRecordIdsByCell == null || cellRegionIndices == null || spawnRegions.Count == 0)
            return;

        foodRecordIdsByCell[record.CellIndex]?.Remove(record.Id);
        int chunkIndex = GetChunkIndex(record.CellIndex);
        foodIdsByChunk[chunkIndex]?.Remove(record.Id);
        if (fadeOut)
        {
            if (!retiredIdsByChunk.TryGetValue(chunkIndex, out HashSet<uint> retiredIds))
            {
                retiredIds = new HashSet<uint>();
                retiredIdsByChunk.Add(chunkIndex, retiredIds);
            }

            retiredIds.Add(record.Id);
        }
        MarkChunkDirty(chunkIndex);
        nextVisibilityRefreshAt = 0d;
        ChangeRegionFoodCount(cellRegionIndices[record.CellIndex], -1);
    }

    public void ConsumeFoodAlongPath(MP_Player player, MP_PlayerGrowth growth,
        CircleCollider2D body, Vector2 from, Vector2 to)
    {
        if (!isServer || !isSpawned || player == null || !player.IsAlive ||
            growth == null || body == null || !body.enabled || foodRecordIdsByCell == null ||
            indexedBoundarySize.x <= 0f || indexedBoundarySize.y <= 0f)
            return;

        Vector3 scale = body.transform.lossyScale;
        float playerRadius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        Vector2 centerOffset = (Vector2)body.transform.TransformPoint(body.offset) -
            (Vector2)player.transform.position;
        Vector2 start = from + centerOffset;
        Vector2 end = to + centerOffset;
        // Sweep across the movement path so fast players cannot skip food
        float searchRadius = playerRadius + largestFoodRadius;
        Vector2 minimum = indexedBoundaryCenter - indexedBoundarySize * 0.5f;
        float cellWidth = indexedBoundarySize.x / activeColumns;
        float cellHeight = indexedBoundarySize.y / activeRows;
        int firstColumn = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(start.x, end.x) -
            searchRadius - minimum.x) / cellWidth), 0, activeColumns - 1);
        int lastColumn = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(start.x, end.x) +
            searchRadius - minimum.x) / cellWidth), 0, activeColumns - 1);
        int firstRow = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(start.y, end.y) -
            searchRadius - minimum.y) / cellHeight), 0, activeRows - 1);
        int lastRow = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(start.y, end.y) +
            searchRadius - minimum.y) / cellHeight), 0, activeRows - 1);
        Vector2 movement = end - start;
        float movementLengthSquared = movement.sqrMagnitude;
        double now = Time.realtimeSinceStartupAsDouble;

        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                List<uint> cell = foodRecordIdsByCell[row * activeColumns + column];
                if (cell == null)
                    continue;

                for (int i = cell.Count - 1; i >= 0; i--)
                {
                    FoodRecord record = foodRecords[cell[i]];
                    if (now < record.EdibleAt)
                        continue;

                    float progress = movementLengthSquared > 0f ?
                        Mathf.Clamp01(Vector2.Dot(record.HitCenter - start, movement) /
                            movementLengthSquared) : 0f;
                    Vector2 nearest = start + movement * progress;
                    float reach = playerRadius + record.HitRadius;
                    if ((record.HitCenter - nearest).sqrMagnitude > reach * reach)
                        continue;

                    RemoveFoodRecord(record);
                    QueueReplacement(record);
                    growth.GainExperience(record.Experience);
                }
            }
        }
    }

    private static float GetLocalCameraAspect()
    {
        Camera camera = Camera.main;
        return camera != null ? camera.aspect :
            Screen.width / (float)Mathf.Max(1, Screen.height);
    }

    private float GetClientAspect(PlayerID player)
    {
        return clientAspectRatios.TryGetValue(player, out float aspect) ? aspect :
            Mathf.Max(1f, maximumClientAspectRatio);
    }

    private Vector2 GetViewExtents(PlayerID player, MP_Player cloud, float padding)
    {
        float scale = Mathf.Max(1f, cloud.transform.localScale.x);
        float halfHeight = Mathf.Max(0.1f, cameraBaseOrthographicSize) * scale;
        return new Vector2(halfHeight * GetClientAspect(player) + padding,
            halfHeight + padding);
    }

    private void RefreshFoodChunks()
    {
        MP_PlayerManager playerManager = MP_PlayerManager.Instance;
        if (playerManager == null || foodIdsByChunk == null || indexedBoundarySize.x <= 0f ||
            indexedBoundarySize.y <= 0f)
            return;

        snapshotCache.Clear();
        foreach (KeyValuePair<PlayerID, MP_PlayerManager.PlayerSession> entry in playerManager.Sessions)
        {
            PlayerID player = entry.Key;
            if (!readyPlayers.Contains(player))
                continue;

            if (!nearbyChunksByPlayer.TryGetValue(player, out HashSet<int> previous))
            {
                previous = new HashSet<int>();
                nearbyChunksByPlayer.Add(player, previous);
            }

            visibilityCandidates.Clear();
            if (entry.Value.IsAlive)
            {
                MP_Player cloud = entry.Value.Cloud;
                float padding = Mathf.Max(0f, visibilityPadding) + largestFoodRadius;
                Vector2 extents = GetViewExtents(player, cloud, padding);
                GatherChunksNear(cloud.transform.position, extents, visibilityCandidates);
            }

            foreach (int chunkIndex in previous)
                if (!visibilityCandidates.Contains(chunkIndex))
                    ReceiveFoodChunkClear(player, chunkIndex);

            foreach (int chunkIndex in visibilityCandidates)
                if (!previous.Contains(chunkIndex) || dirtyChunks.Contains(chunkIndex))
                    SendFoodChunk(player, chunkIndex);

            previous.Clear();
            previous.UnionWith(visibilityCandidates);
        }

        dirtyChunks.Clear();
        retiredIdsByChunk.Clear();
    }

    private void GatherChunksNear(Vector2 position, Vector2 extents, HashSet<int> results)
    {
        Vector2 minimum = indexedBoundaryCenter - indexedBoundarySize * 0.5f;
        Vector2 maximum = minimum + indexedBoundarySize;
        if (position.x + extents.x < minimum.x || position.x - extents.x > maximum.x ||
            position.y + extents.y < minimum.y || position.y - extents.y > maximum.y)
            return;

        float chunkWidth = indexedBoundarySize.x * CellsPerChunk / activeColumns;
        float chunkHeight = indexedBoundarySize.y * CellsPerChunk / activeRows;
        int firstColumn = Mathf.Clamp(Mathf.FloorToInt((position.x - extents.x - minimum.x) / chunkWidth),
            0, chunkColumns - 1);
        int lastColumn = Mathf.Clamp(Mathf.FloorToInt((position.x + extents.x - minimum.x) / chunkWidth),
            0, chunkColumns - 1);
        int firstRow = Mathf.Clamp(Mathf.FloorToInt((position.y - extents.y - minimum.y) / chunkHeight),
            0, chunkRows - 1);
        int lastRow = Mathf.Clamp(Mathf.FloorToInt((position.y + extents.y - minimum.y) / chunkHeight),
            0, chunkRows - 1);

        for (int row = firstRow; row <= lastRow; row++)
            for (int column = firstColumn; column <= lastColumn; column++)
                results.Add(row * chunkColumns + column);
    }

    private void SendFoodChunk(PlayerID player, int chunkIndex)
    {
        if (!snapshotCache.TryGetValue(chunkIndex,
                out (uint[] ids, Vector2[] positions, float[] sizes, bool[] fadeIn, uint[] retiredIds) snapshot))
        {
            List<uint> foodIds = foodIdsByChunk[chunkIndex];
            int count = foodIds?.Count ?? 0;
            uint[] ids = new uint[count];
            Vector2[] positions = new Vector2[count];
            float[] sizes = new float[count];
            bool[] fadeIn = new bool[count];
            for (int i = 0; i < count; i++)
            {
                FoodRecord record = foodRecords[foodIds[i]];
                ids[i] = record.Id;
                positions[i] = record.Position;
                sizes[i] = record.SizeMultiplier;
                fadeIn[i] = record.FadeInOnSpawn;
            }

            uint[] retiredIds = retiredIdsByChunk.TryGetValue(chunkIndex,
                out HashSet<uint> retired) ? new uint[retired.Count] : Array.Empty<uint>();
            retired?.CopyTo(retiredIds);
            snapshot = (ids, positions, sizes, fadeIn, retiredIds);
            snapshotCache.Add(chunkIndex, snapshot);
        }

        ReceiveFoodChunk(player, chunkIndex, chunkVersions[chunkIndex],
            snapshot.ids, snapshot.positions, snapshot.sizes, snapshot.fadeIn, snapshot.retiredIds);
    }

    [TargetRpc(runLocally: true)]
    private void ReceiveFoodChunk(PlayerID target, int chunkIndex, uint version,
        uint[] ids, Vector2[] positions, float[] sizes, bool[] fadeIn, uint[] retiredIds)
    {
        if (clientVisuals == null ||
            (receivedChunkVersions.TryGetValue(chunkIndex, out uint previous) && version < previous))
            return;

        clientVisuals.ApplyChunk(chunkIndex, ids, positions, sizes, fadeIn, retiredIds);
        receivedChunkVersions[chunkIndex] = version;
    }

    [TargetRpc(runLocally: true)]
    private void ReceiveFoodChunkClear(PlayerID target, int chunkIndex)
    {
        clientVisuals?.ClearChunk(chunkIndex);
        receivedChunkVersions.Remove(chunkIndex);
    }

    [ServerRpc(requireOwnership: false)]
    private void RequestFoodChunks(float aspect, RPCInfo info = default)
    {
        if (float.IsNaN(aspect) || float.IsInfinity(aspect))
            return;

        float clampedAspect = Mathf.Clamp(aspect, 0.5f, Mathf.Max(1f, maximumClientAspectRatio));
        if (readyPlayers.Add(info.sender) ||
            !clientAspectRatios.TryGetValue(info.sender, out float previous) ||
            Mathf.Abs(previous - clampedAspect) > 0.01f)
            nextVisibilityRefreshAt = 0d;

        clientAspectRatios[info.sender] = clampedAspect;
    }

    private void HandleVisibilityPlayerLeft(PlayerID player, bool asServer)
    {
        if (asServer)
        {
            nearbyChunksByPlayer.Remove(player);
            clientAspectRatios.Remove(player);
            readyPlayers.Remove(player);
        }
    }

    private void RefreshOutdatedFood(double now, int maxPerFrame)
    {
        double elapsed = lastOutdatedFoodTransitionBudgetAt > 0d ?
            Math.Max(0d, now - lastOutdatedFoodTransitionBudgetAt) : 0d;
        lastOutdatedFoodTransitionBudgetAt = now;
        if (retiringFood.Count == 0)
        {
            outdatedFoodTransitionAllowance = 0d;
            return;
        }

        int rate = Mathf.Max(1, outdatedFoodTransitionsPerSecond);
        double allowanceCap = Math.Max(1d, Math.Min(maxPerFrame, rate * 0.1d));
        outdatedFoodTransitionAllowance = Math.Min(allowanceCap,
            outdatedFoodTransitionAllowance + elapsed * rate);
        int allowedTransitions = (int)Math.Min(maxPerFrame, Math.Floor(outdatedFoodTransitionAllowance));
        if (allowedTransitions == 0)
            return;

        int candidatesToCheck = Mathf.Min(maxPerFrame, retiringFood.Count);
        int transitioned = 0;
        int currentCycle = GetCurrentCycle();
        MP_CycleTimer timer = cycleTimer != null ? cycleTimer : MP_CycleTimer.Instance;
        float sizeMultiplier = timer != null ? timer.FoodSizeMultiplier : 1f;
        float expMultiplier = timer != null ? timer.FoodExpMultiplier : 1f;
        for (int i = 0; i < candidatesToCheck && transitioned < allowedTransitions; i++)
        {
            uint foodId = retiringFood.Dequeue();
            if (!foodRecords.TryGetValue(foodId, out FoodRecord record) ||
                record.SpawnCycle >= currentCycle)
                continue;

            if (IsOutsideAllPlayerViews(record.Position, record.Radius))
            {
                RemoveFoodRecord(record, true);
                QueueReplacement(record);
            }
            else
            {
                FoodRecord upgraded = record.WithCycle(currentCycle, sizeMultiplier,
                    baseFoodExperience * Mathf.Max(1f, expMultiplier), baseFoodRadius,
                    baseFoodHitOffset, baseFoodHitRadius,
                    Math.Max(record.EdibleAt, now + MP_Food.GrowthDurationSeconds));
                foodRecords[foodId] = upgraded;
                largestFoodRadius = Mathf.Max(largestFoodRadius, upgraded.Radius);
                MarkChunkDirty(GetChunkIndex(record.CellIndex));
                nextVisibilityRefreshAt = 0d;
            }

            outdatedFoodTransitionAllowance -= 1d;
            transitioned++;
        }

        if (transitioned == 0)
            outdatedFoodTransitionAllowance = 0d;
    }

    private void HandleCycleChanged(int cycle)
    {
        nextFillAttemptAt = 0d;
        warnedNoSpawnPosition = false;
        TryFillTargetFood(0);

        retiringFood.Clear();
        foreach (FoodRecord record in foodRecords.Values)
            if (record.SpawnCycle < cycle)
                retiringFood.Enqueue(record.Id);
    }

    private void TryFillTargetFood(int maxSpawns)
    {
        if (!isServer || foodRecordIdsByCell == null)
            return;

        MP_GameBoundary boundary = MP_GameBoundary.Instance;
        if (boundary == null || boundary.Size.x <= 0f || boundary.Size.y <= 0f)
            return;

        EnsureSpawnRegions(boundary);
        int replacementsToTry = pendingRespawns.Count;
        int regionChecksRemaining = FailedRegionChecksPerFill;
        for (int spawned = 0; spawned < maxSpawns && regionChecksRemaining > 0; spawned++)
        {
            bool didSpawn = false;
            if (replacementsToTry > 0)
            {
                Vector2 source = pendingRespawns.Dequeue();
                replacementsToTry--;
                didSpawn = TrySpawnReplacement(boundary, source, ref regionChecksRemaining);
                if (!didSpawn)
                    pendingRespawns.Enqueue(source);
            }

            if (!didSpawn && regionChecksRemaining > 0 && initialRegions.Count > 0)
                didSpawn = TrySpawnInitialFood(boundary, ref regionChecksRemaining);

            if (didSpawn)
            {
                regionChecksRemaining++;
                warnedNoSpawnPosition = false;
                continue;
            }

            if (initialRegions.Count > 0 || pendingRespawns.Count > 0)
            {
                if (!warnedNoSpawnPosition)
                {
                    Debug.LogWarning("No hidden collision-free food position was found within the search budget", this);
                    warnedNoSpawnPosition = true;
                }

                nextFillAttemptAt = Time.realtimeSinceStartupAsDouble + FailedFillRetrySeconds;
            }
            return;
        }
    }

    private void EnsureSpawnRegions(MP_GameBoundary boundary)
    {
        int groupSize = GetGroupSize(GetCurrentCycle());
        if (spawnRegions.Count > 0 && activeGroupSize == groupSize &&
            indexedMaxFoodPerGrid == Mathf.Max(1, maxFoodPerGrid) &&
            indexedBoundaryCenter == boundary.Center && indexedBoundarySize == boundary.Size)
            return;

        int unfinishedInitialFood = 0;
        foreach (SpawnRegion region in spawnRegions)
            unfinishedInitialFood += region.InitialRemaining;

        bool isFirstDistribution = !initialDistributionStarted;
        initialDistributionStarted = true;
        activeGroupSize = groupSize;
        indexedMaxFoodPerGrid = Mathf.Max(1, maxFoodPerGrid);
        indexedBoundaryCenter = boundary.Center;
        indexedBoundarySize = boundary.Size;
        spawnRegions.Clear();
        regionsByFoodCount.Clear();
        initialRegions.Clear();

        int regionColumns = 1 + (activeColumns - 1) / groupSize;
        for (int row = 0; row < activeRows; row += groupSize)
        {
            for (int column = 0; column < activeColumns; column += groupSize)
            {
                Rect bounds = GetRegionBounds(boundary, column, row,
                    Mathf.Min(column + groupSize, activeColumns),
                    Mathf.Min(row + groupSize, activeRows), activeColumns, activeRows);
                int regionIndex = spawnRegions.Count;
                spawnRegions.Add(new SpawnRegion(bounds));
                AddRegionToBucket(regionIndex, 0);
            }
        }

        for (int row = 0; row < activeRows; row++)
            for (int column = 0; column < activeColumns; column++)
                cellRegionIndices[row * activeColumns + column] =
                    (row / groupSize) * regionColumns + column / groupSize;

        foreach (List<uint> cell in foodRecordIdsByCell)
            cell?.Clear();

        foreach (List<uint> chunk in foodIdsByChunk)
            chunk?.Clear();

        for (int chunkIndex = 0; chunkIndex < foodIdsByChunk.Length; chunkIndex++)
            MarkChunkDirty(chunkIndex);

        reindexFoodIds.Clear();
        reindexFoodIds.AddRange(foodRecords.Keys);
        foreach (uint foodId in reindexFoodIds)
        {
            FoodRecord record = foodRecords[foodId];
            int cellIndex = GetCellIndex(record.Position, indexedBoundaryCenter, indexedBoundarySize);
            record = record.WithCellIndex(cellIndex);
            foodRecords[foodId] = record;
            AddRecordToCell(foodId, cellIndex);
            AddRecordToChunk(foodId, GetChunkIndex(cellIndex));
            ChangeRegionFoodCount(cellRegionIndices[cellIndex], 1);
        }
        reindexFoodIds.Clear();

        if (isFirstDistribution)
        {
            for (int i = 0; i < spawnRegions.Count; i++)
            {
                SpawnRegion region = spawnRegions[i];
                int initialTarget = UnityEngine.Random.Range(0, indexedMaxFoodPerGrid) + 1;
                region.InitialRemaining = Mathf.Max(0, initialTarget - region.FoodCount);
            }
        }
        else
        {
            redistributionCandidates.Clear();
            for (int i = 0; i < spawnRegions.Count; i++)
                if (spawnRegions[i].FoodCount < indexedMaxFoodPerGrid)
                    redistributionCandidates.Add(i);

            while (unfinishedInitialFood > 0 && redistributionCandidates.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, redistributionCandidates.Count);
                SpawnRegion region = spawnRegions[redistributionCandidates[pick]];
                int amount = Mathf.Min(unfinishedInitialFood, indexedMaxFoodPerGrid - region.FoodCount);
                region.InitialRemaining = amount;
                unfinishedInitialFood -= amount;
                redistributionCandidates[pick] = redistributionCandidates[redistributionCandidates.Count - 1];
                redistributionCandidates.RemoveAt(redistributionCandidates.Count - 1);
            }
            redistributionCandidates.Clear();
        }

        for (int i = 0; i < spawnRegions.Count; i++)
            EnqueueInitialRegion(i);
    }

    private void EnqueueInitialRegion(int index)
    {
        SpawnRegion region = spawnRegions[index];
        if (region.IsQueued || region.InitialRemaining <= 0 ||
            region.FoodCount >= indexedMaxFoodPerGrid)
            return;

        region.IsQueued = true;
        initialRegions.Enqueue(index);
    }

    private void AddRecordToCell(uint foodId, int cellIndex)
    {
        foodRecordIdsByCell[cellIndex] ??= new List<uint>(Mathf.Max(1, indexedMaxFoodPerGrid));
        foodRecordIdsByCell[cellIndex].Add(foodId);
    }

    private int GetChunkIndex(int cellIndex) =>
        (cellIndex / activeColumns / CellsPerChunk) * chunkColumns +
        (cellIndex % activeColumns / CellsPerChunk);

    private void AddRecordToChunk(uint foodId, int chunkIndex)
    {
        foodIdsByChunk[chunkIndex] ??= new List<uint>(CellsPerChunk * CellsPerChunk *
            Mathf.Max(1, indexedMaxFoodPerGrid));
        foodIdsByChunk[chunkIndex].Add(foodId);
        MarkChunkDirty(chunkIndex);
    }

    private void MarkChunkDirty(int chunkIndex)
    {
        chunkVersions[chunkIndex]++;
        dirtyChunks.Add(chunkIndex);
    }

    private void AddRegionToBucket(int regionIndex, int foodCount)
    {
        while (regionsByFoodCount.Count <= foodCount)
            regionsByFoodCount.Add(new List<int>());

        List<int> bucket = regionsByFoodCount[foodCount];
        spawnRegions[regionIndex].BucketPosition = bucket.Count;
        bucket.Add(regionIndex);
    }

    private void ChangeRegionFoodCount(int regionIndex, int change)
    {
        SpawnRegion region = spawnRegions[regionIndex];
        List<int> oldBucket = regionsByFoodCount[region.FoodCount];
        int lastIndex = oldBucket.Count - 1;
        int movedRegionIndex = oldBucket[lastIndex];
        oldBucket[region.BucketPosition] = movedRegionIndex;
        spawnRegions[movedRegionIndex].BucketPosition = region.BucketPosition;
        oldBucket.RemoveAt(lastIndex);

        region.FoodCount += change;
        AddRegionToBucket(regionIndex, region.FoodCount);
    }

    private bool TrySpawnInitialFood(MP_GameBoundary boundary, ref int regionChecksRemaining)
    {
        int regionsToTry = initialRegions.Count;
        for (int regionAttempt = 0;
             regionAttempt < regionsToTry && regionChecksRemaining > 0; regionAttempt++)
        {
            int regionIndex = initialRegions.Dequeue();
            SpawnRegion region = spawnRegions[regionIndex];
            region.IsQueued = false;
            if (region.InitialRemaining <= 0 || region.FoodCount >= indexedMaxFoodPerGrid)
            {
                regionChecksRemaining--;
                continue;
            }

            if (TrySpawnInRegion(boundary, regionIndex, ref regionChecksRemaining))
            {
                region.InitialRemaining--;
                EnqueueInitialRegion(regionIndex);
                return true;
            }

            EnqueueInitialRegion(regionIndex);
        }

        return false;
    }

    private bool TrySpawnReplacement(MP_GameBoundary boundary, Vector2 source,
        ref int regionChecksRemaining)
    {
        int sourceCell = GetCellIndex(source, indexedBoundaryCenter, indexedBoundarySize);
        int sourceRegion = cellRegionIndices[sourceCell];

        return TrySpawnInOtherRegion(boundary, sourceRegion,
                   ref regionChecksRemaining) ||
            (regionChecksRemaining > 0 && TrySpawnInRegion(boundary, sourceRegion,
                ref regionChecksRemaining));
    }

    private bool TrySpawnInOtherRegion(MP_GameBoundary boundary, int sourceRegion,
        ref int regionChecksRemaining)
    {
        int bucketLimit = Mathf.Min(indexedMaxFoodPerGrid, regionsByFoodCount.Count);
        for (int foodCount = 0;
             foodCount < bucketLimit && regionChecksRemaining > 0; foodCount++)
            if (TrySpawnFromBucket(boundary, regionsByFoodCount[foodCount], sourceRegion,
                    ref regionChecksRemaining))
                return true;

        return false;
    }

    private bool TrySpawnFromBucket(MP_GameBoundary boundary, List<int> candidates,
        int sourceRegion, ref int regionChecksRemaining)
    {
        if (candidates.Count == 0)
            return false;

        int start = UnityEngine.Random.Range(0, candidates.Count);
        for (int i = 0; i < candidates.Count && regionChecksRemaining > 0; i++)
        {
            int regionIndex = candidates[(start + i) % candidates.Count];
            if (regionIndex == sourceRegion)
                continue;

            if (TrySpawnInRegion(boundary, regionIndex, ref regionChecksRemaining))
            {
                SpawnRegion region = spawnRegions[regionIndex];
                region.InitialRemaining = Mathf.Max(0, region.InitialRemaining - 1);
                return true;
            }
        }

        return false;
    }

    private bool TrySpawnInRegion(MP_GameBoundary boundary, int regionIndex,
        ref int regionChecksRemaining)
    {
        if (foodPrefab == null || regionChecksRemaining <= 0)
            return false;

        regionChecksRemaining--;

        MP_CycleTimer timer = cycleTimer != null ? cycleTimer : MP_CycleTimer.Instance;
        int cycle = timer != null ? Mathf.Max(1, timer.CurrentCycle) : 1;
        float sizeMultiplier = timer != null ? timer.FoodSizeMultiplier : 1f;
        float expMultiplier = timer != null ? timer.FoodExpMultiplier : 1f;
        float radius = GetFoodRadius(sizeMultiplier);
        SpawnRegion region = spawnRegions[regionIndex];
        if (region.FoodCount >= indexedMaxFoodPerGrid ||
            region.Bounds.width < radius * 2f || region.Bounds.height < radius * 2f)
            return false;

        int attempts = placementAttemptsPerRegion > 0 ? placementAttemptsPerRegion : 8;
        for (int i = 0; i < attempts; i++)
        {
            Vector2 position = new(
                UnityEngine.Random.Range(region.Bounds.xMin + radius, region.Bounds.xMax - radius),
                UnityEngine.Random.Range(region.Bounds.yMin + radius, region.Bounds.yMax - radius)
            );

            if (!IsOutsideAllPlayerViews(position, radius) ||
                !IsClear(position, radius, boundary))
                continue;

            if (nextFoodId == 0)
            {
                Debug.LogError("Food record IDs have been exhausted", this);
                return false;
            }

            int cellIndex = GetCellIndex(position, indexedBoundaryCenter, indexedBoundarySize);
            uint foodId = nextFoodId++;
            double edibleAt = Time.realtimeSinceStartupAsDouble;
            FoodRecord record = new(foodId, position, radius,
                position + baseFoodHitOffset * sizeMultiplier,
                baseFoodHitRadius * sizeMultiplier, cycle, sizeMultiplier,
                baseFoodExperience * Mathf.Max(1f, expMultiplier), edibleAt,
                false, cellIndex);
            foodRecords.Add(foodId, record);
            AddRecordToCell(foodId, cellIndex);
            AddRecordToChunk(foodId, GetChunkIndex(cellIndex));
            ChangeRegionFoodCount(cellRegionIndices[cellIndex], 1);
            largestFoodRadius = Mathf.Max(largestFoodRadius, radius);
            return true;
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
        return baseFoodRadius * sizeMultiplier;
    }

    private bool IsOutsideAllPlayerViews(Vector2 position, float foodRadius)
    {
        MP_PlayerManager playerManager = MP_PlayerManager.Instance;
        if (playerManager == null)
            return true;

        float padding = Mathf.Max(0f, visibilityPadding) + foodRadius;
        foreach (KeyValuePair<PlayerID, MP_PlayerManager.PlayerSession> entry in playerManager.Sessions)
        {
            if (!entry.Value.IsAlive)
                continue;

            MP_Player cloud = entry.Value.Cloud;
            Vector2 viewExtents = GetViewExtents(entry.Key, cloud, padding);
            Vector2 offset = position - (Vector2)cloud.transform.position;
            if (Mathf.Abs(offset.x) <= viewExtents.x &&
                Mathf.Abs(offset.y) <= viewExtents.y)
                return false;
        }

        return true;
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
                List<uint> cell = foodRecordIdsByCell[row * activeColumns + column];
                if (cell == null)
                    continue;

                foreach (uint foodId in cell)
                {
                    FoodRecord record = foodRecords[foodId];
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
