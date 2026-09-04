using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveAction : IAction
{
    private readonly List<Vector2Int> path;
    private readonly bool ignoreEnergyCost;

    public MoveAction(List<Vector2Int> path, bool ignoreEnergyCost = false)
    {
        this.path = path;
        this.ignoreEnergyCost = ignoreEnergyCost;
    }

    public bool CanExecute(Character actor)
    {
        if (actor == null || actor.Stats == null)
            return false;

        if (path == null || path.Count == 0)
            return false;

        if (ignoreEnergyCost)
            return true;

        int totalCost =
            path.Count * actor.GetMovementCostPerCell();

        return actor.Stats.HasEnergy(totalCost);
    }

    public IEnumerator Execute(Character actor)
    {
        if (!ignoreEnergyCost)
        {
            int totalCost =
                path.Count * actor.GetMovementCostPerCell();

            actor.Stats.ConsumeEnergy(totalCost);
        }

        yield return actor.Movement.MoveAlongRoutine(path);
    }
}