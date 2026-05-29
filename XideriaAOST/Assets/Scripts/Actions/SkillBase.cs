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
}
