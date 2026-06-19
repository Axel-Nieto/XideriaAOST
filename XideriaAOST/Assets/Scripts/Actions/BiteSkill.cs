using System.Collections;
using UnityEngine;

public class BiteSkill : SkillBase
{
    public BiteSkill()
        : base(
            name: "Mordida",
            description: "Ataque físico cuerpo a cuerpo.",
            energyCost: 2,
            cooldown: 0,
            minRange: 1,
            maxRange: 1,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Physical)
    {
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null) return false;
        if (!user.Stats.HasEnergy(EnergyCost)) return false;
        if (!grid.IsValidCell(targetCell)) return false;
        if (!IsInRange(user.GridPosition, targetCell)) return false;
        if (user.IsSkillOnCooldown(this)) return false;

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null) return false;
        if (target.IsPlayer == user.IsPlayer) return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.FaceCell(targetCell);

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        int damage = Mathf.Max(1, user.Stats.Attack - target.Stats.Defense);

        target.TakeDamage(damage, DamageType.Physical, user);

        yield return null;
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? 0
            : user.Stats.Attack;

        return $"Daño: {estimatedDamage}";
    }
}