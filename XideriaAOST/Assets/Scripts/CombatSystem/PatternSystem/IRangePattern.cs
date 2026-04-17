using System.Collections.Generic;
using UnityEngine;

public interface IRangePattern
{
    List<Vector2Int> GetCells(Vector2Int origin);
}
