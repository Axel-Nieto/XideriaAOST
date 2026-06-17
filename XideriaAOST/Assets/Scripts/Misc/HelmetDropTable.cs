using System.Collections.Generic;
using UnityEngine;

public class HelmetDropTable : MonoBehaviour
{
    public static HelmetDropTable Instance { get; private set; }

    [SerializeField] private List<GameObject> helmetPickupPrefabs = new();

    private void Awake()
    {
        Instance = this;
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