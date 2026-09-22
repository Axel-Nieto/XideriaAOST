[System.Serializable]
public class HelmetInventoryEntry
{
    public HelmetType Type;
    public bool IsUnlocked;
    public int Amount;

    public HelmetInventoryEntry(HelmetType type)
    {
        Type = type;
        IsUnlocked = false;
        Amount = 0;
    }
}