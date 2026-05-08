using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterWaveSkill : SkillBase
{
    private readonly int damage = 4;
    private readonly int lineLength = 4;

    public override int AreaRadius => 0;

    public WaterWaveSkill()
        : base(
            name: "Marea",
            description: "Lanza una ola en línea recta que golpea a los enemigos.",
            energyCost: 5,
            cooldown: 2,
            minRange: 1,
            maxRange: 4,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Water)
    {
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null) return false;
        if (!user.Stats.HasEnergy(EnergyCost)) return false;
        if (!grid.IsValidCell(targetCell)) return false;
        if (!IsInRange(user.GridPosition, targetCell)) return false;
        if (user.IsSkillOnCooldown(this)) return false;

        return GetDirection(user.GridPosition, targetCell) != Vector2Int.zero;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Vector2Int direction = GetDirection(user.GridPosition, targetCell);

        foreach (var pos in GetLineCells(user.GridPosition, direction))
        {
            if (!grid.IsValidCell(pos))
                continue;

            Character target = grid.GetCharacterAt(pos);

            if (target == null || target.IsPlayer == user.IsPlayer)
                continue;

            int finalDamage = Mathf.Max(1, damage + user.Stats.Attack - target.Stats.Defense);
            target.TakeDamage(finalDamage, DamageType.Water);
        }

        yield return null;
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell)
    {
        Vector2Int direction = GetDirection(origin, targetCell);

        foreach (var cell in GetLineCells(origin, direction))
            yield return cell;
    }

    private IEnumerable<Vector2Int> GetLineCells(Vector2Int origin, Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            yield break;

        for (int i = 1; i <= lineLength; i++)
        {
            yield return origin + direction * i;
        }
    }

    private Vector2Int GetDirection(Vector2Int origin, Vector2Int target)
    {
        Vector2Int delta = target - origin;

        if (delta == Vector2Int.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2Int(delta.x > 0 ? 1 : -1, 0);

        return new Vector2Int(0, delta.y > 0 ? 1 : -1);
    }
}