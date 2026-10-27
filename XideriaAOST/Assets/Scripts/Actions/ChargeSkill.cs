using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChargeSkill : SkillBase
{
    private readonly int damage;
    private readonly int minPushDistance;
    private readonly int maxPushDistance;
    private readonly float chargeSpeed;

    public ChargeSkill(
        int damage = 3,
        int minPushDistance = 2,
        int maxPushDistance = 3,
        float chargeSpeed = 12f)
        : base(
            name: "Embestida",
            description:
                "Carga en l�nea recta contra el objetivo, inflige da�o y lo empuja entre 2 y 3 celdas.",
            energyCost: 5,
            cooldown: 2,
            minRange: 2,
            maxRange: 6,
            targetType: SkillTargetType.TargetCell,
            damageType: DamageType.Physical,
            icon: null)
    {
        this.damage = damage;
        this.minPushDistance = minPushDistance;
        this.maxPushDistance = maxPushDistance;
        this.chargeSpeed = chargeSpeed;
    }

    public override bool CanUse(
        Character user,
        Vector2Int targetCell,
        GridSystem grid)
    {
        if (user == null || grid == null)
            return false;

        if (!user.Stats.HasEnergy(EnergyCost))
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

        if (!grid.IsValidCell(targetCell))
            return false;

        Character target =
            grid.GetCharacterAt(targetCell);

        if (target == null)
            return false;

        if (target.IsPlayer == user.IsPlayer)
            return false;

        if (!IsInRange(user, target))
            return false;

        Vector2Int direction =
            GetChargeDirection(user, target);

        if (direction == Vector2Int.zero)
            return false;

        if (!AreAlignedForCharge(
        user,
        target,
        direction))
        {
            return false;
        }

        List<Vector2Int> path =
        BuildChargePath(
            user,
            user.GridPosition,
            target,
            direction,
            grid
        );

        return path.Count > 0;
    }

    public override IEnumerator Execute(
        Character user,
        Vector2Int targetCell,
        GridSystem grid)
    {
        Character target =
            grid.GetCharacterAt(targetCell);

        if (target == null)
            yield break;

        Vector2Int direction =
            GetChargeDirection(user, target);

        if (direction == Vector2Int.zero)
            yield break;

        if (!AreAlignedForCharge(
        user,
        target,
        direction))
        {
            yield break;
        }

        List<Vector2Int> path =
    BuildChargePath(
        user,
        user.GridPosition,
        target,
        direction,
        grid
    );

        if (path.Count == 0)
            yield break;

        user.Stats.ConsumeEnergy(EnergyCost);
        user.StartCooldown(this);

        CharacterMovement movement =
            user.GetComponent<CharacterMovement>();

        if (movement == null)
            yield break;

        yield return movement.MoveAlongRoutine(
            path,
            chargeSpeed
        );

        // Comprobamos nuevamente que el objetivo siga
        // siendo alcanzable despu�s de la carga.
        if (target == null ||
            !target.Stats.IsAlive ||
            target.IsDying)
        {
            yield break;
        }

        if (!IsAdjacent(user, target))
            yield break;

        int finalDamage = Mathf.Max(
            1,
            damage
            + user.Stats.Attack
            - target.Stats.Defense
        );

        target.TakeDamage(
            finalDamage,
            DamageType.Physical,
            user
        );

        if (target.Stats.IsAlive &&
            !target.IsDying)
        {
            int pushDistance =
                Random.Range(
                    minPushDistance,
                    maxPushDistance + 1
                );

            yield return PushSystem.Push(
                user,
                target,
                grid,
                pushDistance
            );
        }
    }

    private List<Vector2Int> BuildChargePath(
    Character user,
    Vector2Int startPosition,
    Character target,
    Vector2Int direction,
    GridSystem grid)
    {
        List<Vector2Int> path = new();

        Vector2Int current = startPosition;

        for (int i = 0; i < MaxRange; i++)
        {
            Vector2Int next = current + direction;

            // Si desde la posici�n actual ya estamos
            // adyacentes, no necesitamos avanzar m�s.
            if (IsAdjacentFromPosition(
                    user,
                    current,
                    target))
            {
                break;
            }

            // La Larva completa debe caber.
            if (!grid.CanCharacterOccupy(
                    user,
                    next,
                    ignoreSelf: true))
            {
                break;
            }

            // Seguridad adicional: nunca atravesar
            // el footprint del objetivo.
            if (FootprintsOverlap(
                    user,
                    next,
                    target))
            {
                break;
            }

            path.Add(next);
            current = next;

            // Llegamos a posici�n de impacto.
            if (IsAdjacentFromPosition(
                    user,
                    current,
                    target))
            {
                return path;
            }
        }

        // Si recorrimos el camino pero nunca terminamos
        // junto al objetivo, la Embestida no es v�lida.
        path.Clear();

        return path;
    }

    private Vector2Int GetChargeDirection(
        Character user,
        Character target)
    {
        Vector2Int direction =
            PushSystem.GetPushDirection(
                user,
                target
            );

        return direction;
    }

    private bool IsAdjacent(
        Character user,
        Character target)
    {
        return IsAdjacentFromPosition(
            user,
            user.GridPosition,
            target
        );
    }

    private bool IsAdjacentFromPosition(
        Character user,
        Vector2Int userPosition,
        Character target)
    {
        foreach (Vector2Int userCell
                 in user.GetOccupiedCells(userPosition))
        {
            foreach (Vector2Int targetCell
                     in target.GetOccupiedCells())
            {
                int distance =
                    Mathf.Abs(
                        userCell.x - targetCell.x)
                    +
                    Mathf.Abs(
                        userCell.y - targetCell.y);

                if (distance == 1)
                    return true;
            }
        }

        return false;
    }

    private bool FootprintsOverlap(
    Character user,
    Vector2Int userPosition,
    Character target)
    {
        foreach (Vector2Int userCell
                 in user.GetOccupiedCells(userPosition))
        {
            foreach (Vector2Int targetCell
                     in target.GetOccupiedCells())
            {
                if (userCell == targetCell)
                    return true;
            }
        }

        return false;
    }

    public override string GetTooltipDetails(
        Character user)
    {
        int estimatedDamage =
            user == null
                ? damage
                : damage + user.Stats.Attack;

        return
            $"Da�o: {estimatedDamage}\n" +
            $"Empuje: {minPushDistance}-{maxPushDistance}";
    }

    private bool AreAlignedForCharge(
    Character user,
    Character target,
    Vector2Int direction)
    {
        if (direction.x != 0)
        {
            // Carga horizontal:
            // al menos una fila de ambos footprints debe coincidir.
            foreach (Vector2Int userCell in user.GetOccupiedCells())
            {
                foreach (Vector2Int targetCell in target.GetOccupiedCells())
                {
                    if (userCell.y == targetCell.y)
                        return true;
                }
            }

            return false;
        }

        if (direction.y != 0)
        {
            // Carga vertical:
            // al menos una columna debe coincidir.
            foreach (Vector2Int userCell in user.GetOccupiedCells())
            {
                foreach (Vector2Int targetCell in target.GetOccupiedCells())
                {
                    if (userCell.x == targetCell.x)
                        return true;
                }
            }

            return false;
        }

        return false;
    }

    public override bool CanUseFromPosition(
    Character user,
    Vector2Int simulatedPosition,
    Character target,
    GridSystem grid,
    int availableEnergy)
    {
        if (user == null ||
            target == null ||
            grid == null)
        {
            return false;
        }

        if (availableEnergy < EnergyCost)
            return false;

        if (user.IsSkillOnCooldown(this))
            return false;

        if (!IsInRange(
                user,
                simulatedPosition,
                target))
        {
            return false;
        }

        Vector2Int direction =
            GetChargeDirectionFromPosition(
                user,
                simulatedPosition,
                target
            );

        if (direction == Vector2Int.zero)
            return false;

        if (!AreAlignedForChargeFromPosition(
                user,
                simulatedPosition,
                target,
                direction))
        {
            return false;
        }

        List<Vector2Int> path =
            BuildChargePath(
                user,
                simulatedPosition,
                target,
                direction,
                grid
            );

        return path.Count > 0;
    }

    private Vector2Int GetChargeDirectionFromPosition(
    Character user,
    Vector2Int userPosition,
    Character target)
    {
        Vector2Int closestUserCell = userPosition;
        Vector2Int closestTargetCell = target.GridPosition;

        int bestDistance = int.MaxValue;

        foreach (Vector2Int userCell
                 in user.GetOccupiedCells(userPosition))
        {
            foreach (Vector2Int targetCell
                     in target.GetOccupiedCells())
            {
                int distance =
                    Mathf.Abs(targetCell.x - userCell.x) +
                    Mathf.Abs(targetCell.y - userCell.y);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closestUserCell = userCell;
                    closestTargetCell = targetCell;
                }
            }
        }

        Vector2Int delta =
            closestTargetCell - closestUserCell;

        if (delta == Vector2Int.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return new Vector2Int(
                delta.x > 0 ? 1 : -1,
                0
            );
        }

        return new Vector2Int(
            0,
            delta.y > 0 ? 1 : -1
        );
    }

    private bool AreAlignedForChargeFromPosition(
    Character user,
    Vector2Int userPosition,
    Character target,
    Vector2Int direction)
    {
        if (direction.x != 0)
        {
            foreach (Vector2Int userCell
                     in user.GetOccupiedCells(userPosition))
            {
                foreach (Vector2Int targetCell
                         in target.GetOccupiedCells())
                {
                    if (userCell.y == targetCell.y)
                        return true;
                }
            }

            return false;
        }

        if (direction.y != 0)
        {
            foreach (Vector2Int userCell
                     in user.GetOccupiedCells(userPosition))
            {
                foreach (Vector2Int targetCell
                         in target.GetOccupiedCells())
                {
                    if (userCell.x == targetCell.x)
                        return true;
                }
            }

            return false;
        }

        return false;
    }
}
