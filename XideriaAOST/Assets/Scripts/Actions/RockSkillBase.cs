using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RockSkillBase : SkillBase
{
    protected RockSkillBase(string name, string description, int cost, int cooldown,
        SkillTargetType targetType = SkillTargetType.Self, int minRange = 0, int maxRange = 0, Sprite icon = null)
        : base(name, description, cost, cooldown, minRange, maxRange, targetType, DamageType.Physical, icon) { }

    protected bool Ready(Character user, GridSystem grid, int energy) =>
        user != null && user.Stats != null && user.Stats.IsAlive && !user.IsDying &&
        grid != null && energy >= EnergyCost && !user.IsSkillOnCooldown(this);

    protected void Spend(Character user)
    {
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);
    }

    public static IEnumerable<Vector2Int> Area(Vector2Int origin, int width, int height, int radius)
    {
        for (int x = origin.x - radius; x < origin.x + width + radius; x++)
            for (int y = origin.y - radius; y < origin.y + height + radius; y++)
            {
                int dx = Mathf.Max(origin.x - x, 0, x - (origin.x + width - 1));
                int dy = Mathf.Max(origin.y - y, 0, y - (origin.y + height - 1));
                if (dx + dy <= radius) yield return new Vector2Int(x, y);
            }
    }

    protected List<Character> Enemies(Character user, GridSystem grid, Vector2Int origin, int radius)
    {
        var result = new List<Character>();
        foreach (Vector2Int cell in Area(origin, user.GridWidth, user.GridHeight, radius))
        {
            Character target = grid.GetCharacterAt(cell);
            if (target != null && target != user && target.IsPlayer != user.IsPlayer &&
                target.Stats != null && target.Stats.IsAlive && !target.IsDying && !result.Contains(target))
                result.Add(target);
        }
        return result;
    }

    protected static void Damage(Character user, Character target) =>
        target.TakeDamage(Mathf.Max(1, user.Stats.Attack - target.Stats.Defense), DamageType.Physical, user);

    protected static IEnumerator Cast(Character user)
    {
        var animation = user.GetComponentInChildren<CharacterAnimationController>();
        if (animation != null) yield return animation.PlayCastUntilHitFrame();
    }

    protected static IEnumerator FinishCast(Character user)
    {
        var animation = user.GetComponentInChildren<CharacterAnimationController>();
        if (animation != null) yield return animation.WaitCastFinished();
    }
}
