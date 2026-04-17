using System.Collections;
using UnityEngine;

public abstract class SkillBase
{
    public string Name { get; }
    public string Description { get; }

    public int EnergyCost { get; }
    public int Cooldown { get; }

    public int MinRange { get; }
    public int MaxRange { get; }

    protected SkillBase(
        string name,
        string description,
        int energyCost,
        int cooldown,
        int minRange,
        int maxRange)
    {
        Name = name;
        Description = description;
        EnergyCost = energyCost;
        Cooldown = cooldown;
        MinRange = minRange;
        MaxRange = maxRange;
    }

    public bool IsInRange(Vector2Int origin, Vector2Int target)
    {
        int distance = Mathf.Abs(origin.x - target.x) +
                       Mathf.Abs(origin.y - target.y);

        return distance >= MinRange && distance <= MaxRange;
    }

    public abstract bool CanUse(Character user, Character target);
    public abstract IEnumerator Execute(Character user, Character target);
}
