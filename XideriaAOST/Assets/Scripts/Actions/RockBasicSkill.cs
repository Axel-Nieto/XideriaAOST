using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RockBasicSkill : RockSkillBase
{
    private const int damageBonus = -1;
    public override int AreaRadius => 2;
    public RockBasicSkill() : base("Giro de Tierra", "Gira sin moverte, golpea a los enemigos a hasta dos casillas y los empuja una casilla. Ganas 10% de reducción de daño por enemigo golpeado durante dos turnos propios.",
        2, 0, icon: SkillIconLibrary.Instance != null ? SkillIconLibrary.Instance.basicAttack : null) { }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid) =>
        Ready(user, grid, user?.Stats?.CurrentEnergy ?? 0);

    public override bool CanUseFromPosition(Character user, Vector2Int origin, Character target, GridSystem grid, int energy) =>
        Ready(user, grid, energy) && Enemies(user, grid, origin, AreaRadius).Contains(target);

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid) =>
        ExecuteSpin(user, targetCell, grid, false);

    // Only the excavation arrival proc uses this path. It keeps the normal Q's
    // animation, damage, armor and push, without spending energy or starting a cooldown.
    internal IEnumerator ExecuteFree(Character user, GridSystem grid) =>
        ExecuteSpin(user, user.GridPosition, grid, true);

    private IEnumerator ExecuteSpin(Character user, Vector2Int targetCell, GridSystem grid, bool free)
    {
        if (free ? !Ready(user, grid, EnergyCost) : !CanUse(user, targetCell, grid)) yield break;
        if (!free) Spend(user);
        var animation = user.GetComponentInChildren<CharacterAnimationController>();
        var vfx = user.GetComponent<RockSkillVFX>();
        try
        {
            if (animation != null)
            {
                yield return animation.PlayTwirlUntilHitFrame(rate => vfx?.PlaySlash(rate));
                if (!animation.TwirlHitFrameReached) yield break;
            }
            else vfx?.PlaySlash();

            List<Character> targets = Enemies(user, grid, user.GridPosition, AreaRadius);
            foreach (Character target in targets) Damage(user, target, damageBonus);
            RockGuardPassive.For(user).AddArmor(targets.Count);
            foreach (Character target in targets)
                if (target != null && target.Stats.IsAlive && !target.IsDying)
                    yield return PushSystem.Push(user, target, grid, 1);
            if (animation != null) yield return animation.WaitTwirlFinished();
        }
        finally { if (animation != null) animation.FinishTwirl(); }
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell) => Area(origin, 1, 1, AreaRadius);
    public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid) =>
        Area(origin, user.GridWidth, user.GridHeight, AreaRadius);
    public override string GetTooltipDetails(Character user) => $"Daño físico: {Mathf.Max(1, (user?.Stats?.Attack ?? 0) + damageBonus)}. Empuje: 1 casilla.\nDefensa: +10% por enemigo, 2 turnos.";
}
