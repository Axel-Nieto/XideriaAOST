using UnityEngine;

public enum UpgradeType
{
    VitalImpulse = 0,
    // Reserved for future implementations. Purchases of this type are rejected for now.
    StatIncrease = 1
}

public enum UpgradePurchaseResult
{
    Success,
    InvalidDefinition,
    InvalidCost,
    UnsupportedUpgrade,
    AlreadyPurchased,
    InsufficientFragments
}

/// <summary>Configuration only. Ownership belongs to RunState, never to this asset.</summary>
[CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Xideria/Upgrade Definition")]
public sealed class UpgradeDefinition : ScriptableObject
{
    public const string VitalImpulseId = "sir_tete.vital_impulse";

    [Tooltip("Stable save identity. Impulso Vital uses sir_tete.vital_impulse. Do not rename purchased IDs.")]
    [SerializeField] private string upgradeId;
    [SerializeField] private string displayName;
    [TextArea, SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("Positive cost in Xideria fragments only. Must be configured before purchasing.")]
    [Min(1), SerializeField] private int fragmentCost;
    [SerializeField] private UpgradeType upgradeType;

    public string Id => upgradeId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int FragmentCost => fragmentCost;
    public UpgradeType Type => upgradeType;
}
