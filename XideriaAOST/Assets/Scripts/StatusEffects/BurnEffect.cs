using UnityEngine;

public class BurnEffect : StatusEffect
{
    private readonly int damagePerTurn;

    public override string EffectName => "Burn";

    public override Sprite Icon =>
        StatusIconDatabase.Instance.burnIcon;

    public BurnEffect(int duration, int damagePerTurn)
        : base(duration)
    {
        this.damagePerTurn = damagePerTurn;
    }

    public override void OnTurnStart(Character target)
    {
        base.OnTurnStart(target);

        target.TakeDamage(damagePerTurn, DamageType.Fire);

        Debug.Log($"{target.name} sufre {damagePerTurn} de burn.");
    }
}
