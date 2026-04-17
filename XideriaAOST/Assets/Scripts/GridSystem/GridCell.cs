using UnityEngine;

public class GridCell
{
    public Vector2Int Position;
    public bool Walkable;
    public bool IsOccupied;
    public CellType Type;

    public int MoveCost =>
        Type switch
        {
            CellType.Normal => 1,
            CellType.Water => 2,
            CellType.Lava => 3,
            _ => int.MaxValue
        };

    public GridCell(Vector2Int position, bool walkable, CellType type)
    {
        Position = position;
        Walkable = walkable;
        Type = type;
        IsOccupied = false;
    }
}