using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlameDashSkill : SkillBase
{
    private readonly int damage = 4;
    private readonly int aoeRadius = 1;
    private readonly float dashSpeed = 14f;
    public override int AreaRadius => aoeRadius;

    public FlameDashSkill()
        : base(
            name: "Dash Llameante",
            description: "Te desplazas hacia una celda y quemas enemigos adyacentes al detenerte.",
            energyCost: 4,
            cooldown: 2,
            minRange: 2,
            maxRange: 8,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Fire,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.flameDash
            : null)
    {
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null)
            return false;

        if (!user.Stats.HasEnergy(EnergyCost))
            return false;

        if (!grid.IsValidCell(targetCell))
            return false;

        if (!IsInRange(user.GridPosition, targetCell))
            return false;

        Vector2Int direction = GetDirection(user.GridPosition, targetCell);

        if (direction == Vector2Int.zero)
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

        return BuildDashPath(user.GridPosition, targetCell, direction, grid).Count > 0;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        Vector2Int direction = GetDirection(user.GridPosition, targetCell);

        if (direction == Vector2Int.zero)
            yield break;

        List<Vector2Int> path = BuildDashPath(user.GridPosition, targetCell,direction, grid);

        if (path.Count == 0)
            yield break;

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        GameObject trail = null;

        if (SkillVFXLibrary.Instance.flameDashTrail != null)
        {
            trail = Object.Instantiate(
                SkillVFXLibrary.Instance.flameDashTrail,
                user.transform
            );

            trail.transform.localPosition = Vector3.zero;
            trail.transform.localRotation = Quaternion.identity;
        }

        yield return user.Movement.MoveAlongRoutine(path, dashSpeed);

        if (trail != null)
        {
            trail.transform.SetParent(null);
            Object.Destroy(trail, 1.5f);
        }

        SkillVFXSpawner.SpawnAtCell(
            SkillVFXLibrary.Instance.flameDashImpact,
            grid,
            user.GridPosition,
            Vector3.up * 0.05f
        );

        foreach (var pos in GetAreaCells(user.GridPosition, aoeRadius))
        {
            if (!grid.IsValidCell(pos))
                continue;

            Character target = grid.GetCharacterAt(pos);

            if (target == null || target == user || target.IsPlayer == user.IsPlayer)
                continue;

            int finalDamage = Mathf.Max(
                1,
                damage + user.Stats.Attack - target.Stats.Defense
            );

            target.TakeDamage(finalDamage, DamageType.Fire, user);
        }
    }

    private Vector2Int GetDirection(Vector2Int origin, Vector2Int target)
    {
        Vector2Int delta = target - origin;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2Int(delta.x > 0 ? 1 : -1, 0);

        if (delta.y != 0)
            return new Vector2Int(0, delta.y > 0 ? 1 : -1);

        return Vector2Int.zero;
    }

    private List<Vector2Int> BuildDashPath(Vector2Int origin, Vector2Int targetCell, Vector2Int direction, GridSystem grid)
    {
        List<Vector2Int> path = new();

        int distanceToTarget =
        Mathf.Abs(targetCell.x - origin.x) +
        Mathf.Abs(targetCell.y - origin.y);

        int dashDistance = Mathf.Min(MaxRange, distanceToTarget);

        for (int i = 1; i <= dashDistance; i++)
        {
            Vector2Int next = origin + direction * i;

            if (grid.IsBlockedForMovement(next))
                break;

            path.Add(next);
        }

        return path;
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
        Vector2Int direction = GetDirection(origin, targetCell);

        if (direction == Vector2Int.zero)
            yield break;

        int distanceToTarget =
            Mathf.Abs(targetCell.x - origin.x) +
            Mathf.Abs(targetCell.y - origin.y);

        int dashDistance = Mathf.Min(MaxRange, distanceToTarget);

        Vector2Int finalCell = origin;

        for (int i = 1; i <= dashDistance; i++)
        {
            Vector2Int pathCell = origin + direction * i;

            finalCell = pathCell;

            yield return pathCell;
        }

        foreach (var cell in GetAreaCells(finalCell, aoeRadius))
            yield return cell;
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? damage
            : damage + user.Stats.Attack;

        return
            $"Daño: {estimatedDamage}";
    }
}