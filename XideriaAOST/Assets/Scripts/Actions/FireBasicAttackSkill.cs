using System.Collections;
using UnityEngine;

public class FireBasicAttackSkill : SkillBase
{
    public FireBasicAttackSkill()
        : base(
            name: "Ataque Ígneo",
            description: "Lanza un ataque de fuego a distancia. Aplica quemadura",
            energyCost: 3,
            cooldown: 0,
            minRange: 3,
            maxRange: 5,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Fire,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.basicAttack
            : null)
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

        if (user.IsSkillOnCooldown(this))
            return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.FaceCell(targetCell);

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Vector3 startPos = grid.GridToWorld(user.GridPosition) + Vector3.up * 1f;
        Vector3 endPos = grid.GridToWorld(targetCell) + Vector3.up * 1f;

        yield return SkillVFXSpawner.SpawnProjectile(
            SkillVFXLibrary.Instance.waterBasic,
            SkillVFXLibrary.Instance.waterBasic,
            startPos,
            endPos,
            speed: 10f
        );

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        int damage = Mathf.Max(1, user.Stats.Attack - target.Stats.Defense);

        target.TakeDamage(damage, DamageType.Fire, user);
        target.ApplyStatusEffect(new BurnEffect(3, 2));

        Debug.Log($"{user.name} usa {Name} contra {target.name} por {damage} de fuego");

        yield return null;
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? 0
            : user.Stats.Attack;

        return
        $"Daño: {estimatedDamage}";
    }
}