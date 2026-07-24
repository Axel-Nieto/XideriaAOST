using UnityEngine;

public class SlowEffect : StatusEffect
{
    public override string EffectName => "Slow";

    public override Sprite Icon =>
        StatusIconDatabase.Instance.slowIcon;

    public SlowEffect(int duration)
        : base(duration)
    {
    }
}
