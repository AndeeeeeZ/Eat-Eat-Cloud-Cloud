using System;
using PurrNet;
using UnityEngine;
using UnityEngine.Serialization;

// Creates a fixed lifetime total of ownerless enemy clouds after cycle one starts
public class MP_EnemySpawner : NetworkBehaviour
{
    [SerializeField] private MP_Player enemyPrefab;
    [SerializeField, Min(0)] private int initialEnemySpawns = 1;
    [FormerlySerializedAs("enemyCount")]
    [SerializeField, Min(0)] private int totalEnemySpawns = 1;
    [SerializeField, Min(0.1f)] private float spawnIntervalSeconds = 5f;
    [SerializeField, Min(0.1f)] private float retryInterval = 1f;

    private int spawnedCount;
    private int initialRemaining;
    private double nextAttemptAt;
    private double nextScheduledSpawnAt;
    private bool started;
    private bool stopped;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (!asServer)
            return;

        if (enemyPrefab == null || enemyPrefab.GetComponent<MP_EnemyAI>() == null ||
            enemyPrefab.Stats == null || enemyPrefab.GetComponent<MP_PlayerGrowth>() == null)
        {
            Debug.LogError("Assign a valid AI player prefab on MP_EnemySpawner", this);
            stopped = true;
            return;
        }

        ResetRound();
    }

    public void ResetRound()
    {
        if (!isServer || !isSpawned)
            return;

        spawnedCount = 0;
        initialRemaining = 0;
        nextAttemptAt = 0d;
        nextScheduledSpawnAt = 0d;
        started = false;
        stopped = enemyPrefab == null || enemyPrefab.GetComponent<MP_EnemyAI>() == null ||
            enemyPrefab.Stats == null || enemyPrefab.GetComponent<MP_PlayerGrowth>() == null;
    }

    private void Update()
    {
        if (!isServer || !isSpawned || stopped || spawnedCount >= Mathf.Max(0, totalEnemySpawns))
            return;

        MP_CycleTimer timer = MP_CycleTimer.Instance;
        MP_PlayerManager manager = MP_PlayerManager.Instance;
        if (manager != null && manager.RoundEnded)
            return;

        if (!started)
        {
            if (timer == null || timer.CurrentCycle == 0)
                return;

            started = true;
            initialRemaining = Mathf.Min(Mathf.Max(0, initialEnemySpawns),
                Mathf.Max(0, totalEnemySpawns));
            nextScheduledSpawnAt = Time.realtimeSinceStartupAsDouble +
                Mathf.Max(0.1f, spawnIntervalSeconds);
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextAttemptAt)
            return;

        if (manager == null)
        {
            nextAttemptAt = now + Mathf.Max(0.1f, retryInterval);
            return;
        }

        if (initialRemaining > 0)
        {
            while (initialRemaining > 0 && !stopped)
            {
                if (!TrySpawn(manager))
                {
                    nextAttemptAt = now + Mathf.Max(0.1f, retryInterval);
                    return;
                }

                spawnedCount++;
                initialRemaining--;
            }

            nextScheduledSpawnAt = now + Mathf.Max(0.1f, spawnIntervalSeconds);
            return;
        }

        if (now < nextScheduledSpawnAt)
            return;

        if (TrySpawn(manager))
        {
            spawnedCount++;
            nextScheduledSpawnAt = now + Mathf.Max(0.1f, spawnIntervalSeconds);
        }
        else
            nextAttemptAt = now + Mathf.Max(0.1f, retryInterval);
    }

    private bool TrySpawn(MP_PlayerManager manager)
    {
        try
        {
            return manager.TrySpawnEnemy(enemyPrefab);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            stopped = true;
            return false;
        }
    }

    protected override void OnDespawned(bool asServer)
    {
        if (asServer)
        {
            spawnedCount = 0;
            initialRemaining = 0;
            started = false;
            stopped = false;
        }

        base.OnDespawned(asServer);
    }
}
