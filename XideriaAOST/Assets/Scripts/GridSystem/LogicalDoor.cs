using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Resolves the existing terrain exit cells into logical doors without changing occupancy.</summary>
public sealed class LogicalDoor
{
    public string Id { get; private set; }
    public CellType Type { get; private set; }
    public readonly List<Vector2Int> Cells = new();
    public IReadOnlyList<string> TargetScenes { get; private set; }
    public bool IsValid { get; private set; } = true;
    public Transform RewardIconAnchor { get; private set; }

    public static List<LogicalDoor> Build(IReadOnlyList<TerrainCellData> terrain)
    {
        var exits = new List<TerrainCellData>();
        foreach (TerrainCellData cell in terrain)
            if (cell.Type == CellType.ExitRun || cell.Type == CellType.ExitTutorial)
                exits.Add(cell);
        exits.Sort((a, b) => a.Position.x != b.Position.x ?
            a.Position.x.CompareTo(b.Position.x) : a.Position.y.CompareTo(b.Position.y));

        var doors = new List<LogicalDoor>();
        var assigned = new HashSet<TerrainCellData>();
        foreach (TerrainCellData first in exits)
        {
            if (!assigned.Add(first))
                continue;

            bool explicitId = !string.IsNullOrWhiteSpace(first.LogicalDoorId);
            var door = new LogicalDoor
            {
                Id = explicitId ? "id:" + first.LogicalDoorId :
                    $"exit:{(int)first.Type}:{first.Position.x},{first.Position.y}",
                Type = first.Type, TargetScenes = first.TargetScenes, RewardIconAnchor = first.RewardIconAnchor
            };
            door.Cells.Add(first.Position);
            for (int cursor = 0; cursor < door.Cells.Count; cursor++)
                foreach (TerrainCellData candidate in exits)
                {
                    if (assigned.Contains(candidate))
                        continue;

                    bool sameDoor = explicitId ? candidate.LogicalDoorId == first.LogicalDoorId :
                        string.IsNullOrWhiteSpace(candidate.LogicalDoorId) && candidate.Type == first.Type &&
                        SameTargets(first.TargetScenes, candidate.TargetScenes) &&
                        IsAdjacent(door.Cells[cursor], candidate.Position);
                    if (!sameDoor)
                        continue;

                    assigned.Add(candidate);
                    door.Cells.Add(candidate.Position);
                    door.IsValid &= candidate.Type == first.Type && SameTargets(first.TargetScenes, candidate.TargetScenes);
                    if (candidate.RewardIconAnchor != null)
                    {
                        door.IsValid &= door.RewardIconAnchor == null || door.RewardIconAnchor == candidate.RewardIconAnchor;
                        door.RewardIconAnchor = candidate.RewardIconAnchor;
                    }
                }
            doors.Add(door);
        }
        return doors;
    }

    public static bool TryFindTouched(IReadOnlyList<LogicalDoor> doors,
        IEnumerable<Vector2Int> footprint, out LogicalDoor touched)
    {
        touched = null;
        foreach (Vector2Int cell in footprint)
            foreach (LogicalDoor door in doors)
                if (door.Cells.Contains(cell))
                {
                    // Do not silently choose between separate exits touched by a large character.
                    if (touched != null && touched != door)
                    {
                        touched = null;
                        return false;
                    }
                    touched = door;
                }
        return touched != null;
    }

    private static bool IsAdjacent(Vector2Int a, Vector2Int b) =>
        Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) == 1;

    private static bool SameTargets(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a == null || b == null || a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }
}
