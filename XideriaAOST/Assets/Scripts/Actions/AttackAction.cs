using System.Collections;
using UnityEngine;

public class AttackAction : IAction
{
    private readonly Character _target;
    private readonly int _energyCost;
    private readonly int _range;
    private readonly DamageType _damageType;

    public AttackAction(Character target, int energyCost, int range, DamageType damageType = DamageType.Physical)
    {
        _target = target;
        _energyCost = energyCost;
        _range = range;
        _damageType = damageType;
    }

    public bool CanExecute(Character actor)
    {
        if (actor == null || actor.Stats == null) return false;
        if (_target == null || _target.Stats == null) return false;
        if (!actor.Stats.HasEnergy(_energyCost)) return false;
        if (_target == actor) return false;
        if (_target.IsPlayer == actor.IsPlayer) return false;

        int distance = Mathf.Abs(actor.GridPosition.x - _target.GridPosition.x) +
                       Mathf.Abs(actor.GridPosition.y - _target.GridPosition.y);

        return distance <= _range;
    }

    public IEnumerator Execute(Character actor)
    {
        actor.Stats.ConsumeEnergy(_energyCost);

        int damage = Mathf.Max(1, actor.Stats.Attack - _target.Stats.Defense);

        _target.TakeDamage(damage, _damageType);

        yield return null;
    }
}