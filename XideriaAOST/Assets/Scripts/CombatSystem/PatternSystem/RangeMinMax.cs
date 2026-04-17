using System.Collections.Generic;
using UnityEngine;

public class RangeMinMax : IRangePattern
{
    private int _min;
    private int _max;

    public RangeMinMax(int min, int max)
    {
        _min = min;
        _max = max;
    }

    public List<Vector2Int> GetCells(Vector2Int origin)
    {
        var result = new List<Vector2Int>();

        for (int x = -_max; x <= _max; x++)
        {
            for (int y = -_max; y <= _max; y++)
            {
                int dist = Mathf.Abs(x) + Mathf.Abs(y);

                if (dist >= _min && dist <= _max)
                {
                    result.Add(origin + new Vector2Int(x, y));
                }
            }
        }

        return result;
    }
}