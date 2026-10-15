using UnityEngine;

public abstract class PassiveEffect
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public virtual Sprite Icon => null;

    public virtual int ModifyIncomingDamage(
        Character owner,
        int damage,
        DamageType damageType)
    {
        return damage;
    }

    public virtual bool BlocksPush(Character owner)
    {
        return false;
    }

    public virtual void OnTurnStart(Character owner)
    {
    }

    public virtual void OnOtherCharacterDeathCompleted(Character owner, Character deceased)
    {
    }
}
