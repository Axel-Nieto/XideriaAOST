using System.Collections;
using UnityEngine;

public sealed class LarvaPullSkill : LarvaSpecialSkill
{
    public LarvaPullSkill(int energyCost = 3, int cooldown = 3)
        : base("Arrastre de Aire", "Inflige daño físico y arrastra una casilla al objetivo alineado.",
            4, 6, DamageType.Physical, energyCost: energyCost, cooldown: cooldown) { }

    protected override bool IsGeometryValid(Character user, Vector2Int origin, Character target) =>
        GetPullDirection(user, origin, target) != Vector2Int.zero;

    private static Vector2Int GetPullDirection(Character user, Vector2Int origin, Character target)
    {
        foreach (Vector2Int sourceCell in user.GetOccupiedCells(origin))
            foreach (Vector2Int targetCell in target.GetOccupiedCells())
            {
                Vector2Int delta = sourceCell - targetCell;
                if (delta.x == 0 && delta.y != 0)
                    return new Vector2Int(0, delta.y > 0 ? 1 : -1);
                if (delta.y == 0 && delta.x != 0)
                    return new Vector2Int(delta.x > 0 ? 1 : -1, 0);
            }
        return Vector2Int.zero;
    }

    protected override IEnumerator ApplyEffect(Character user, Character target, GridSystem grid)
    {
        DealDamage(user, target);
        if (target.Stats.IsAlive && !target.IsDying && !target.BlocksPush())
            yield return PushSystem.PushInDirection(target,
                GetPullDirection(user, user.GridPosition, target), 1, grid);
    }
}
