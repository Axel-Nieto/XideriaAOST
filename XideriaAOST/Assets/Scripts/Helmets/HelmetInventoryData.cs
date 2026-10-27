using System;
using System.Collections.Generic;
using UnityEngine;

public class HelmetInventoryData : MonoBehaviour
{
    public static HelmetInventoryData Instance { get; private set; }

    private readonly Dictionary<HelmetType, HelmetInventoryEntry> helmets = new();


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        InitializeInventory();
    }

    private void Start()
    {
        UnlockHelmet(HelmetType.Fire);
        UnlockHelmet(HelmetType.Water);
        UnlockHelmet(HelmetType.Rock);
    }

    private void InitializeInventory()
    {
        foreach (HelmetType type in Enum.GetValues(typeof(HelmetType)))
        {
            if (type == HelmetType.Default)
                continue;

            if (!helmets.ContainsKey(type))
            {
                helmets.Add(
                    type,
                    new HelmetInventoryEntry(type)
                );
            }
        }
    }

    public bool IsUnlocked(HelmetType type)
    {
        if (type == HelmetType.Default)
            return true;

        return helmets.TryGetValue(type, out HelmetInventoryEntry entry)
            && entry.IsUnlocked;
    }

    public int GetAmount(HelmetType type)
    {
        if (!helmets.TryGetValue(type, out HelmetInventoryEntry entry))
            return 0;

        return entry.Amount;
    }

    public void UnlockHelmet(HelmetType type)
    {
        if (type == HelmetType.Default)
            return;

        if (!helmets.TryGetValue(type, out HelmetInventoryEntry entry))
            return;

        if (entry.IsUnlocked)
            return;

        entry.IsUnlocked = true;

        Debug.Log($"[HELMET INVENTORY] {type} desbloqueado.");
    }

    public bool AddHelmet(HelmetType type, int amount = 1)
    {
        if (type == HelmetType.Default || amount <= 0)
            return false;

        if (!helmets.TryGetValue(type, out HelmetInventoryEntry entry))
            return false;

        if (!entry.IsUnlocked)
        {
            Debug.Log(
                $"[HELMET INVENTORY] No se puede almacenar {type}: está bloqueado."
            );

            return false;
        }

        HelmetCatalogEntry catalogEntry =
            HelmetCatalog.Instance?.GetEntry(type);

        if (catalogEntry == null)
        {
            Debug.LogError(
                $"[HELMET INVENTORY] No existe {type} en HelmetCatalog."
            );

            return false;
        }

        int maxAmount = catalogEntry.MaxStored;

        if (entry.Amount >= maxAmount)
            return false;

        entry.Amount = Mathf.Min(
            entry.Amount + amount,
            maxAmount
        );

        Debug.Log(
            $"[HELMET INVENTORY] {type}: " +
            $"{entry.Amount}/{maxAmount}"
        );

        return true;
    }

    public bool ConsumeHelmet(HelmetType type)
    {
        if (!helmets.TryGetValue(type, out HelmetInventoryEntry entry))
            return false;

        if (!entry.IsUnlocked || entry.Amount <= 0)
            return false;

        entry.Amount--;

        Debug.Log(
            $"[HELMET INVENTORY] Consumido {type}. " +
            $"Restantes: {entry.Amount}"
        );

        return true;
    }
}