using UnityEngine;

public abstract class StatusEffect
{
    public int RemainingTurns { get; protected set; }

    public bool IsFinished => RemainingTurns <= 0;

    public abstract string EffectName { get; }
    public virtual Sprite Icon => null;
    public string Description { get; } = string.Empty;

    protected StatusEffect(int duration)
    {
        RemainingTurns = duration;
    }

    public virtual void OnApply(Character target) { }

    public virtual void OnTurnStart(Character target)
    {
        RemainingTurns--;
    }

    public virtual void OnExpire(Character target) { }
}
