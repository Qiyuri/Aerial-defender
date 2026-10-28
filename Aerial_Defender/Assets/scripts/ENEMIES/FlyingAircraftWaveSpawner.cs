using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyingAircraftWaveSpawner : MonoBehaviour
{
    [Header("Wave setup")]
    [SerializeField] private FlyingAircraft aircraftPrefab;
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float minimumSpawnDistance = 1000f;
    [SerializeField, Min(0f)] private float maximumSpawnDistance = 8000f;
    [SerializeField, Min(0f)] private float maximumHeightDifference = 100f;
    [SerializeField, Min(0f)] private float timeBetweenWaves = 5f;

    [Header("Wave speed")]
    [SerializeField, Range(0f, 1f)] private float firstWaveSpeedMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float speedIncreasePerPairOfWaves = 0.05f;

    private readonly List<FlyingAircraft> currentWaveAircraft = new List<FlyingAircraft>();

    private void Start()
    {
        if (aircraftPrefab == null)
        {
            Debug.LogError("Assign an aircraft prefab to the wave spawner.", this);
            enabled = false;
            return;
        }

        if (target == null)
        {
            Debug.LogError("Assign a target transform to the wave spawner.", this);
            enabled = false;
            return;
        }

        if (maximumSpawnDistance < minimumSpawnDistance)
        {
            Debug.LogError("Maximum spawn distance must be at least the minimum spawn distance.", this);
            enabled = false;
            return;
        }

        StartCoroutine(SpawnWaves());
    }

    private IEnumerator SpawnWaves()
    {
        int waveNumber = 1;
        while (true)
        {
            int aircraftCount = waveNumber + 1;
            float speedMultiplier = GetSpeedMultiplier(waveNumber);
            currentWaveAircraft.Clear();

            for (int i = 0; i < aircraftCount; i++)
            {
                Vector2 horizontalDirection = Random.insideUnitCircle.normalized;
                if (horizontalDirection.sqrMagnitude < 0.0001f) horizontalDirection = Vector2.right;
                float spawnDistance = Random.Range(minimumSpawnDistance, maximumSpawnDistance);
                float heightOffset = Random.Range(-maximumHeightDifference, maximumHeightDifference);
                Vector3 spawnPosition = target.position + new Vector3(
                    horizontalDirection.x * spawnDistance,
                    heightOffset,
                    horizontalDirection.y * spawnDistance);

                FlyingAircraft aircraft = Instantiate(
                    aircraftPrefab,
                    spawnPosition,
                    Quaternion.identity);
                aircraft.InitializeWaveSpawn(target, speedMultiplier);
                currentWaveAircraft.Add(aircraft);
            }

            while (!IsCurrentWaveDefeated())
            {
                yield return null;
            }

            if (timeBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }

            waveNumber++;
        }
    }

    private float GetSpeedMultiplier(int waveNumber)
    {
        if (waveNumber == 1) return firstWaveSpeedMultiplier;

        int completedWavePairs = (waveNumber - 2) / 2;
        return Mathf.Pow(1f + speedIncreasePerPairOfWaves, completedWavePairs);
    }

    private bool IsCurrentWaveDefeated()
    {
        foreach (FlyingAircraft aircraft in currentWaveAircraft)
        {
            if (aircraft != null && !aircraft.IsDefeated) return false;
        }

        return true;
    }
}
