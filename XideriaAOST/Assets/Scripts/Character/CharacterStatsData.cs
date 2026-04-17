using UnityEngine;

[System.Serializable]
public class CharacterStatsData
{
    [Min(1)] public int attack = 5;
    [Min(0)] public int defense = 2;
    [Min(1)] public int maxHP = 20;
    [Min(1)] public int maxEnergy = 7;
    [Min(0)] public int energyRegen = 3;
}
