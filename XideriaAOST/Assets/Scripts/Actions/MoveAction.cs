using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveAction : IAction
{
    private readonly List<Vector2Int> path;

    public MoveAction(List<Vector2Int> path)
    {
        this.path = path;
    }

    public bool CanExecute(Character actor)
    {
        if (actor == null || actor.Stats == null)
            return false;

        if (path == null || path.Count == 0)
            return false;

        int totalCost = path.Count * actor.GetMovementCostPerCell();

        return actor.Stats.HasEnergy(totalCost);
    }

    public IEnumerator Execute(Character actor)
    {
        int totalCost = path.Count * actor.GetMovementCostPerCell();

        actor.Stats.ConsumeEnergy(totalCost);

        yield return actor.Movement.MoveAlongRoutine(path);
    }
}