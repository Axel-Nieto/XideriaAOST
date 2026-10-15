using System.Collections;
using UnityEngine;

public class WaterPrisonSkill : SkillBase
{
    private readonly int damage = 4;
    private readonly int rootDuration = 3;

    public WaterPrisonSkill()
        : base(
            name: "Prisión Marina",
            description: "Daña y evita que el objetivo se mueva durante sus próximos dos turnos.",
            energyCost: 5,
            cooldown: 4,
            minRange: 2,
            maxRange: 6,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Water,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.waterPrison
            : null)
    {
    }

    public override bool CanUse(
    Character user,
    Vector2Int targetCell,
    GridSystem grid)
    {
        if (user == null || grid == null)
            return false;

        if (!user.Stats.HasEnergy(EnergyCost))
            return false;

        if (!grid.IsValidCell(targetCell))
            return false;

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            return false;

        if (target.IsPlayer == user.IsPlayer)
            return false;

        if (!IsInRange(user, target))
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

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

        CharacterAnimationController anim =
            user.GetComponentInChildren<CharacterAnimationController>();

        if (anim != null)
            yield return anim.PlayCastUntilHitFrame();

        int finalDamage = Mathf.Max(1, damage - target.Stats.Defense);

        target.TakeDamage(finalDamage, DamageType.Water, user);
        target.ApplyStatusEffect(new RootEffect(rootDuration));

        if (anim != null)
            yield return anim.WaitCastFinished();
    }

    public override string GetTooltipDetails(Character user)
    {
        return $"Daño: {damage}";
    }
}