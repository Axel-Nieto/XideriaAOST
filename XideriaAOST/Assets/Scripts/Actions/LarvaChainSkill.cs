using System.Collections;
using UnityEngine;

public sealed class LarvaChainSkill : LarvaSpecialSkill
{
    public LarvaChainSkill(int energyCost = 3, int cooldown = 3)
        : base("Cadenazo", "Inflige daño físico y empuja al objetivo una casilla.",
            1, 2, DamageType.Physical, energyCost: energyCost, cooldown: cooldown) { }

    protected override IEnumerator ApplyEffect(Character user, Character target, GridSystem grid)
    {
        DealDamage(user, target);
        if (target.Stats.IsAlive && !target.IsDying)
            yield return PushSystem.Push(user, target, grid, 1);
    }
}
