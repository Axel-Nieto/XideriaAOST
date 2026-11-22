using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class WaterPrisonSkill
{
    private static bool HasGlobalUpgrade(Character user) =>
        WaterUpgradePassive.HasUpgrade(user, UpgradeDefinition.WaterPrisonGlobalId);

    public override SkillTargetType GetTargetType(Character user) =>
        HasGlobalUpgrade(user) ? SkillTargetType.Self : base.GetTargetType(user);

    public override string GetRangeDescription(Character user) =>
        HasGlobalUpgrade(user) ? "Todo el campo" : base.GetRangeDescription(user);

    private bool CanUseGlobalPrison(Character user, GridSystem grid, int energy) =>
        user != null && grid != null && user.Stats != null && user.Stats.IsAlive && !user.IsDying &&
        energy >= EnergyCost && !user.IsSkillOnCooldown(this) && GetPrisonTargets(user, grid).Count > 0;

    private static List<Character> GetPrisonTargets(Character user, GridSystem grid)
    {
        // The grid's effective registry is authoritative. Do not pick enemies
        // from another board/scene or count each occupied cell as another target.
        var targets = new HashSet<Character>();
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                Character enemy = grid.GetCharacterAt(new Vector2Int(x, y));
                if (enemy != null && enemy != user && enemy.IsPlayer != user.IsPlayer &&
                    enemy.Stats != null && enemy.Stats.IsAlive && !enemy.IsDying)
                    targets.Add(enemy);
            }
        return new List<Character>(targets);
    }

    private IEnumerator ExecuteGlobalPrison(Character user, GridSystem grid)
    {
        if (!CanUseGlobalPrison(user, grid, user?.Stats?.CurrentEnergy ?? 0)) yield break;
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);
        var animation = user.GetComponentInChildren<CharacterAnimationController>();
        if (animation != null) yield return animation.PlayCastUntilHitFrame();
        if (user.Stats.IsAlive && !user.IsDying)
            foreach (Character enemy in GetPrisonTargets(user, grid))
            {
                enemy.TakeDamage(Mathf.Max(1, damage + user.Stats.Attack - enemy.Stats.Defense), DamageType.Water, user);
                if (enemy.Stats.IsAlive && !enemy.IsDying) enemy.ApplyStatusEffect(new RootEffect(rootDuration));
            }
        if (animation != null) yield return animation.WaitCastFinished();
    }

    public override bool CanUseFromPosition(Character user, Vector2Int origin, Character target, GridSystem grid, int availableEnergy)
    {
        if (!HasGlobalUpgrade(user)) return base.CanUseFromPosition(user, origin, target, grid, availableEnergy);
        return target != null && CanUseGlobalPrison(user, grid, availableEnergy) && GetPrisonTargets(user, grid).Contains(target);
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid)
    {
        if (!HasGlobalUpgrade(user))
        {
            foreach (Vector2Int cell in base.GetPreviewCells(user, origin, targetCell, grid)) yield return cell;
            yield break;
        }
        if (grid == null) yield break;
        foreach (Character target in GetPrisonTargets(user, grid))
            foreach (Vector2Int cell in target.GetOccupiedCells()) yield return cell;
    }
}
