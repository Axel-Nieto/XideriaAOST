using System.Collections;
using UnityEngine;

public class WaterBasicSkill : SkillBase
{
    private readonly int damage = 3;
    private readonly int pushDistance = 1;

    public WaterBasicSkill()
        : base(
            name: "Chorro de Agua",
            description: "Dispara agua a distancia y empuja al objetivo.",
            energyCost: 3,
            cooldown: 0,
            minRange: 2,
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

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null) return false;
        if (target.IsPlayer == user.IsPlayer) return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        int finalDamage = Mathf.Max(1, damage + user.Stats.Attack - target.Stats.Defense);
        target.TakeDamage(finalDamage, DamageType.Water);

        TryPush(user, target, grid);

        yield return null;
    }

    private void TryPush(Character user, Character target, GridSystem grid)
    {
        Vector2Int direction = target.GridPosition - user.GridPosition;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            direction = new Vector2Int(direction.x > 0 ? 1 : -1, 0);
        else
            direction = new Vector2Int(0, direction.y > 0 ? 1 : -1);

        Vector2Int destination = target.GridPosition + direction * pushDistance;

        if (grid.IsBlockedForMovement(destination))
            return;

        grid.UnregisterCharacter(target.GridPosition);

        target.transform.position = grid.GridToWorld(destination) + target.VisualOffset;
        target.SetGridPosition(destination);

        grid.RegisterCharacter(destination, target);
    }
}