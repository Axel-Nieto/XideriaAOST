using System.Collections;
using UnityEngine;

public class WaterBasicSkill : SkillBase
{
    private readonly int damage = 3;
    private readonly int pushDistance = 1;

    public WaterBasicSkill()
        : base(
            name: "Chorro de Agua",
            description: "Dispara agua a distancia y empuja al objetivo.",
            energyCost: 3,
            cooldown: 0,
            minRange: 2,
            maxRange: 4,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Water,
            icon: SkillIconLibrary.Instance != null
            ? SkillIconLibrary.Instance.basicAttack
            : null)
    {
    }

    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (user == null || grid == null) return false;
        if (!user.Stats.HasEnergy(EnergyCost)) return false;
        if (!grid.IsValidCell(targetCell)) return false;
        if (!IsInRange(user.GridPosition, targetCell)) return false;
        if (user.IsSkillOnCooldown(this)) return false;

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null) return false;
        if (target.IsPlayer == user.IsPlayer) return false;

        return true;
    }

    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        user.FaceCell(targetCell);

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        Character target = grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        var anim = user.GetComponentInChildren<CharacterAnimationController>();

        if (anim != null)
            yield return anim.PlayAttackUntilHitFrame();

        yield return BasicAttackVFX.Play(target, DamageType.Water);

        int finalDamage = Mathf.Max(
            1,
            damage + user.Stats.Attack - target.Stats.Defense
        );

        target.TakeDamage(finalDamage, DamageType.Water, user);

        if (!target.Stats.IsAlive || target.IsDying)
            yield break;

        yield return TryPush(user, target, grid);

        if (anim != null)
            yield return anim.WaitAttackFinished();
    }

    private IEnumerator TryPush(Character user, Character target, GridSystem grid)
    {
        Vector2Int direction = target.GridPosition - user.GridPosition;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            direction = new Vector2Int(direction.x > 0 ? 1 : -1, 0);
        else
            direction = new Vector2Int(0, direction.y > 0 ? 1 : -1);

        Vector2Int origin = target.GridPosition;
        Vector2Int destination = origin + direction * pushDistance;

        if (grid.IsBlockedForMovement(destination))
            yield break;

        grid.UnregisterCharacter(origin);

        Vector3 startPosition = target.transform.position;
        Vector3 endPosition = grid.GridToWorld(destination) + target.VisualOffset;

        float pushSpeed = 10f;

        while (Vector3.Distance(target.transform.position, endPosition) > 0.05f)
        {
            target.transform.position = Vector3.MoveTowards(
                target.transform.position,
                endPosition,
                pushSpeed * Time.deltaTime
            );

            yield return null;
        }

        target.transform.position = endPosition;

        target.SetGridPosition(destination);
        grid.RegisterCharacter(destination, target);

        target.ProcessCurrentTerrain();
    }

    public override string GetTooltipDetails(Character user)
    {
        int estimatedDamage = user == null
            ? damage
            : damage + user.Stats.Attack;

        return
            $"Daño: {estimatedDamage}";
    }
}