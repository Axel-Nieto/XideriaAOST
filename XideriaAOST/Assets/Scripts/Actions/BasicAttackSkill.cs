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
            maxRange: 1,
            targetType: SkillTargetType.TargetCell)
    {
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null)
            return false;

        if (!user.Stats.HasEnergy(EnergyCost))
            return false;

        if (!grid.IsValidCell(targetCell))
            return false;

        if (!IsInRange(user.GridPosition, targetCell))
            return false;

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            return false;

        if (target.IsPlayer == user.IsPlayer)
            return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.Stats.ConsumeEnergy(EnergyCost);

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        int damage = Mathf.Max(1, user.Stats.Attack - target.Stats.Defense);

        Debug.Log($"{user.name} usa {Name} sobre {target.name} por {damage}");

        target.TakeDamage(damage, DamageType.Physical);

        yield return null;
    }
}