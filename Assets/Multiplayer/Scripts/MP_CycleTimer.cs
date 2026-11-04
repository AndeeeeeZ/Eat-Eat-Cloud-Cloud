using System;
using PurrNet;
using UnityEngine;

public class MP_CycleTimer : NetworkBehaviour
{
    [SerializeField, Min(1f)] private float cycleDurationSeconds = 60f;
    [SerializeField, Min(1)] private int finalCycle = 5;
    [SerializeField, Min(1f)] private float finalCycleDurationSeconds = 180f;
    [SerializeField, Min(1f)] private float speedMultiplierPerCycle = 1.1f;
    [Tooltip("One movement base level per cycle, starting with cycle 1. Later cycles reuse the last entry")]
    [SerializeField] private int[] baseLevelsPerCycle = { 1, 1, 1, 1, 1 };
    [SerializeField, Range(0f, 100f)] private float movementSpeedReductionPerLevelPercent = 5f;
    [SerializeField, Range(0f, 100f)] private float maxMovementSpeedReductionPercent = 20f;
    [SerializeField, Min(1f)] private float foodSizeMultiplierPerCycle = 1.1f;
    [SerializeField, Min(1f)] private float foodExpMultiplierPerCycle = 1.1f;

    private readonly SyncVar<int> currentCycle = new(0);
    private readonly SyncVar<int> secondsRemaining = new(0);
    private readonly SyncVar<float> speedMultiplier = new(1f);
    private readonly SyncVar<int> movementBaseLevel = new(1);
    private readonly SyncVar<float> movementSpeedReductionPerLevel = new(0.05f);
    private readonly SyncVar<float> maxMovementSpeedReduction = new(0.2f);
    private readonly SyncVar<float> foodSizeMultiplier = new(1f);
    private readonly SyncVar<float> foodExpMultiplier = new(1f);
    private double nextCycleAt;

    public static MP_CycleTimer Instance { get; private set; }
    public int CurrentCycle => isSpawned ? currentCycle.value : 0;
    public int FinalCycle => Mathf.Max(1, finalCycle);
    public int SecondsRemaining => isSpawned ? secondsRemaining.value : 0;
    public float SpeedMultiplier => isSpawned ? speedMultiplier.value : 1f;
    public int MovementBaseLevel => isSpawned ? movementBaseLevel.value : 1;
    public float MovementSpeedReductionPerLevel => isSpawned ? movementSpeedReductionPerLevel.value : 0.05f;
    public float MaxMovementSpeedReduction => isSpawned ? maxMovementSpeedReduction.value : 0.2f;
    public float FoodSizeMultiplier => isSpawned ? foodSizeMultiplier.value : 1f;
    public float FoodExpMultiplier => isSpawned ? foodExpMultiplier.value : 1f;

    public float GetFoodSizeMultiplierForCycle(int cycle) =>
        Mathf.Pow(Mathf.Max(1f, foodSizeMultiplierPerCycle), Mathf.Max(1, cycle) - 1);

    public event Action<int> OnCycleChanged;
    public event Action<int> OnSecondsRemainingChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one MP_CycleTimer should be active in a scene", this);
            enabled = false;
            return;
        }

        Instance = this;
        currentCycle.onChanged += HandleCycleChanged;
        secondsRemaining.onChanged += HandleSecondsRemainingChanged;
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (asServer)
            ResetRound();
    }

    public void ResetRound()
    {
        if (!isServer || !isSpawned)
            return;

        nextCycleAt = 0d;
        currentCycle.value = 0;
        secondsRemaining.value = 0;
        speedMultiplier.value = 1f;
        movementBaseLevel.value = 1;
        movementSpeedReductionPerLevel.value = Mathf.Clamp01(movementSpeedReductionPerLevelPercent / 100f);
        maxMovementSpeedReduction.value = Mathf.Clamp01(maxMovementSpeedReductionPercent / 100f);
        foodSizeMultiplier.value = 1f;
        foodExpMultiplier.value = 1f;
    }

    private void StartFirstCycle()
    {
        BeginCycle(1);
        StartCycleTimer(Time.realtimeSinceStartupAsDouble);
    }

    private void Update()
    {
        if (!isServer || !isSpawned)
            return;

        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager != null && manager.RoundEnded)
            return;

        if (currentCycle.value == 0)
        {
            if (manager != null && manager.Sessions.Count > 0)
                StartFirstCycle();
            return;
        }

        if (nextCycleAt <= 0d)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        if (now >= nextCycleAt)
        {
            if (currentCycle.value >= FinalCycle)
            {
                nextCycleAt = 0d;
                secondsRemaining.value = 0;
                manager?.EndRoundForLargestCloud();
                return;
            }

            BeginCycle(currentCycle.value + 1);
            StartCycleTimer(now);
        }

        int remaining = Mathf.CeilToInt((float)Math.Max(0d, nextCycleAt - now));
        if (secondsRemaining.value != remaining)
            secondsRemaining.value = remaining;
    }

    private void StartCycleTimer(double now)
    {
        float duration = currentCycle.value >= FinalCycle
            ? Mathf.Max(1f, finalCycleDurationSeconds)
            : Mathf.Max(1f, cycleDurationSeconds);
        nextCycleAt = now + duration;
        secondsRemaining.value = Mathf.CeilToInt(duration);
    }

    private void BeginCycle(int cycle)
    {
        speedMultiplier.value = Mathf.Pow(Mathf.Max(1f, speedMultiplierPerCycle), cycle - 1);
        int baseLevelIndex = baseLevelsPerCycle != null
            ? Mathf.Min(cycle - 1, baseLevelsPerCycle.Length - 1) : -1;
        movementBaseLevel.value = baseLevelIndex >= 0
            ? Mathf.Max(1, baseLevelsPerCycle[baseLevelIndex]) : 1;
        foodSizeMultiplier.value = GetFoodSizeMultiplierForCycle(cycle);
        foodExpMultiplier.value = Mathf.Pow(Mathf.Max(1f, foodExpMultiplierPerCycle), cycle - 1);
        if (cycle > 1 && MP_PlayerManager.Instance != null)
            MP_PlayerManager.Instance.CloseJoining();
        currentCycle.value = cycle;
    }

    public void StopForRoundEnd()
    {
        if (!isServer)
            return;

        nextCycleAt = 0d;
        secondsRemaining.value = 0;
    }

    private void HandleCycleChanged(int cycle)
    {
        OnCycleChanged?.Invoke(cycle);
    }

    private void HandleSecondsRemainingChanged(int remaining)
    {
        OnSecondsRemainingChanged?.Invoke(remaining);
    }

    protected override void OnDestroy()
    {
        currentCycle.onChanged -= HandleCycleChanged;
        secondsRemaining.onChanged -= HandleSecondsRemainingChanged;
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }
}
