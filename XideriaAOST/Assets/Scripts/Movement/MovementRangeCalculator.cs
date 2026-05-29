using System.Collections.Generic;
using UnityEngine;

public class MovementRangeCalculator
{
    private GridSystem _grid;

    public MovementRangeCalculator(GridSystem grid)
    {
        _grid = grid;
    }

    public HashSet<Vector2Int> GetReachableCells(Vector2Int start, int maxCost)
    {
        var visited = new HashSet<Vector2Int>();
        var costSoFar = new Dictionary<Vector2Int, int>();

        Queue<Vector2Int> queue = new();
        queue.Enqueue(start);

        costSoFar[start] = 0;
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var dir in GetDirections())
            {
                var next = current + dir;

                if (!_grid.IsValidCell(next))
                    continue;

                if (_grid.IsBlockedForMovement(next))
                    continue;

                int newCost = costSoFar[current] + 1;

                if (newCost > maxCost)
                    continue;

                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

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