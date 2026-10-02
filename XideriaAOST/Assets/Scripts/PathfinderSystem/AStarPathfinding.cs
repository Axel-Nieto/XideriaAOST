using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinding
{
    private GridSystem _grid;

    public AStarPathfinding(GridSystem grid)
    {
        _grid = grid;
    }

    class PathNode
    {
        public Vector2Int Position;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public PathNode Parent;

        public PathNode(Vector2Int pos)
        {
            Position = pos;
        }
    }

    public List<Vector2Int> FindPath(
    Vector2Int start,
    Vector2Int end,
    Character character = null,
    bool avoidUnsafeCells = false)
    {
        var openSet = new List<PathNode>();
        var closedSet = new HashSet<Vector2Int>();
        var nodes = new Dictionary<Vector2Int, PathNode>();

        var startNode = new PathNode(start)
        {
            GCost = 0,
            HCost = GetDistance(start, end)
        };

        openSet.Add(startNode);
        nodes[start] = startNode;

        while (openSet.Count > 0)
        {
            var current = GetLowestFCost(openSet);

            if (current.Position == end)
                return ReconstructPath(current);

            openSet.Remove(current);
            closedSet.Add(current.Position);

            foreach (var neighborPos in GetNeighbors(current.Position))
            {
                if (closedSet.Contains(neighborPos))
                    continue;

                if (character != null)
                {
                    if (!_grid.CanCharacterOccupy(
                        character,
                        neighborPos,
                        ignoreSelf: true))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!_grid.IsWalkable(neighborPos))
                        continue;

                    if (_grid.IsBlockedForMovement(neighborPos) &&
                        neighborPos != end)
                    {
                        continue;
                    }
                }

                if (avoidUnsafeCells)
                {
                    if (!IsFootprintSafe(character, neighborPos))
                        continue;
                }

                int newCost = current.GCost + GetCellCost(neighborPos);

                if (!nodes.TryGetValue(neighborPos, out var neighbor))
                {
                    neighbor = new PathNode(neighborPos);
                    nodes[neighborPos] = neighbor;
                }

                if (!openSet.Contains(neighbor) || newCost < neighbor.GCost)
                {
                    neighbor.GCost = newCost;
                    neighbor.HCost = GetDistance(neighborPos, end);
                    neighbor.Parent = current;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null;
    }

    private bool IsFootprintSafe(
    Character character,
    Vector2Int origin)
    {
        if (character == null)
            return _grid.IsSafeForAI(origin);

        foreach (Vector2Int cell in
                 character.GetOccupiedCells(origin))
        {
            if (!_grid.IsSafeForAI(cell))
                return false;
        }

        return true;
    }

    private List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        return new List<Vector2Int>
        {
            pos + Vector2Int.up,
            pos + Vector2Int.down,
            pos + Vector2Int.left,
            pos + Vector2Int.right
        };
    }

    private int GetCellCost(Vector2Int pos)
    {
        return _grid.GetCell(pos).MoveCost;
    }

    private int GetDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private PathNode GetLowestFCost(List<PathNode> list)
    {
        PathNode best = list[0];

        foreach (var node in list)
        {
            if (node.FCost < best.FCost)
                best = node;
        }

        return best;
    }

    private List<Vector2Int> ReconstructPath(PathNode node)
    {
        var path = new List<Vector2Int>();

        while (node != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }

        path.Reverse();
        return path;
    }
}