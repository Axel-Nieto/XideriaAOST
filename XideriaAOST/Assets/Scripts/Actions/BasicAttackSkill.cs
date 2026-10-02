using System.Collections;
using UnityEngine;

public class BasicAttackSkill : SkillBase
{
    [SerializeField]
    private Vector3 slashOffset = new Vector3(0f, 1.2f, 0f);

    [SerializeField]
    private float slashHitDelay = 0.25f;

    public BasicAttackSkill()
        : base(
            name: "Ataque Básico",
            description: "Golpea a un enemigo cercano.",
            energyCost: 3,
            cooldown: 0,
            minRange: 1,
            maxRange: 1,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Physical,
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

        yield return BasicAttackVFX.Play(
            target,
            DamageType.Physical,
            slashOffset,
            slashHitDelay);

        int damage = Mathf.Max(
            1,
            user.Stats.Attack - target.Stats.Defense);

        target.TakeDamage(
            damage,
            DamageType.Physical,
            user);

        if (anim != null)
            yield return anim.WaitAttackFinished();

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