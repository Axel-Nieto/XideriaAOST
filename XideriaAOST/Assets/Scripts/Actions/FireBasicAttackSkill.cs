using System.Collections;
using UnityEngine;

public class FireBasicAttackSkill : SkillBase
{
    public FireBasicAttackSkill()
        : base(
            name: "Ataque Ígneo",
            description: "Golpea e inflige quemadura.",
            energyCost: 3,
            cooldown: 0,
            minRange: 3,
            maxRange: 5)
    {
    }

    public override bool CanUse(Character user, Character target)
    {
        if (user == null || target == null) return false;
        if (!user.Stats.HasEnergy(EnergyCost)) return false;
        if (!IsInRange(user.GridPosition, target.GridPosition)) return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Character target)
    {
        user.Stats.ConsumeEnergy(EnergyCost);

        int damage = Mathf.Max(1, user.Stats.Attack - target.Stats.Defense);

        target.TakeDamage(damage);

        //Aca aplicamos efecto de burn o lo que venga

        yield return null;
    }
}