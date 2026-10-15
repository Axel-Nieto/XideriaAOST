using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireExplosionSkill : SkillBase
{
    private readonly int damage;

    private readonly int aoeRadius;
    public override int AreaRadius => aoeRadius;

    private readonly float explosionHitDelay = 1.2f;

    public FireExplosionSkill()
        : base(
            name: "Explosión de Fuego",
            description: "Libera una explosión de fuego alrededor del personaje. Aplica quemadura",
            energyCost: 6,
            cooldown: 3,
            minRange: 0,
            maxRange: 0,
            targetType: SkillTargetType.Self,
            damageType: DamageType.Fire,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.fireExplosion
            : null)
    {
        damage = 18;
        aoeRadius = 8;

        Debug.Log("SkillIconLibrary: " + SkillIconLibrary.Instance);
        Debug.Log("Fire Explosion Icon: " + SkillIconLibrary.Instance?.fireExplosion);
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null)
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

        return user.Stats.HasEnergy(EnergyCost);
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        CharacterAnimationController anim =
            user.GetComponentInChildren<CharacterAnimationController>();

        if (anim != null)
            yield return anim.PlayCastUntilHitFrame();

        SkillVFXSpawner.SpawnAtCell(
            SkillVFXLibrary.Instance.fireExplosion,
            grid,
            user.GridPosition,
            Vector3.up * 2f
        );

        Vector2Int center = user.GridPosition;

        yield return new WaitForSeconds(explosionHitDelay);

        HashSet<Character> targets = new();

        foreach (var pos in GetAreaCells(center, aoeRadius))
        {
            if (!grid.IsValidCell(pos))
                continue;

            Character target = grid.GetCharacterAt(pos);

            if (target == null)
                continue;

            if (target == user)
                continue;

            if (target.IsPlayer == user.IsPlayer)
                continue;

            targets.Add(target);
        }

        foreach (Character target in targets)
        {
            int finalDamage = Mathf.Max(
                1,
                damage - target.Stats.Defense
            );

            target.TakeDamage(
                finalDamage,
                DamageType.Fire,
                user
            );

            if (target.Stats.IsAlive && !target.IsDying)
            {
                target.ApplyStatusEffect(
                    new BurnEffect(2, 4)
                );
            }
        }

        if (anim != null)
            yield return anim.WaitCastFinished();
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
        foreach (var cell in GetAreaCells(origin, aoeRadius))
            yield return cell;
    }

    public override string GetTooltipDetails(Character user)
    {
        return $"Daño: {damage}";
    }
}