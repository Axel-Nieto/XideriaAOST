using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterWaveSkill : SkillBase
{
    private readonly int damage = 4;
    private readonly int slowDuration = 3;

    public override int AreaRadius => 0;

    public WaterWaveSkill()
        : base(
            name: "Marea",
            description: "Te reposicionas en línea recta y lanzas una ola que daña y ralentiza.",
            energyCost: 5,
            cooldown: 2,
            minRange: 1,
            maxRange: 6,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Water,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.waterWave
            : null)
    {
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null) return false;
        if (!user.Stats.HasEnergy(EnergyCost)) return false;
        if (!grid.IsValidCell(targetCell)) return false;
        if (!IsInRange(user.GridPosition, targetCell)) return false;
        if (user.IsSkillOnCooldown(this)) return false;

        Vector2Int direction = GetDirection(user.GridPosition, targetCell);

        if (direction == Vector2Int.zero)
            return false;

        List<Vector2Int> movementPath = BuildMovementPath(user.GridPosition, direction, grid);

        return movementPath.Count > 0;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        Vector2Int originalPosition = user.GridPosition;

        Vector2Int direction = GetDirection(originalPosition, targetCell);

        if (direction == Vector2Int.zero)
            yield break;

        List<Vector2Int> movementPath = BuildMovementPath(
            originalPosition,
            direction,
            grid
        );

        if (movementPath.Count == 0)
            yield break;

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Vector3 waveStart =
            grid.GridToWorld(originalPosition) + Vector3.up * 0.2f;

        Vector3 waveEnd =
            grid.GridToWorld(originalPosition + direction * MaxRange) + Vector3.up * 0.2f;

        SkillVFXSpawner.SpawnMovingVFX(
            SkillVFXLibrary.Instance.waterWave,
            waveStart,
            waveEnd,
            GetRotationFromDirection(direction),
            speed: 8f
        );

        yield return user.Movement.MoveAlongRoutine(movementPath);

        foreach (var pos in GetWaveCells(originalPosition, direction))
        {
            if (!grid.IsValidCell(pos))
                continue;

            Character target = grid.GetCharacterAt(pos);

            if (target == null || target.IsPlayer == user.IsPlayer)
                continue;

            int finalDamage = Mathf.Max(
                1,
                damage + user.Stats.Attack - target.Stats.Defense
            );

            target.TakeDamage(finalDamage, DamageType.Water);
            target.ApplyStatusEffect(new SlowEffect(slowDuration));
        }

        yield return null;
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell)
    {
        Vector2Int direction = GetDirection(origin, targetCell);

        foreach (var cell in GetWaveCells(origin, direction))
            yield return cell;
    }

    private List<Vector2Int> BuildMovementPath(
        Vector2Int origin,
        Vector2Int direction,
        GridSystem grid)
    {
        List<Vector2Int> path = new();

        for (int i = 1; i <= MaxRange; i++)
        {
            Vector2Int next = origin + direction * i;

            if (grid.IsBlockedForMovement(next))
                break;

            path.Add(next);
        }

        return path;
    }

    private IEnumerable<Vector2Int> GetWaveCells(Vector2Int origin, Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            yield break;

        Vector2Int side = GetSideDirection(direction);

        for (int i = 1; i <= MaxRange; i++)
        {
            Vector2Int center = origin + direction * i;

            yield return center;
            yield return center + side;
            yield return center - side;
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

    private Vector2Int GetSideDirection(Vector2Int direction)
    {
        if (direction.x != 0)
            return Vector2Int.up;

        return Vector2Int.right;
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? damage
            : damage + user.Stats.Attack;

        return
            $"Daño: {estimatedDamage}";
    }

    private Quaternion GetRotationFromDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return Quaternion.Euler(0f, 180f, 0f);

        if (direction == Vector2Int.down)
            return Quaternion.Euler(0f, 0f, 0f);

        if (direction == Vector2Int.right)
            return Quaternion.Euler(0f, -90f, 0f);

        if (direction == Vector2Int.left)
            return Quaternion.Euler(0f, 90f, 0f);

        return Quaternion.identity;
    }
}