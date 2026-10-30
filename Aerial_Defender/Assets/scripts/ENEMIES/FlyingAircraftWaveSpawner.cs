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

    [Header("Wave speed")]
    [SerializeField, Range(0f, 1f)] private float firstWaveSpeedMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float speedIncreasePerPairOfWaves = 0.05f;

    private readonly List<FlyingAircraft> currentWaveAircraft = new List<FlyingAircraft>();
    private CharacterCustomization characterCustomization;
    private GameManager gameManager;
    private GUIStyle waveStatusStyle;
    private int displayedWaveNumber = 1;
    private bool hasStartedWaves;

    private void Start()
    {
        if (aircraftPrefab == null)
        {
            Debug.LogError("Assign an aircraft prefab to the wave spawner.", this);
            enabled = false;
            return;
        }

        if (aircraftPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("The wave spawner's aircraft prefab must reference the prefab asset, not a scene aircraft instance.", this);
            enabled = false;
            return;
        }

        Transform towerHitpoint = FlyingAircraft.FindTowerHitpointTarget();
        if (towerHitpoint != null)
        {
            target = towerHitpoint;
        }

        if (target == null || target.name != "towerhitpoint")
        {
            Debug.LogError("The wave spawner needs the scene instance of the 'towerhitpoint' prefab as its target.", this);
            enabled = false;
            return;
        }

        if (maximumSpawnDistance < minimumSpawnDistance)
        {
            Debug.LogError("Maximum spawn distance must be at least the minimum spawn distance.", this);
            enabled = false;
            return;
        }

        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("The wave shop requires an active GameManager in the scene.", this);
            enabled = false;
            return;
        }

        gameManager.InitializeTowerUpgrades();
        characterCustomization = FindObjectOfType<CharacterCustomization>();
        StartCoroutine(SpawnWaves());
    }

    private IEnumerator SpawnWaves()
    {
        while (LocalMultiplayerManager.IsCustomizationFlowActive
            || (characterCustomization != null && characterCustomization.IsCustomizing))
        {
            yield return null;
        }

        hasStartedWaves = true;
        int waveNumber = 1;
        while (true)
        {
            if (aircraftPrefab == null)
            {
                Debug.LogError($"Cannot spawn wave {waveNumber}: the aircraft prefab reference is missing or destroyed.", this);
                yield break;
            }

            displayedWaveNumber = waveNumber;
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

            yield return gameManager.WaitForBetweenWaveShop();

            waveNumber++;
        }
    }

    private void OnGUI()
    {
        if (!hasStartedWaves) return;

        if (waveStatusStyle == null)
        {
            waveStatusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 22,
                richText = false
            };
        }

        DrawWaveStatus($"WAVE {displayedWaveNumber}", Color.white);
    }

    private void DrawWaveStatus(string statusText, Color statusColor)
    {
        float panelWidth = Mathf.Min(380f, Screen.width - 32f);
        float panelX = LocalMultiplayerManager.SelectedPlayerCount > 1
            ? Screen.width * 0.5f + 16f
            : (Screen.width - panelWidth) * 0.5f;
        panelX = Mathf.Min(panelX, Screen.width - panelWidth - 16f);
        Rect backgroundRect = new Rect(panelX, 12f, panelWidth, 88f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture);
        GUI.color = statusColor;
        GUI.Label(backgroundRect, statusText, waveStatusStyle);
        GUI.color = previousColor;
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
