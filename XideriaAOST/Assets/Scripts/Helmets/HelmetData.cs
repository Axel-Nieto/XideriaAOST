using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HelmetData
{
    public string Name;
    public string Description;
    public GameObject HelmetVisualPrefab;

    public List<SkillBase> ActiveSkills;
    public List<PassiveEffect> PassiveEffects;
}