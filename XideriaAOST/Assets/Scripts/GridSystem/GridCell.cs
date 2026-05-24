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
            CellType.Ground => 1,
            CellType.Water => 2,
            CellType.Lava => 3,
            CellType.Void => 1,
            CellType.Blocked => int.MaxValue,
            _ => int.MaxValue
        };

    public GridCell(Vector2Int position, CellType type)
    {
        Position = position;
        Type = type;
        IsOccupied = false;
        Walkable = type != CellType.Blocked;
    }

    public void SetType(CellType type)
    {
        Type = type;

        Walkable = type != CellType.Blocked;
    }
}