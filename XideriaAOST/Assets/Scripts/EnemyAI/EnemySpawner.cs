using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EnemySpawnPoint
{
    [Tooltip("Logical origin of the enemy's occupied rectangle.")]
    public Vector2Int gridPosition;
    [Tooltip("World-space visual offset. Does not change occupied cells.")]
    public Vector3 visualOffset = new(0f, 0.5f, 0f);
}

// Grid/scene characters finish Awake first. Spawn before GameStateManager.Start
// scans combatants and before TurnManager builds its order on the next frame.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GridSystem grid;
    [Tooltip("Root Character components of the enemy prefabs allowed in this map. Equal probability; repeats allowed.")]
    [SerializeField] private List<Character> allowedEnemies = new();
    [Tooltip("One enemy per point. Position and visual offset are independent of the randomly selected prefab.")]
    [SerializeField] private List<EnemySpawnPoint> spawnPoints = new();

    private bool hasSpawned;
    private readonly List<Character> spawnedEnemies = new();
    public IReadOnlyList<Character> AllowedEnemies => allowedEnemies;
    public IReadOnlyList<EnemySpawnPoint> SpawnPoints => spawnPoints;
    public IReadOnlyList<Character> SpawnedEnemies => spawnedEnemies;

    private void Start() => SpawnEnemies();

    // Safe to call again: reopening/enabling the component never duplicates enemies.
    public void SpawnEnemies()
    {
        if (hasSpawned)
            return;
        hasSpawned = true;

        if (grid == null || grid.gameObject.scene != gameObject.scene)
        {
            Debug.LogError("EnemySpawner: assign the GridSystem of this map.", this);
            return;
        }

        var candidates = new List<Character>();
        GameObject staging = new("Enemy spawn staging");
        staging.SetActive(false);
        staging.transform.SetParent(transform, false);
        try
        {
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                EnemySpawnPoint point = spawnPoints[i];
                candidates.Clear();
                if (point != null)
                    foreach (Character prefab in allowedEnemies)
                        if (prefab != null && !prefab.IsPlayer &&
                            prefab.transform.parent == null && !candidates.Contains(prefab) &&
                            CanSpawn(prefab, point.gridPosition))
                            candidates.Add(prefab);

                if (candidates.Count == 0)
                {
                    Debug.LogError($"EnemySpawner: point {i} has no allowed enemy that fits. Check prefabs, terrain and occupancy.", this);
                    continue;
                }

                Character selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                // Instantiating under an inactive parent defers Awake. Configure
                // before activation so the prefab's default cell is never registered.
                Character enemy = Instantiate(selected, staging.transform, false);
                enemy.gameObject.SetActive(false);
                enemy.ConfigureSpawn(grid, point.gridPosition, point.visualOffset);
                // Keep prefab scale/rotation independent of the spawner transform.
                // Unparenting retains the map scene inherited through staging.
                enemy.transform.SetParent(null, false);
                enemy.gameObject.SetActive(true);
                spawnedEnemies.Add(enemy);
            }
        }
        finally
        {
            if (Application.isPlaying)
                Destroy(staging);
            else
                DestroyImmediate(staging);
        }
    }

    private bool CanSpawn(Character prefab, Vector2Int origin)
    {
        if (!grid.CanCharacterOccupy(prefab, origin, ignoreSelf: false))
            return false;
        foreach (Vector2Int cell in prefab.GetOccupiedCells(origin))
            if (grid.GetCell(cell).Type == CellType.Void)
                return false;
        return true;
    }
}
