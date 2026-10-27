using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SkillBase
{
    public string Name { get; }
    public string Description { get; }
    public int EnergyCost { get; }
    public int Cooldown { get; }
    public int MinRange { get; }
    public int MaxRange { get; }
    public SkillTargetType TargetType { get; }
    public DamageType DamageType { get; }
    public virtual int AreaRadius => 0;
    public virtual bool ShowOnlyUsableTargets => false;
    public Sprite Icon { get; }

    protected SkillBase(
    string name,
    string description,
    int energyCost,
    int cooldown,
    int minRange,
    int maxRange,
    SkillTargetType targetType,
    DamageType damageType,
    Sprite icon = null)
    {
        Name = name;
        Description = description;
        EnergyCost = energyCost;
        Cooldown = cooldown;
        MinRange = minRange;
        MaxRange = maxRange;
        TargetType = targetType;
        DamageType = damageType;
        Icon = icon;
    }

    public bool IsInRange(Vector2Int origin, Vector2Int target)
    {
        int distance = Mathf.Abs(origin.x - target.x) + Mathf.Abs(origin.y - target.y);

        return distance >= MinRange && distance <= MaxRange;
    }

    public bool IsInRange(
    Character user,
    Vector2Int userPosition,
    Character target)
    {
        if (user == null || target == null)
            return false;

        int minDistance = int.MaxValue;

        foreach (Vector2Int userCell
                 in user.GetOccupiedCells(userPosition))
        {
            foreach (Vector2Int targetCell
                     in target.GetOccupiedCells())
            {
                int distance =
                    Mathf.Abs(userCell.x - targetCell.x) +
                    Mathf.Abs(userCell.y - targetCell.y);

                if (distance < minDistance)
                    minDistance = distance;
            }
        }

        return minDistance >= MinRange &&
               minDistance <= MaxRange;
    }

    public bool IsInRange(
    Character user,
    Character target)
    {
        if (user == null || target == null)
            return false;

        return IsInRange(
            user,
            user.GridPosition,
            target
        );
    }

    public abstract bool CanUse(Character user, Vector2Int targetCell, GridSystem grid);
    public abstract IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid);

    public virtual IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell)
    {
        yield return targetCell;
    }

    public virtual string GetTooltipDetails(Character user)
    {
        return "";
    }

    public virtual IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin,
        Vector2Int targetCell, GridSystem grid) => GetPreviewCells(origin, targetCell);

    public virtual bool CanUseFromPosition(
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

        return IsInRange(
            user,
            simulatedPosition,
            target
        );
    }
}
