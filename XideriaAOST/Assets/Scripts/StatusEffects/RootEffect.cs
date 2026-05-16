using UnityEngine;

public class RootEffect : StatusEffect
{
    public override string EffectName => "Root";

    public RootEffect(int duration)
        : base(duration)
    {
    }
}