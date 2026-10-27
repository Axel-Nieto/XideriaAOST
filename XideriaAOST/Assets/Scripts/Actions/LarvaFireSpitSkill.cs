using System.Collections;
using UnityEngine;

public sealed class LarvaFireSpitSkill : LarvaSpecialSkill
{
    private readonly int burnDuration;
    private readonly int burnDamage;

    public LarvaFireSpitSkill(int damage = 3, int burnDuration = 3, int burnDamage = 2,
        int energyCost = 3, int cooldown = 3)
        : base("Escupitajo de Fuego", "Lanza una bola de fuego contra el objetivo y aplica burn.",
            3, 4, DamageType.Fire, damage, energyCost, cooldown)
    {
        this.burnDuration = burnDuration;
        this.burnDamage = burnDamage;
    }

    protected override IEnumerator ApplyEffect(Character user, Character target, GridSystem grid)
    {
        SkillVFXLibrary effects = SkillVFXLibrary.Instance;
        if (effects != null)
            yield return SkillVFXSpawner.SpawnProjectile(effects.fireballProjectile, null,
                user.transform.position + Vector3.up, target.transform.position + Vector3.up, 10f);

        if (target == null || !target.Stats.IsAlive || target.IsDying)
            yield break;

        DealDamage(user, target);
        if (target.Stats.IsAlive && !target.IsDying)
            target.ApplyStatusEffect(new BurnEffect(burnDuration, burnDamage));
    }

    public override string GetTooltipDetails(Character user) =>
        base.GetTooltipDetails(user) + $"\nBurn: {burnDamage} por turno durante {burnDuration} turnos";
}
