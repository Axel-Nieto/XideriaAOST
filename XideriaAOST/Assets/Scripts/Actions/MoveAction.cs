using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveAction : IAction
{
    private List<Vector2Int> _path;

    public MoveAction(List<Vector2Int> path)
    {
        _path = path;
    }

    public bool CanExecute(Character actor)
    {
        return actor.Stats.HasEnergy(_path.Count);
    }

    public IEnumerator Execute(Character actor)
    {
        actor.Stats.ConsumeEnergy(_path.Count);

        yield return actor.Movement.MoveAlongRoutine(_path);
    }
}