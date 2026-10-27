using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RockBasicSkill : RockSkillBase
{
    public override int AreaRadius => 2;
    public RockBasicSkill() : base("Giro de Tierra", "Gira sin moverte, golpea a los enemigos a hasta dos casillas y los empuja una casilla. Ganas 10% de reducción de daño por enemigo golpeado durante dos turnos propios.",
        2, 0, icon: SkillIconLibrary.Instance != null ? SkillIconLibrary.Instance.basicAttack : null) { }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid) =>
        Ready(user, grid, user?.Stats?.CurrentEnergy ?? 0);

    public override bool CanUseFromPosition(Character user, Vector2Int origin, Character target, GridSystem grid, int energy) =>
        Ready(user, grid, energy) && Enemies(user, grid, origin, AreaRadius).Contains(target);

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (!CanUse(user, targetCell, grid)) yield break;
        Spend(user);
        var animation = user.GetComponentInChildren<CharacterAnimationController>();
        if (animation != null) yield return animation.PlayAttackUntilHitFrame();
        List<Character> targets = Enemies(user, grid, user.GridPosition, AreaRadius);
        foreach (Character target in targets) Damage(user, target);
        RockGuardPassive.For(user).AddArmor(targets.Count);
        foreach (Character target in targets)
            if (target != null && target.Stats.IsAlive && !target.IsDying)
                yield return PushSystem.Push(user, target, grid, 1);
        Transform visual = animation != null ? animation.transform : null;
        if (visual != null && visual != user.transform)
        {
            Quaternion original = visual.localRotation;
            try
            {
                for (float elapsed = 0f; elapsed < 0.3f; elapsed += Time.deltaTime)
                {
                    visual.localRotation = original * Quaternion.Euler(0f, elapsed / 0.3f * 360f, 0f);
                    yield return null;
                }
            }
            finally { if (visual != null) visual.localRotation = original; }
        }
        if (animation != null) yield return animation.WaitAttackFinished();
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell) => Area(origin, 1, 1, AreaRadius);
    public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid) =>
        Area(origin, user.GridWidth, user.GridHeight, AreaRadius);
    public override string GetTooltipDetails(Character user) => $"Daño físico: {user?.Stats?.Attack ?? 0}. Empuje: 1 casilla.\nDefensa: +10% por enemigo, 2 turnos.";
}
