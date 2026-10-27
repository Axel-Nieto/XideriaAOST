public sealed class UnstoppablePassive : PassiveEffect
{
    public int CollisionDamage { get; }
    public UnstoppablePassive(int collisionDamage = 2) => CollisionDamage = UnityEngine.Mathf.Max(0, collisionDamage);
    public override string Name => "Imparable";
    public override string Description => $"No puedes ser empujado. Tus empujes causan {CollisionDamage} de daño físico extra al chocar contra un obstáculo, otro personaje o el límite del tablero.";
    public override bool BlocksPush(Character owner) => true;
}
