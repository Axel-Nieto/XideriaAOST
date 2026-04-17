using System.Collections.Generic;
using UnityEngine;

public class RangeMelee : IRangePattern
{
    public List<Vector2Int> GetCells(Vector2Int origin)
    {
        return new List<Vector2Int>
        {
            origin + Vector2Int.up,
            origin + Vector2Int.down,
            origin + Vector2Int.left,
            origin + Vector2Int.right
        };
    }
}
