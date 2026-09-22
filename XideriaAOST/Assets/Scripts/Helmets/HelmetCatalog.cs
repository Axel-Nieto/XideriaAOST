using System.Collections.Generic;
using UnityEngine;

public class HelmetCatalog : MonoBehaviour
{
    public static HelmetCatalog Instance { get; private set; }

    [SerializeField]
    private List<HelmetCatalogEntry> helmets = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public HelmetCatalogEntry GetEntry(HelmetType type)
    {
        foreach (var entry in helmets)
        {
            if (entry.Type == type)
                return entry;
        }

        return null;
    }

    public HelmetData CreateHelmet(HelmetType type)
    {
        HelmetData helmet = type switch
        {
            HelmetType.Fire => HelmetFactory.CreateFireHelmet(),
            HelmetType.Water => HelmetFactory.CreateWaterHelmet(),
            _ => HelmetFactory.CreateDefaultHelmet()
        };

        HelmetCatalogEntry entry = GetEntry(type);

        if (entry != null)
            helmet.HelmetVisualPrefab = entry.VisualPrefab;

        return helmet;
    }
}
