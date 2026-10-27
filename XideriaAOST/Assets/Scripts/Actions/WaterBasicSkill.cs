using System.Collections;
using UnityEngine;

public class WaterBasicSkill : SkillBase
{
    [SerializeField]
    private float slashHitDelay = 1.2f;

    private readonly int pushDistance = 1;

    public WaterBasicSkill()
        : base(
            name: "Chorro de Agua",
            description: "Dispara agua a distancia y empuja al objetivo.",
            energyCost: 2,
            cooldown: 0,
            minRange: 2,
            maxRange: 4,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Water,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.basicAttack
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

    public override IEnumerator Execute(
    Character user,
    Vector2Int targetCell,
    GridSystem grid)
    {
        user.FaceCell(targetCell);

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        var anim =
            user.GetComponentInChildren<CharacterAnimationController>();

        if (anim != null)
            yield return anim.PlayAttackUntilHitFrame();

        yield return BasicAttackVFX.PlayWater(
            user,
            target,
            grid,
            slashHitDelay);

        int finalDamage = Mathf.Max(
            1,
            user.Stats.Attack - target.Stats.Defense
        );

        target.TakeDamage(
            finalDamage,
            DamageType.Water,
            user);

        if (!target.Stats.IsAlive || target.IsDying)
            yield break;

        yield return PushSystem.Push(
            user,
            target,
            grid,
            pushDistance
        );

        if (anim != null)
            yield return anim.WaitAttackFinished();
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? 0
            : user.Stats.Attack;

        return
            $"Daño: {estimatedDamage}";
    }
}