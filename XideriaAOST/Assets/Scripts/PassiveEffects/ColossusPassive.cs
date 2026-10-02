public class ColossusPassive : PassiveEffect
{
    public override string Name => "Coloso";

    public override string Description =>
        "No puede ser empujado. Los intentos de empuje desplazan al atacante en sentido contrario.";

    public override bool BlocksPush(Character owner)
    {
        return true;
    }
}
