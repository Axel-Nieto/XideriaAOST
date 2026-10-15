using System.Collections;
using UnityEngine;

public class HornStrikeSkill : SkillBase
{
    private readonly int damage;
    private readonly int pushDistance;

    public HornStrikeSkill(
        int damage = 3,
        int pushDistance = 1)
        : base(
            name: "Cornada",
            description: "Ataca con los cuernos e impulsa al objetivo hacia atr�s.",
            energyCost: 3,
            cooldown: 0,
            minRange: 1,
            maxRange: 1,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Physical,
            icon: null)
    {
        this.damage = damage;
        this.pushDistance = pushDistance;
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

        if (user.IsSkillOnCooldown(this))
            return false;

        if (!grid.IsValidCell(targetCell))
            return false;

        Character target =
            grid.GetCharacterAt(targetCell);

        if (target == null)
            return false;

        if (target.IsPlayer == user.IsPlayer)
            return false;

        if (!IsInRange(user, target))
            return false;

        return true;
    }

    public override IEnumerator Execute(
        Character user,
        Vector2Int targetCell,
        GridSystem grid)
    {
        Character target =
            grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        user.FaceCell(targetCell);

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        CharacterAnimationController anim =
            user.GetComponentInChildren<CharacterAnimationController>();

        if (anim != null)
            yield return anim.PlayAttackUntilHitFrame();

        int finalDamage = Mathf.Max(
            1,
            damage
            + user.Stats.Attack
            - target.Stats.Defense
        );

        target.TakeDamage(
            finalDamage,
            DamageType.Physical,
            user
        );

        if (target.Stats.IsAlive &&
            !target.IsDying)
        {
            yield return PushSystem.Push(
                user,
                target,
                grid,
                pushDistance
            );
        }

        if (anim != null)
            yield return anim.WaitAttackFinished();
    }

    public override string GetTooltipDetails(
        Character user)
    {
        int estimatedDamage =
            user == null
                ? damage
                : damage + user.Stats.Attack;

        return
            $"Da�o: {estimatedDamage}\n" +
            $"Empuje: {pushDistance}";
    }
}
