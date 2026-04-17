using System.Collections;
using UnityEngine;

public class BasicAttackSkill : SkillBase
{
    public BasicAttackSkill()
        : base(
            name: "Ataque Básico",
            description: "Golpea a un enemigo cercano.",
            energyCost: 3,
            cooldown: 0,
            minRange: 1,
            maxRange: 1)
    {
    }

    public override bool CanUse(Character user, Character target)
    {
        if (user == null || target == null)
            return false;

        if (!user.Stats.HasEnergy(EnergyCost))
            return false;

        if (target.IsPlayer)
            return false;

        if (!IsInRange(user.GridPosition, target.GridPosition))
            return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Character target)
    {
        user.Stats.ConsumeEnergy(EnergyCost);

        int damage = Mathf.Max(1, user.Stats.Attack - target.Stats.Defense);

        Debug.Log($"{user.name} usa {Name} sobre {target.name} por {damage}");

        target.TakeDamage(damage);

        yield return null;
    }
}