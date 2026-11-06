using UnityEngine;

public enum UpgradeType
{
    VitalImpulse = 0,
    // Reserved for future implementations. Purchases of this type are rejected for now.
    StatIncrease = 1,
    FireballCooldown = 2,
    FlameDashCooldown = 3,
    MaxHealth = 4,
    BaseDefense = 5,
    BaseDamage = 6,
    HelmetDurability = 7
}

public enum UpgradePurchaseResult
{
    Success,
    InvalidDefinition,
    InvalidCost,
    UnsupportedUpgrade,
    AlreadyPurchased,
    InsufficientFragments,
    InsufficientGold
}

/// <summary>Configuration only. Ownership belongs to RunState, never to this asset.</summary>
[CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Xideria/Upgrade Definition")]
public sealed class UpgradeDefinition : ScriptableObject
{
    public const string VitalImpulseId = "sir_tete.vital_impulse";
    public const string FireballCooldownId = "fire.fireball_cooldown";
    public const string FlameDashCooldownId = "fire.flame_dash_cooldown";
    public const string MaxHealthId = "sir_tete.max_health";
    public const string BaseDefenseId = "sir_tete.base_defense";
    public const string BaseDamageId = "sir_tete.base_damage";
    public const string HelmetDurabilityId = "sir_tete.helmet_durability";

    [Tooltip("Stable save identity. Impulso Vital uses sir_tete.vital_impulse. Do not rename purchased IDs.")]
    [SerializeField] private string upgradeId;
    [SerializeField] private string displayName;
    [TextArea, SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("Positive cost in Xideria fragments only. Must be configured before purchasing.")]
    [Min(1), SerializeField] private int fragmentCost;
    [Min(1), SerializeField] private int goldCost;
    [SerializeField] private UpgradeType upgradeType;

    public string Id => upgradeId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int FragmentCost => fragmentCost;
    public RunCurrency Currency => Type == UpgradeType.FireballCooldown || Type == UpgradeType.FlameDashCooldown
        ? RunCurrency.Gold : RunCurrency.XideriaFragments;
    public int Cost => Currency == RunCurrency.Gold ? goldCost : fragmentCost;
    public bool IsSupported => Type switch
    {
        UpgradeType.VitalImpulse => Id == VitalImpulseId,
        UpgradeType.FireballCooldown => Id == FireballCooldownId,
        UpgradeType.FlameDashCooldown => Id == FlameDashCooldownId,
        UpgradeType.MaxHealth => Id == MaxHealthId,
        UpgradeType.BaseDefense => Id == BaseDefenseId,
        UpgradeType.BaseDamage => Id == BaseDamageId,
        UpgradeType.HelmetDurability => Id == HelmetDurabilityId,
        _ => false
    };
    public UpgradeType Type => upgradeType;
}
