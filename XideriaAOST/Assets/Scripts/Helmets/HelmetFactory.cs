public static class HelmetFactory
{
    public static HelmetData CreateDefaultHelmet()
    {
        return new HelmetData
        {
            Name = "Casco Básico",
            Description = "Sin efectos.",
            BasicSkill = new BasicAttackSkill()
        };
    }

    public static HelmetData CreateFireHelmet()
    {
        return new HelmetData
        {
            Name = "Casco de Fuego",
            Description = "Ataque básico aplica fuego.",
            BasicSkill = new FireBasicAttackSkill()
        };
    }
}
