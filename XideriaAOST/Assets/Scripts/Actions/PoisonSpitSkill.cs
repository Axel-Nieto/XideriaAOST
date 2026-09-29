using System.Collections;
using UnityEngine;

public class PoisonSpitSkill : SkillBase
{
    private readonly int damage;
    private readonly int poisonDuration;
    private readonly int poisonDamage;

    public PoisonSpitSkill(
    int damage = 3,
    int minRange = 2,
    int maxRange = 4,
    int poisonDuration = 3,
    int poisonDamage = 2)
    : base(
        name: "Escupitajo Venenoso",
        description: "Ataque a distancia que inflige veneno.",
        energyCost: 3,
        cooldown: 3,
        minRange: minRange,
        maxRange: maxRange,
        targetType: SkillTargetType.TargetCell,
        damageType: DamageType.Poison)
    {
        this.damage = damage;
        this.poisonDuration = poisonDuration;
        this.poisonDamage = poisonDamage;
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

        Vector3 startPos = grid.GridToWorld(user.GridPosition) + Vector3.up * 1f;
        Vector3 endPos = grid.GridToWorld(targetCell) + Vector3.up * 1f;

        yield return SkillVFXSpawner.SpawnProjectile(
            SkillVFXLibrary.Instance.poisonSpit,
            null,
            startPos,
            endPos,
            speed: 10f
        );

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