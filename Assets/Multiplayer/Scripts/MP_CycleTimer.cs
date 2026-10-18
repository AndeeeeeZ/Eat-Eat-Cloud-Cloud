using System;
using PurrNet;
using UnityEngine;

public class MP_CycleTimer : NetworkBehaviour
{
    [SerializeField, Min(1f)] private float cycleDurationSeconds = 60f;
    [SerializeField, Min(1)] private int finalCycle = 5;
    [SerializeField, Min(1f)] private float speedMultiplierPerCycle = 1.1f;
    [SerializeField, Min(1f)] private float foodSizeMultiplierPerCycle = 1.1f;
    [SerializeField, Min(1f)] private float foodExpMultiplierPerCycle = 1.1f;

    private readonly SyncVar<int> currentCycle = new(0);
    private readonly SyncVar<int> secondsRemaining = new(0);
    private readonly SyncVar<float> speedMultiplier = new(1f);
    private readonly SyncVar<float> foodSizeMultiplier = new(1f);
    private readonly SyncVar<float> foodExpMultiplier = new(1f);
    private double nextCycleAt;

    public static MP_CycleTimer Instance { get; private set; }
    public int CurrentCycle => isSpawned ? currentCycle.value : 0;
    public int FinalCycle => Mathf.Max(1, finalCycle);
    public int SecondsRemaining => isSpawned ? secondsRemaining.value : 0;
    public float SpeedMultiplier => isSpawned ? speedMultiplier.value : 1f;
    public float FoodSizeMultiplier => isSpawned ? foodSizeMultiplier.value : 1f;
    public float FoodExpMultiplier => isSpawned ? foodExpMultiplier.value : 1f;

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
        if (!asServer)
            return;

        BeginCycle(1);
        if (currentCycle.value >= FinalCycle)
        {
            secondsRemaining.value = 0;
            return;
        }

        float duration = Mathf.Max(1f, cycleDurationSeconds);
        nextCycleAt = Time.realtimeSinceStartupAsDouble + duration;
        secondsRemaining.value = Mathf.CeilToInt(duration);
    }

    private void Update()
    {
        if (!isServer || !isSpawned || currentCycle.value == 0 || currentCycle.value >= FinalCycle)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        if (now >= nextCycleAt)
        {
            BeginCycle(currentCycle.value + 1);
            if (currentCycle.value >= FinalCycle)
            {
                secondsRemaining.value = 0;
                return;
            }

            float duration = Mathf.Max(1f, cycleDurationSeconds);
            nextCycleAt = now + duration;
        }

        int remaining = Mathf.CeilToInt((float)Math.Max(0d, nextCycleAt - now));
        if (secondsRemaining.value != remaining)
            secondsRemaining.value = remaining;
    }

    private void BeginCycle(int cycle)
    {
        speedMultiplier.value = Mathf.Pow(Mathf.Max(1f, speedMultiplierPerCycle), cycle - 1);
        foodSizeMultiplier.value = Mathf.Pow(Mathf.Max(1f, foodSizeMultiplierPerCycle), cycle - 1);
        foodExpMultiplier.value = Mathf.Pow(Mathf.Max(1f, foodExpMultiplierPerCycle), cycle - 1);
        currentCycle.value = cycle;
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
