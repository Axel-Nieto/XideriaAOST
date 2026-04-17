using UnityEngine;

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