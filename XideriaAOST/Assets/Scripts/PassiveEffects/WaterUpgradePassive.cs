using UnityEngine;

public sealed class WaterUpgradePassive : PassiveEffect
{
    private readonly bool prison;
    public WaterUpgradePassive(bool prison) => this.prison = prison;
    public override string Name => prison ? "Prisi\u00f3n Global" : "Empuje de Marea";
    public override string Description => prison
        ? "La prisi\u00f3n marina ahora afecta a todos los enemigos en el campo"
        : "La ola ahora puede empujar a los enemigos";
    public override Sprite Icon => StatusIconDatabase.Instance != null
        ? StatusIconDatabase.Instance.waterPassiveIcon : null;
    public bool IsEnabled(Character user) => HasUpgrade(user,
        prison ? UpgradeDefinition.WaterPrisonGlobalId : UpgradeDefinition.WaterWavePushId);

    public static bool HasUpgrade(Character user, string id) => user != null && user.IsPlayer &&
        user.EquippedHelmet != null && user.EquippedHelmet.Type == HelmetType.Water &&
        PlayerSessionData.Instance != null && PlayerSessionData.Instance.HasUpgrade(id);
}
