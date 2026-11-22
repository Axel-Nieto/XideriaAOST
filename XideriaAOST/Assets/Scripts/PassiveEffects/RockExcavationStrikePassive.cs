using System.Collections;
using UnityEngine;

public sealed class RockExcavationStrikePassive : PassiveEffect
{
    public override string Name => "Emboscada de Tierra";
    public override string Description => "Al salir de Excavar, si hay al menos un enemigo adyacente por un lado, realizas un Giro de Tierra gratuito. Solo una vez por uso de Excavar.";
    public override Sprite Icon => StatusIconDatabase.Instance != null
        ? StatusIconDatabase.Instance.rockPassiveIcon : null;

    public static bool IsEnabled(Character user) =>
        RockGuardPassive.HasUpgrade(user, UpgradeDefinition.RockExcavationStrikeId);

    internal IEnumerator OnExcavationArrived(Character user, GridSystem grid)
    {
        if (!IsEnabled(user) || grid == null || user.Stats == null || !user.Stats.IsAlive || user.IsDying)
            yield break;
        foreach (Vector2Int cell in RockSkillBase.Area(user.GridPosition, user.GridWidth, user.GridHeight, 1))
        {
            Character enemy = grid.GetCharacterAt(cell);
            if (enemy == null || enemy == user || enemy.IsPlayer == user.IsPlayer ||
                enemy.Stats == null || !enemy.Stats.IsAlive || enemy.IsDying)
                continue;
            // Stop after the first adjacent enemy. The Q itself finds and
            // deduplicates every target in its normal two-cell area.
            yield return new RockBasicSkill().ExecuteFree(user, grid);
            yield break;
        }
    }
}
