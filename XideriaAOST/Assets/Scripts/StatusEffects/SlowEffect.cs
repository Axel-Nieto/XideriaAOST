public class SlowEffect : StatusEffect
{
    public override string EffectName => "Slow";

    public SlowEffect(int duration)
        : base(duration)
    {
    }
}
