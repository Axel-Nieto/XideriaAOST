using System.Collections.Generic;
using UnityEngine;

public class SingleTargetStrategy : ITargetingStrategy
{
    private GridSystem _grid;

    public SingleTargetStrategy(GridSystem grid)
    {
        _grid = grid;
    }

    public List<Character> GetTargets(Character actor, Vector2Int selectedCell)
    {
        var result = new List<Character>();

        var target = _grid.GetCharacterAt(selectedCell);
        if (target != null)
            result.Add(target);

        return result;
    }
}