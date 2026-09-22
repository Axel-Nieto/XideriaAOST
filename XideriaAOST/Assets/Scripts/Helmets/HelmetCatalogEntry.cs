using UnityEngine;

[System.Serializable]
public class HelmetCatalogEntry
{
    public HelmetType Type;

    public GameObject VisualPrefab;

    public Sprite InventoryIcon;

    public int MaxStored = 5;

    public int MaxCharges = 3;
}
