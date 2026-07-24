using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireballSkill : SkillBase
{
    private readonly int damage;
    private readonly int aoeRadius;
    public override int AreaRadius => aoeRadius;

    public FireballSkill()
        : base(
            name: "Bola de Fuego",
            description: "Lanza una bola de fuego que explota alrededor del objetivo.",
            energyCost: 5,
            cooldown: 2,
            minRange: 2,
            maxRange: 4,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Fire,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.fireball
            : null)
    {
        damage = 6;
        aoeRadius =3;
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

        if (user.IsSkillOnCooldown(this))
            return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.FaceCell(targetCell);

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        CharacterAnimationController anim =
        user.GetComponentInChildren<CharacterAnimationController>();

        if (anim != null)
            yield return anim.PlayAttackUntilHitFrame();

        Vector3 startPos = grid.GridToWorld(user.GridPosition) + Vector3.up * 1f;
        Vector3 endPos = grid.GridToWorld(targetCell) + Vector3.up * 1f;

        yield return SkillVFXSpawner.SpawnProjectile(
            SkillVFXLibrary.Instance.fireballProjectile,
            SkillVFXLibrary.Instance.fireballExplosion,
            startPos,
            endPos,
            speed: 10f
        );

        foreach (var pos in GetAreaCells(targetCell, aoeRadius))
        {
            if (!grid.IsValidCell(pos))
                continue;

            Character target = grid.GetCharacterAt(pos);

            if (target == null || target.IsPlayer == user.IsPlayer)
                continue;

            int finalDamage = Mathf.Max(1, damage + user.Stats.Attack - target.Stats.Defense);

            target.TakeDamage(finalDamage, DamageType.Fire, user);
        }

        if (anim != null)
            yield return anim.WaitAttackFinished();
    }

    private IEnumerable<Vector2Int> GetAreaCells(Vector2Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int distance = Mathf.Abs(x) + Mathf.Abs(y);

                if (distance <= radius)
                    yield return center + new Vector2Int(x, y);
            }
        }
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell)
    {
        foreach (var cell in GetAreaCells(targetCell, aoeRadius))
            yield return cell;
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? damage
            : damage + user.Stats.Attack;

        return
            $"Daño: {estimatedDamage}"; ;
    }
}