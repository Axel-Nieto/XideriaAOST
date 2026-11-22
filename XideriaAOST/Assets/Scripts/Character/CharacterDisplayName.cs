using System.Text.RegularExpressions;

// Player-facing labels are independent of prefab/spawn names and Unity suffixes.
public static class CharacterDisplayName
{
    private static readonly Regex instanceSuffix = new(@"(?:\s*(?:\(Clone\)|\(\d+\)))+$", RegexOptions.IgnoreCase);
    private static readonly Regex whitespace = new(@"\s+");

    public static string For(Character character)
    {
        if (character == null) return "";
        string clean = whitespace.Replace(instanceSuffix.Replace(character.name.Trim(), "").Replace('_', ' '), " ").Trim();
        if (character.IsPlayer) return clean;
        if (character.GetComponent<BossAI>() != null) return "Jefe de la Zona 1";
        var elemental = character.GetComponent<ElementalLarvaSetup>();
        if (elemental != null)
            return elemental.Element switch
            {
                LarvaElement.Air => "Larva de Viento",
                LarvaElement.Earth => "Larva de Tierra",
                LarvaElement.Fire => "Larva de Fuego",
                _ => clean
            };
        string key = clean.Replace(" ", "").Replace("-", "").ToLowerInvariant();
        return key switch
        {
            "larvaaire" or "larbaaire" or "larvaviento" => "Larva de Viento",
            "larvatierra" or "larbatierra" => "Larva de Tierra",
            "larvafuego" or "larbafuego" => "Larva de Fuego",
            "larvaveneno" or "larbaveneno" => "Larva de Veneno",
            "larva" or "larba" => "Larva",
            "bosszone1combat" or "bosszone1" or "1boss" or "jefezona1" => "Jefe de la Zona 1",
            _ => clean
        };
    }
}
