using UnityEngine;

public class LarvaArmorPassive : PassiveEffect
{
    private const float DamageReduction = 0.20f;

    public override string Name => "Coraza de la Larva";

    public override string Description =>
        "Reduce un 20% el daño Físico, de Fuego y de Agua recibido.";

    public override int ModifyIncomingDamage(
        Character owner,
        int damage,
        DamageType damageType)
    {
        if (damageType != DamageType.Physical &&
            damageType != DamageType.Fire &&
            damageType != DamageType.Water)
        {
            return damage;
        }

        int reducedDamage =
            Mathf.CeilToInt(
                damage * (1f - DamageReduction)
            );

        return Mathf.Max(0, reducedDamage);
    }
}
