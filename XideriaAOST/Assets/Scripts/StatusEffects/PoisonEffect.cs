using UnityEngine;

public class PoisonEffect : StatusEffect
{
    private readonly int damagePerTurn;
    public override string EffectName => "Poison";

    public override Sprite Icon =>
        StatusIconDatabase.Instance.poisonIcon;

    public PoisonEffect(int duration, int damagePerTurn)
        : base(duration)
    {
        this.damagePerTurn = damagePerTurn;
    }

    public override void OnTurnStart(Character target)
    {
        base.OnTurnStart(target);

        target.TakeDamage(damagePerTurn, DamageType.Poison);
    }
}
