public abstract class PassiveEffect
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public virtual int ModifyIncomingDamage(
        Character owner,
        int damage,
        DamageType damageType)
    {
        return damage;
    }

    public virtual void OnTurnStart(Character owner)
    {
    }
}