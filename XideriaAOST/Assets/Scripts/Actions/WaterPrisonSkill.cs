using System.Collections;
using UnityEngine;

public partial class WaterPrisonSkill : SkillBase
{
    private readonly int damage = 1;
    private readonly int rootDuration = 3;

    public WaterPrisonSkill()
        : base(
            name: "Prisión Marina",
            description: "Inflige da\u00f1o e impide moverse durante los pr\u00f3ximos dos turnos.",
            energyCost: 5,
            cooldown: 4,
            minRange: 2,
            maxRange: 6,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Water,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.waterPrison
            : null)
    {
    }

    public override bool CanUse(
    Character user,
    Vector2Int targetCell,
    GridSystem grid)
    {
        if (HasGlobalUpgrade(user))
            return CanUseGlobalPrison(user, grid, user?.Stats?.CurrentEnergy ?? 0);

        if (user == null || grid == null)
            return false;

        if (!user.Stats.HasEnergy(EnergyCost))
            return false;

        if (!grid.IsValidCell(targetCell))
            return false;

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            return false;

        if (target.IsPlayer == user.IsPlayer)
            return false;

        if (!IsInRange(user, target))
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (HasGlobalUpgrade(user))
        {
            yield return ExecuteGlobalPrison(user, grid);
            yield break;
        }
        user.FaceCell(targetCell);

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        CharacterAnimationController anim =
            user.GetComponentInChildren<CharacterAnimationController>();

        if (anim != null)
            yield return anim.PlayCastUntilHitFrame();

        int finalDamage = Mathf.Max(1, damage + user.Stats.Attack - target.Stats.Defense);

        target.TakeDamage(finalDamage, DamageType.Water, user);
        target.ApplyStatusEffect(new RootEffect(rootDuration));

        if (anim != null)
            yield return anim.WaitCastFinished();
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = damage + (user?.Stats?.Attack ?? 0);
        return $"Daño: {estimatedDamage}\nInmoviliza durante {rootDuration - 1} turnos del objetivo." + (HasGlobalUpgrade(user) ? "\nAfecta a todos los enemigos del campo. Pulsa R para seleccionar y otra vez para confirmar." : "");
    }
}