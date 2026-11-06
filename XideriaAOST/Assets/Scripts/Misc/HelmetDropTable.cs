using System.Collections.Generic;
using UnityEngine;

// Replace map pickups before other Start methods inspect movement/occupancy.
[DefaultExecutionOrder(-200)]
public class HelmetDropTable : MonoBehaviour
{
    public static HelmetDropTable Instance { get; private set; }

    [Tooltip("Available pickup prefabs for this map and enemy drops. Add future helmets here.")]
    [SerializeField] private List<GameObject> helmetPickupPrefabs = new();
    [Tooltip("Choose a random helmet at each existing map pickup location when the scene loads. Repeats are allowed.")]
    [SerializeField] private bool randomizeSceneHelmets = true;

    private readonly List<HelmetPickup> scenePickups = new();
    private bool sceneHelmetsRandomized;

    private void Awake()
    {
        Instance = this;

        // Capture only authored pickups. Loot instantiated later must not be rerolled.
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (HelmetPickup pickup in root.GetComponentsInChildren<HelmetPickup>())
                if (pickup.gameObject.activeInHierarchy)
                    scenePickups.Add(pickup);
    }

    private void Start() => RandomizeSceneHelmets();

    public void RandomizeSceneHelmets()
    {
        if (!randomizeSceneHelmets || sceneHelmetsRandomized)
            return;
        sceneHelmetsRandomized = true;

        var candidates = new List<HelmetPickup>();
        if (helmetPickupPrefabs != null)
            foreach (GameObject prefab in helmetPickupPrefabs)
            {
                if (prefab == null || !prefab.TryGetComponent(out HelmetPickup pickup) ||
                    pickup.Type == HelmetType.Default || candidates.Contains(pickup))
                    continue;
                candidates.Add(pickup);
            }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("HelmetDropTable: no hay prefabs validos para aleatorizar los cascos del mapa.", this);
            return;
        }

        GameObject staging = new("Helmet spawn staging");
        staging.SetActive(false);
        staging.transform.SetParent(transform, false);
        // The staging parent must not inherit the table's visual scale/rotation.
        staging.transform.SetParent(null, true);
        staging.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        staging.transform.localScale = Vector3.one;
        try
        {
            foreach (HelmetPickup spawnPoint in scenePickups)
            {
                if (spawnPoint == null || spawnPoint.IsCollected || !spawnPoint.gameObject.activeInHierarchy)
                    continue;

                HelmetPickup selected = candidates[Random.Range(0, candidates.Count)];
                HelmetPickup replacement = Instantiate(selected, staging.transform, false);
                replacement.gameObject.SetActive(false);
                replacement.ConfigureSceneSpawn(spawnPoint);
                replacement.transform.SetParent(spawnPoint.transform.parent, true);
                // Hide the old pickup immediately: Destroy is deferred in Play mode.
                spawnPoint.gameObject.SetActive(false);
                replacement.gameObject.SetActive(true);
                DestroySpawnObject(spawnPoint.gameObject);
            }
        }
        finally
        {
            scenePickups.Clear();
            DestroySpawnObject(staging);
        }
    }

    private static void DestroySpawnObject(GameObject target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    public GameObject GetRandomHelmetPickupPrefab()
    {
        if (helmetPickupPrefabs == null || helmetPickupPrefabs.Count == 0)
        {
            Debug.LogWarning("HelmetDropTable: no hay cascos configurados.");
            return null;
        }

        int index = Random.Range(0, helmetPickupPrefabs.Count);
        return helmetPickupPrefabs[index];
    }
}
