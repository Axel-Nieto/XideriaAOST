using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class WaterWaveSkill
{
    private sealed class PushWavePlan
    {
        public readonly List<Character> Targets = new();
        public readonly Dictionary<Character, Vector2Int> Positions = new();
        public List<Vector2Int> PlayerPath;
        public bool EnemyContact;
        public Vector2Int? Retreat;
    }

    private static bool HasPushUpgrade(Character user) =>
        WaterUpgradePassive.HasUpgrade(user, UpgradeDefinition.WaterWavePushId);

    private bool CanUsePushWave(Character user, Vector2Int origin, Vector2Int direction, GridSystem grid)
    {
        if (user.Stats == null || !user.Stats.IsAlive || user.IsDying || user.Movement == null ||
            !user.Movement.isActiveAndEnabled || !user.gameObject.activeInHierarchy ||
            user.HasStatusEffect<RootEffect>()) return false;
        PushWavePlan plan = PlanPushWave(user, origin, direction, grid);
        return plan.PlayerPath.Count > 0 || plan.EnemyContact;
    }

    private PushWavePlan PlanPushWave(Character user, Vector2Int origin, Vector2Int direction, GridSystem grid)
    {
        var plan = new PushWavePlan();
        var targets = new HashSet<Character>();
        foreach (Vector2Int cell in GetPushWaveCells(user, origin, direction))
        {
            Character target = grid.GetCharacterAt(cell);
            if (IsWaveEnemy(user, target)) targets.Add(target);
        }
        plan.Targets.AddRange(targets);
        // Resolve the farthest enemies first. A nearer enemy must stop before
        // the final footprint of an enemy already pushed in the same lane.
        plan.Targets.Sort((a, b) => TrailingProjection(b, b.GridPosition, origin, direction)
            .CompareTo(TrailingProjection(a, a.GridPosition, origin, direction)));
        plan.Positions[user] = origin;
        foreach (Character target in plan.Targets) plan.Positions[target] = target.GridPosition;
        int limit = PushWaveLimit(user, origin, direction);
        foreach (Character target in plan.Targets)
        {
            if (target.BlocksPush()) continue;
            Vector2Int start = plan.Positions[target];
            int distance = Mathf.Max(0, limit - TrailingProjection(target, start, origin, direction));
            Vector2Int end = start;
            for (int step = 1; step <= distance; step++)
            {
                Vector2Int next = start + direction * step;
                if (!CanOccupyWavePosition(target, next, grid, plan.Positions)) break;
                end = next;
            }
            plan.Positions[target] = end;
        }
        plan.PlayerPath = BuildPushWavePath(user, origin, direction, grid, plan.Positions,
            out plan.EnemyContact, out plan.Retreat);
        return plan;
    }

    private IEnumerator ExecutePushWave(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (!CanUse(user, targetCell, grid)) yield break;
        Vector2Int origin = user.GridPosition;
        Vector2Int direction = GetDirection(origin, targetCell);
        PushWavePlan plan = PlanPushWave(user, origin, direction, grid);
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);
        user.FaceDirection(direction);
        SkillVFXSpawner.SpawnMovingVFX(SkillVFXLibrary.Instance != null ? SkillVFXLibrary.Instance.waterWave : null,
            grid.GridToWorld(origin) + Vector3.up * 0.2f,
            grid.GridToWorld(origin + direction * MaxRange) + Vector3.up * 0.2f,
            GetRotationFromDirection(direction), 8f);

        int limit = PushWaveLimit(user, origin, direction);
        foreach (Character target in plan.Targets)
        {
            if (!IsWaveEnemy(user, target) || target.BlocksPush()) continue;
            int distance = Mathf.Max(0, limit - TrailingProjection(target, target.GridPosition, origin, direction));
            if (distance > 0)
                yield return PushSystem.PushInDirection(target, direction, distance, grid, source: user);
        }
        if (!user.Stats.IsAlive || user.IsDying) yield break;
        // Use the real board again after the pushes and their terrain effects.
        List<Vector2Int> path = BuildPushWavePath(user, origin, direction, grid, null, out _, out _);
        if (path.Count > 0) yield return user.Movement.MoveAlongRoutine(path);
        if (!user.Stats.IsAlive || user.IsDying) yield break;
        Vector2Int collision = user.GridPosition + direction;
        if (ProjectWave(user.GridPosition - origin, direction) < MaxRange &&
            !grid.CanCharacterOccupy(user, collision, true) &&
            FindWaveContact(user, collision, grid, null, true))
            yield return PushSystem.PushInDirection(user, -direction, 1, grid);

        // Hit the original wave targets once, even when they now occupy cells
        // beyond the wave's range. Movement does not multiply damage or slow.
        foreach (Character target in plan.Targets)
        {
            if (!IsWaveEnemy(user, target)) continue;
            target.TakeDamage(Mathf.Max(1, damage + user.Stats.Attack - target.Stats.Defense), DamageType.Water, user);
            if (target.Stats.IsAlive && !target.IsDying) target.ApplyStatusEffect(new SlowEffect(slowDuration));
        }
    }

    private List<Vector2Int> BuildPushWavePath(Character user, Vector2Int origin, Vector2Int direction,
        GridSystem grid, Dictionary<Character, Vector2Int> positions, out bool enemyContact, out Vector2Int? retreat)
    {
        var path = new List<Vector2Int>();
        enemyContact = false;
        retreat = null;
        Vector2Int current = origin;
        for (int step = 1; step <= MaxRange; step++)
        {
            Vector2Int next = origin + direction * step;
            if (!CanOccupyWavePosition(user, next, grid, positions))
            {
                enemyContact = FindWaveContact(user, next, grid, positions, false);
                if (!user.BlocksPush() && FindWaveContact(user, next, grid, positions, true) &&
                    CanOccupyWavePosition(user, current - direction, grid, positions))
                    retreat = current - direction;
                break;
            }
            path.Add(next);
            current = next;
        }
        return path;
    }

    private static bool CanOccupyWavePosition(Character character, Vector2Int origin, GridSystem grid,
        Dictionary<Character, Vector2Int> positions)
    {
        if (positions == null) return grid.CanCharacterOccupy(character, origin, true);
        foreach (Vector2Int cell in character.GetOccupiedCells(origin))
        {
            if (!grid.IsValidCell(cell) || !grid.IsWalkable(cell) || grid.HasHelmetAt(cell)) return false;
            Character occupant = grid.GetCharacterAt(cell);
            if (occupant != null && occupant != character && !positions.ContainsKey(occupant)) return false;
            foreach (var entry in positions)
                if (entry.Key != character && OccupiesWaveCell(entry.Key, entry.Value, cell)) return false;
        }
        return true;
    }

    private static bool FindWaveContact(Character user, Vector2Int next, GridSystem grid,
        Dictionary<Character, Vector2Int> positions, bool immuneOnly)
    {
        foreach (Vector2Int cell in user.GetOccupiedCells(next))
        {
            Character occupant = grid.GetCharacterAt(cell);
            if (occupant != null && (positions == null || !positions.ContainsKey(occupant)) &&
                IsWaveEnemy(user, occupant) && (!immuneOnly || occupant.BlocksPush())) return true;
            if (positions == null) continue;
            foreach (var entry in positions)
                if (IsWaveEnemy(user, entry.Key) && (!immuneOnly || entry.Key.BlocksPush()) &&
                    OccupiesWaveCell(entry.Key, entry.Value, cell)) return true;
        }
        return false;
    }

    private static bool OccupiesWaveCell(Character character, Vector2Int origin, Vector2Int cell) =>
        cell.x >= origin.x && cell.x < origin.x + character.GridWidth &&
        cell.y >= origin.y && cell.y < origin.y + character.GridHeight;

    private static bool IsWaveEnemy(Character user, Character target) => target != null && target != user &&
        target.IsPlayer != user.IsPlayer && target.Stats != null && target.Stats.IsAlive && !target.IsDying;

    private int PushWaveLimit(Character user, Vector2Int origin, Vector2Int direction)
    {
        int leading = 0;
        foreach (Vector2Int cell in user.GetOccupiedCells(origin)) leading = Mathf.Max(leading, ProjectWave(cell - origin, direction));
        return MaxRange + leading + 1;
    }

    private static int TrailingProjection(Character character, Vector2Int position, Vector2Int origin, Vector2Int direction)
    {
        int trailing = int.MaxValue;
        foreach (Vector2Int cell in character.GetOccupiedCells(position)) trailing = Mathf.Min(trailing, ProjectWave(cell - origin, direction));
        return trailing;
    }

    private static int ProjectWave(Vector2Int delta, Vector2Int direction) => delta.x * direction.x + delta.y * direction.y;

    private IEnumerable<Vector2Int> GetPushWaveCells(Character user, Vector2Int origin, Vector2Int direction)
    {
        if (direction == Vector2Int.zero) yield break;
        var cells = new HashSet<Vector2Int>();
        Vector2Int side = GetSideDirection(direction);
        foreach (Vector2Int occupied in user.GetOccupiedCells(origin))
            for (int step = 1; step <= MaxRange; step++)
            {
                Vector2Int center = occupied + direction * step;
                cells.Add(center); cells.Add(center + side); cells.Add(center - side);
            }
        foreach (Vector2Int cell in cells) yield return cell;
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid)
    {
        if (!HasPushUpgrade(user))
        {
            foreach (Vector2Int cell in GetPreviewCells(origin, targetCell)) yield return cell;
            yield break;
        }
        Vector2Int direction = GetDirection(origin, targetCell);
        if (grid == null || direction == Vector2Int.zero) yield break;
        PushWavePlan plan = PlanPushWave(user, origin, direction, grid);
        var preview = new HashSet<Vector2Int>(GetPushWaveCells(user, origin, direction));
        foreach (Character target in plan.Targets)
            foreach (Vector2Int cell in target.GetOccupiedCells(plan.Positions[target])) preview.Add(cell);
        if (plan.Retreat.HasValue)
            foreach (Vector2Int cell in user.GetOccupiedCells(plan.Retreat.Value)) preview.Add(cell);
        foreach (Vector2Int cell in preview) if (grid.IsValidCell(cell)) yield return cell;
    }
}
