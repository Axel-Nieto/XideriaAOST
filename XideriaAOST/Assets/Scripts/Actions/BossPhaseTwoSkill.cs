using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BossPhaseTwoSkill : SkillBase
{
    private readonly BossAI boss;
    private readonly bool ranged;
    public BossPhaseTwoSkill(BossAI boss, bool ranged) : base(ranged ? "Impacto de Roca" : "Golpe del Coloso",
        ranged ? "Golpea a distancia, expulsa a una casilla adyacente aleatoria y deja una roca durante dos turnos del jefe."
            : "Golpea cuerpo a cuerpo y empuja dos casillas.",
        ranged ? 5 : 3, 0, ranged ? 2 : 1, ranged ? int.MaxValue : 1, SkillTargetType.TargetCell, DamageType.Physical)
    { this.boss = boss; this.ranged = ranged; }

    public override bool CanUse(Character user, Vector2Int cell, GridSystem grid)
    {
        Character target = grid != null ? grid.GetCharacterAt(cell) : null;
        return user != null && CanUseFromPosition(user, user.GridPosition, target, grid, user.Stats?.CurrentEnergy ?? 0);
    }

    public override bool CanUseFromPosition(Character user, Vector2Int simulatedPosition, Character target, GridSystem grid, int availableEnergy)
        => boss != null && boss.IsPhaseTwo && user == boss.Owner && user != null && grid != null &&
            user.Stats != null && user.Stats.IsAlive && !user.IsDying && availableEnergy >= EnergyCost &&
            target != null && target != user && target.IsPlayer != user.IsPlayer && target.Stats != null &&
            target.Stats.IsAlive && !target.IsDying && BossAI.AreAdjacentAt(user, simulatedPosition, target) != ranged;

    public override IEnumerator Execute(Character user, Vector2Int cell, GridSystem grid)
    {
        if (!CanUse(user, cell, grid)) yield break;
        Character target = grid.GetCharacterAt(cell);
        user.Stats.ConsumeEnergy(EnergyCost);
        yield return boss.ExecutePhaseTwoAttack(target, ranged);
    }

    public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid)
    {
        Character target = grid != null ? grid.GetCharacterAt(targetCell) : null;
        if (target != null) foreach (Vector2Int cell in target.GetOccupiedCells()) yield return cell;
    }
    public override string GetRangeDescription(Character user) => ranged ? "Todo el campo" : base.GetRangeDescription(user);
    public override string GetTooltipDetails(Character user) => $"Da\u00f1o: {(user?.Stats?.Attack ?? 0) + (boss != null ? boss.AttackBonus(ranged) : 0)}. " +
        (ranged ? "Expulsi\u00f3n aleatoria: 1 casilla. Roca: 2 turnos del jefe." : "Empuje: 2 casillas.");
}
