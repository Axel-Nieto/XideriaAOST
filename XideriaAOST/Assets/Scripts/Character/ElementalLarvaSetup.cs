using UnityEngine;

public enum LarvaElement { Air, Earth, Fire }

[RequireComponent(typeof(Character))]
[DisallowMultipleComponent]
public sealed class ElementalLarvaSetup : MonoBehaviour
{
    [SerializeField] private LarvaElement element;
    [SerializeField, Min(0)] private int specialEnergyCost = 3;
    [SerializeField, Min(0)] private int specialCooldown = 3;
    [Header("Fire")]
    [SerializeField, Min(0)] private int fireDamage = 3;
    [SerializeField, Min(1)] private int burnDuration = 3;
    [SerializeField, Min(1)] private int burnDamage = 2;

    private void Start()
    {
        Character character = GetComponent<Character>();
        SkillBase special = element switch
        {
            LarvaElement.Air => new LarvaPullSkill(specialEnergyCost, specialCooldown),
            LarvaElement.Earth => new LarvaChainSkill(specialEnergyCost, specialCooldown),
            _ => new LarvaFireSpitSkill(fireDamage, burnDuration, burnDamage, specialEnergyCost, specialCooldown)
        };
        // LarvaAI selects the first usable skill. Prefer the chain at range 1 too,
        // and fall back to the same bite as the poison larva while it is on cooldown.
        character.AddSkill(special);
        character.AddSkill(new BiteSkill());
    }
}
