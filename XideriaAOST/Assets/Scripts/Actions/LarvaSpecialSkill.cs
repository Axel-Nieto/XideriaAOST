using System.Collections;
using UnityEngine;

// Shared only by the new elemental larvas. Actual and simulated targeting use
// the same footprint and energy checks, without changing existing skills.
public abstract class LarvaSpecialSkill : SkillBase
{
    private readonly int baseDamage;

    protected LarvaSpecialSkill(string name, string description, int minRange, int maxRange,
        DamageType damageType, int baseDamage = 0, int energyCost = 3, int cooldown = 3)
        : base(name, description, energyCost, cooldown, minRange, maxRange,
            SkillTargetType.TargetCell, damageType)
    {
        this.baseDamage = baseDamage;
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || user.Stats == null || grid == null || !grid.IsValidCell(targetCell))
            return false;

        return CanUseFromPosition(user, user.GridPosition, grid.GetCharacterAt(targetCell),
            grid, user.Stats.CurrentEnergy);
    }

    public override bool CanUseFromPosition(Character user, Vector2Int simulatedPosition,
        Character target, GridSystem grid, int availableEnergy)
    {
        return user != null && user.Stats != null && user.Stats.IsAlive && !user.IsDying &&
            target != null && target.Stats != null && target.Stats.IsAlive && !target.IsDying &&
            target.IsPlayer != user.IsPlayer && grid != null && grid.IsValidCell(target.GridPosition) &&
            availableEnergy >= EnergyCost && !user.IsSkillOnCooldown(this) &&
            IsInRange(user, simulatedPosition, target) && IsGeometryValid(user, simulatedPosition, target);
    }

    protected virtual bool IsGeometryValid(Character user, Vector2Int origin, Character target) => true;

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        // Execute is also called directly by the AI; invalid attempts must not spend energy.
        if (!CanUse(user, targetCell, grid))
            yield break;

        Character target = grid.GetCharacterAt(targetCell);
        user.FaceCell(targetCell);
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        CharacterAnimationController animation = user.GetComponentInChildren<CharacterAnimationController>();
        if (animation != null)
            yield return animation.PlayCastUntilHitFrame();

        if (target != null && target.Stats.IsAlive && !target.IsDying)
            yield return ApplyEffect(user, target, grid);

        if (animation != null)
            yield return animation.WaitCastFinished();
    }

    protected abstract IEnumerator ApplyEffect(Character user, Character target, GridSystem grid);

    protected void DealDamage(Character user, Character target)
    {
        target.TakeDamage(Mathf.Max(1, baseDamage + user.Stats.Attack - target.Stats.Defense),
            DamageType, user);
    }

    public override string GetTooltipDetails(Character user) =>
        $"Daño: {baseDamage + (user != null && user.Stats != null ? user.Stats.Attack : 0)}";
}
