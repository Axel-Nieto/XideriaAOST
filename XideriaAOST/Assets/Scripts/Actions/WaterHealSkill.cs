using System.Collections;
using UnityEngine;

public class WaterHealSkill : SkillBase
{
    private readonly int healAmount = 8;

    public WaterHealSkill()
        : base(
            name: "Agua Curativa",
            description: "Restable parte de la salud del personaje.",
            energyCost: 4,
            cooldown: 3,
            minRange: 0,
            maxRange: 0,
            targetType: SkillTargetType.Self,
            damageType: DamageType.Water,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.waterHeal
            : null)
    {
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null)
            return false;

        if (!user.Stats.HasEnergy(EnergyCost))
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.Stats.ConsumeEnergy(EnergyCost);

        user.StartCooldown(this);

        SkillVFXSpawner.SpawnAttached(SkillVFXLibrary.Instance.waterHeal, user.transform, Vector3.up * 0.5f);

        user.Heal(healAmount);

        yield return null;
    }

    public override string GetTooltipDetails(Character user)
    {
        return $"Curación: {healAmount}";
    }
}
