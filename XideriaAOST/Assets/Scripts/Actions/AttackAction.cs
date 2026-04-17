using System.Collections;
using UnityEngine;

public class AttackAction : IAction
{
    private Character _target;
    private int _cost;

    public AttackAction(Character target, int cost = 3)
    {
        _target = target;
        _cost = cost;
    }

    public bool CanExecute(Character actor)
    {
        if (_target == null) return false;

        return actor.Stats.HasEnergy(_cost);
    }

    public IEnumerator Execute(Character actor)
    {
        actor.Stats.ConsumeEnergy(_cost);

        int damage = Mathf.Max(
            1,
            actor.Stats.Attack - _target.Stats.Defense
        );

        _target.TakeDamage(damage);

        yield return null;
    }
}
