using System.Collections.Generic;

public static class HelmetFactory
{
    public static HelmetData CreateDefaultHelmet()
    {
        return new HelmetData
        {
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
}
