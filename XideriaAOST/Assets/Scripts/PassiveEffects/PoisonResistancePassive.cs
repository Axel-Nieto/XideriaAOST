using UnityEngine;

public class PoisonResistancePassive : PassiveEffect
{
    public override string Name => "Resistencia al Veneno";

    public override Sprite Icon =>
        StatusIconDatabase.Instance.poisonResistanceIcon;

    public override string Description =>
        "Reduce a la mitad el daño de veneno recibido.";

    public override int ModifyIncomingDamage(
        Character owner,
        int damage,
        DamageType damageType)
    {
        if (damageType != DamageType.Poison)
            return damage;

        return Mathf.Max(0, damage / 2);
    }
}
