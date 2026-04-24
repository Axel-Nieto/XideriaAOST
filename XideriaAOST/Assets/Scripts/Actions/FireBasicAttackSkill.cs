using System.Collections;
using UnityEngine;

public class FireBasicAttackSkill : SkillBase
{
    public FireBasicAttackSkill()
        : base(
            name: "Ataque Ígneo",
            description: "Lanza un ataque de fuego a distancia.",
            energyCost: 3,
            cooldown: 0,
            minRange: 3,
            maxRange: 5,
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

        target.TakeDamage(damage, DamageType.Fire);

        Debug.Log($"{user.name} usa {Name} contra {target.name} por {damage} de fuego");

        // Acá después aplicamos burn

        yield return null;
    }
}