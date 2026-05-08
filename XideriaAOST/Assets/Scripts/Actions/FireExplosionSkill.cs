using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireExplosionSkill : SkillBase
{
    private readonly int damage;

    private readonly int aoeRadius;
    public override int AreaRadius => aoeRadius;

    public FireExplosionSkill()
        : base(
            name: "Explosión de Fuego",
            description: "Libera una explosión de fuego alrededor del personaje. Aplica quemadura",
            energyCost: 6,
            cooldown: 3,
            minRange: 0,
            maxRange: 0,
            targetType: SkillTargetType.Self,
            damageType: DamageType.Fire)
    {
        damage = 7;
        aoeRadius = 2;
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null)
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

        return user.Stats.HasEnergy(EnergyCost);
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Vector2Int center = user.GridPosition;

        foreach (var pos in GetAreaCells(center, aoeRadius))
        {
            Character target = grid.GetCharacterAt(pos);

            if (target == null || target == user || target.IsPlayer == user.IsPlayer)
                continue;

            int finalDamage = Mathf.Max(1, damage + user.Stats.Attack - target.Stats.Defense);

            target.TakeDamage(finalDamage, DamageType.Fire);
            target.ApplyStatusEffect(new BurnEffect(2, 4));
        }

        yield return null;
    }

    private IEnumerable<Vector2Int> GetAreaCells(Vector2Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int distance = Mathf.Abs(x) + Mathf.Abs(y);

                if (distance <= radius)
                    yield return center + new Vector2Int(x, y);
            }
        }
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell)
    {
        foreach (var cell in GetAreaCells(origin, aoeRadius))
            yield return cell;
    }
}