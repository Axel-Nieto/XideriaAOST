using System.Collections.Generic;
using UnityEngine;

public class MovementRangeCalculator
{
    private GridSystem _grid;

    public MovementRangeCalculator(GridSystem grid)
    {
        _grid = grid;
    }

    public HashSet<Vector2Int> GetReachableCells(
    Vector2Int start,
    int maxCost,
    Character character = null)
    {
        var visited = new HashSet<Vector2Int>();
        var costSoFar = new Dictionary<Vector2Int, int>();

        Queue<Vector2Int> queue = new();

        queue.Enqueue(start);

        costSoFar[start] = 0;
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int dir in GetDirections())
            {
                Vector2Int next = current + dir;

                if (character != null)
                {
                    // Para personajes con footprint variable
                    // se valida el área completa que ocuparían.
                    if (!_grid.CanCharacterOccupy(
                        character,
                        next,
                        ignoreSelf: true))
                    {
                        continue;
                    }
                }
                else
                {
                    // Compatibilidad con llamadas antiguas.
                    if (!_grid.IsValidCell(next))
                        continue;

                    GridCell nextCell = _grid.GetCell(next);

                    if (nextCell == null)
                        continue;

                    if (nextCell.Type == CellType.Void)
                        continue;

                    if (_grid.IsBlockedForMovement(next))
                        continue;
                }

                int newCost =
                    costSoFar[current] + 1;

                if (newCost > maxCost)
                    continue;

                if (costSoFar.TryGetValue(
                        next,
                        out int previousCost) &&
                    previousCost <= newCost)
                {
                    continue;
                }

                costSoFar[next] = newCost;

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        visited.Remove(start);

        return visited;
    }

    private List<Vector2Int> GetDirections()
    {
        return new List<Vector2Int>
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
        };
    }
}