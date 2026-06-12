using System.Collections.Generic;
using Unity.VisualScripting;

public static class HelmetFactory
{
    public static HelmetData CreateDefaultHelmet()
    {
        return new HelmetData
        {
            Type = HelmetType.Default,
            Name = "Casco Básico",
            Description = "Un casco simple.",
            ActiveSkills = new List<SkillBase>
            {
                new BasicAttackSkill()
            }
        };
    }

    public static HelmetData CreateFireHelmet()
    {
        return new HelmetData
        {
            Type = HelmetType.Fire,
            Name = "Casco de Fuego",
            Description = "Dominio del fuego.",
            ActiveSkills = new List<SkillBase>
            {
                new FireBasicAttackSkill(),
                new FireballSkill(),
                new FlameDashSkill(),
                new FireExplosionSkill()
            }
        };
    }

    public static HelmetData CreateWaterHelmet()
    {
        return new HelmetData
        {
            Type = HelmetType.Water,
            Name = "Casco de Agua",
            Description = "Dominio del agua.",
            ActiveSkills = new List<SkillBase>
            {
                new WaterBasicSkill(),
                new WaterHealSkill(),
                new WaterWaveSkill(),
                new WaterPrisonSkill()
            },
            PassiveEffects = new List<PassiveEffect>
            {
                new PoisonResistancePassive()
            }

        };
    }
}
