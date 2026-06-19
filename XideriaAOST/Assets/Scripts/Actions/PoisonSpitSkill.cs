using System.Collections;
using UnityEngine;

public class PoisonSpitSkill : SkillBase
{
    private readonly int damage = 3;
    private readonly int poisonDuration = 3;
    private readonly int poisonDamage = 2;

    public PoisonSpitSkill()
        : base(
            name: "Escupitajo Venenoso",
            description: "Ataque a distancia que inflige veneno.",
            energyCost: 3,
            cooldown: 2,
            minRange: 2,
            maxRange: 4,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Poison)
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

        int finalDamage = Mathf.Max(
            1,
            damage + user.Stats.Attack - target.Stats.Defense
        );

        target.TakeDamage(damage, DamageType.Poison, user);
        target.ApplyStatusEffect(new PoisonEffect(poisonDuration, poisonDamage));

        yield return null;
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? damage
            : damage + user.Stats.Attack;

        return
            $"Daño: {estimatedDamage}";
    }
}