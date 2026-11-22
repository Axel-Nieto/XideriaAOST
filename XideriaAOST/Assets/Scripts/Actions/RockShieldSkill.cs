using System.Collections;
using UnityEngine;

public sealed class RockShieldSkill : RockSkillBase
{
    public RockShieldSkill() : base("Escudo de Roca", "Obtienes tres cargas de escudo. Cada golpe recibido consume una y reduce su daño un 10%. Su duración base es de un turno propio.", 5, 3,
        icon: SkillIconLibrary.Instance != null ? SkillIconLibrary.Instance.rockShield : null) { }
    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid) => Ready(user, grid, user?.Stats?.CurrentEnergy ?? 0);
    public override bool CanUseFromPosition(Character user, Vector2Int origin, Character target, GridSystem grid, int energy) => Ready(user, grid, energy);
    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (!CanUse(user, targetCell, grid)) yield break;
        Spend(user);
        yield return Cast(user);
        RockGuardPassive.For(user).ActivateShield(RockGuardPassive.ShieldDuration(user));
        user.GetComponent<RockSkillVFX>()?.PlayShield();
        yield return FinishCast(user);
    }

    public override string GetTooltipDetails(Character user) =>
        $"Duraci\u00f3n: {RockGuardPassive.ShieldDuration(user)} turnos propios. Tres cargas; la mejora de duraci\u00f3n no agrega cargas.";
}
