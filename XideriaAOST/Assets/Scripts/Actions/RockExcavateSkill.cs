using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RockExcavateSkill : RockSkillBase
{
    public RockExcavateSkill() : base("Excavar", "Selecciona un obstáculo cardinal adyacente de una casilla de grosor para cruzarlo. Debe caber todo tu cuerpo justo del otro lado.", 1, 0, SkillTargetType.TargetCell, 1, 1,
        icon: SkillIconLibrary.Instance != null ? SkillIconLibrary.Instance.rockExcavate : null) { }
    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid) =>
        Ready(user, grid, user?.Stats?.CurrentEnergy ?? 0) && !user.HasStatusEffect<RootEffect>() &&
        TryGetDestination(user, user.GridPosition, targetCell, grid, out _);

    // AI can use the same geometry by selecting a blocked cell next to the simulated footprint.
    public bool CanUseFromCell(Character user, Vector2Int origin, Vector2Int obstacle, GridSystem grid, int energy) =>
        Ready(user, grid, energy) && !user.HasStatusEffect<RootEffect>() && TryGetDestination(user, origin, obstacle, grid, out _);

    public override bool CanUseFromPosition(Character user, Vector2Int origin, Character target, GridSystem grid, int energy)
    {
        // LarvaAI's Character-target API would execute at the enemy's cell,
        // never at an obstacle. Do not advertise an unusable attack to that AI.
        // Cell-target simulations use CanUseFromCell with the selected obstacle.
        return false;
    }

    public bool TryGetDestination(Character user, Vector2Int origin, Vector2Int obstacle, GridSystem grid, out Vector2Int destination)
    {
        destination = origin;
        if (user == null || grid == null || grid.GetCell(obstacle)?.Type != CellType.Blocked) return false;
        Vector2Int direction = Vector2Int.zero;
        if (obstacle.y >= origin.y && obstacle.y < origin.y + user.GridHeight)
        {
            if (obstacle.x == origin.x - 1) direction = Vector2Int.left;
            if (obstacle.x == origin.x + user.GridWidth) direction = Vector2Int.right;
        }
        if (obstacle.x >= origin.x && obstacle.x < origin.x + user.GridWidth)
        {
            if (obstacle.y == origin.y - 1) direction = Vector2Int.down;
            if (obstacle.y == origin.y + user.GridHeight) direction = Vector2Int.up;
        }
        if (direction == Vector2Int.zero) return false;
        int distance = (direction.x != 0 ? user.GridWidth : user.GridHeight) + 1;
        destination = origin + direction * distance;
        if (!grid.CanCharacterOccupy(user, destination, true)) return false;
        foreach (Vector2Int cell in user.GetOccupiedCells(destination))
            if (grid.GetCell(cell).Type == CellType.Void) return false;
        // Check the entire swept footprint. Only the selected one-cell-deep
        // barrier plane may be blocked; no characters, helmets or void are crossed.
        for (int step = 1; step < distance; step++)
            foreach (Vector2Int cell in user.GetOccupiedCells(origin + direction * step))
            {
                GridCell terrain = grid.GetCell(cell);
                if (terrain == null || terrain.Type == CellType.Void || grid.HasHelmetAt(cell)) return false;
                Character occupant = grid.GetCharacterAt(cell);
                if (occupant != null && occupant != user) return false;
                bool barrier = direction.x != 0 ? cell.x == obstacle.x : cell.y == obstacle.y;
                if (!terrain.Walkable && !barrier) return false;
            }
        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (!CanUse(user, targetCell, grid) ||
            !TryGetDestination(user, user.GridPosition, targetCell, grid, out Vector2Int destination)) yield break;
        user.FaceCell(targetCell);
        var animation = user.GetComponentInChildren<CharacterAnimationController>();
        bool excavating = animation != null && animation.CanPlayExcavate;
        var vfx = user.GetComponent<RockSkillVFX>();
        bool synchronized = excavating && vfx != null && vfx.CanPlayExcavate;
        try
        {
            if (synchronized)
            {
                Vector2Int delta = destination - user.GridPosition;
                Vector2Int direction = new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
                yield return vfx.PlayExcavate(user, grid, user.GridPosition, direction,
                    animation.PlayExcavateEnter(vfx.ExcavateEnterDuration));
            }
            else if (excavating) yield return animation.PlayExcavateEnter();
            else yield return Cast(user);
            // Revalidate after presentation; invalid attempts never spend energy.
            if (!CanUse(user, targetCell, grid) || !TryGetDestination(user, user.GridPosition, targetCell, grid, out destination))
            {
                if (!excavating) yield return FinishCast(user);
                yield break;
            }
            if (!synchronized && vfx != null)
            {
                Vector2Int delta = destination - user.GridPosition;
                Vector2Int direction = new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
                // Without the excavation animation, retain the normal cast before the VFX.
                yield return vfx.PlayExcavate(user, grid, user.GridPosition, direction);
            }
            // The presentation can span several frames; validate the full landing area again.
            if (!CanUse(user, targetCell, grid) || !TryGetDestination(user, user.GridPosition, targetCell, grid, out destination))
            {
                if (!excavating) yield return FinishCast(user);
                yield break;
            }
            Spend(user);
            grid.UnregisterCharacter(user);
            user.SetGridPosition(destination);
            user.transform.position = grid.GridToWorld(destination, user) + user.VisualOffset;
            grid.RegisterCharacter(user);
            user.ProcessCurrentTerrain();
            if (excavating && user.Stats.IsAlive && !user.IsDying)
                yield return animation.PlayExcavateExit(synchronized ? vfx.ExcavateEnterDuration : 0f);
            else if (!excavating) yield return FinishCast(user);
        }
        finally
        {
            if (excavating && animation != null) animation.FinishExcavate();
        }
        // Arrival is committed and the emerge animation has finished. Invalid or
        // interrupted excavations exit before this single notification.
        foreach (PassiveEffect passive in user.GetPassiveEffects())
            if (passive is RockExcavationStrikePassive strike)
                yield return strike.OnExcavationArrived(user, grid);
    }

    public override string GetTooltipDetails(Character user) => RockExcavationStrikePassive.IsEnabled(user)
        ? "Emboscada de Tierra: al salir junto a un enemigo, realizas un Giro de Tierra gratuito una vez por excavaci\u00f3n."
        : "";

    public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid)
    {
        if (!TryGetDestination(user, origin, targetCell, grid, out Vector2Int destination)) yield break;
        yield return targetCell;
        foreach (Vector2Int cell in user.GetOccupiedCells(destination)) yield return cell;
    }
}
