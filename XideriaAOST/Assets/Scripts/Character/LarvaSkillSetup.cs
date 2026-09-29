using UnityEngine;

public class LarvaSkillSetup : MonoBehaviour
{
    [Header("Bite")]
    [SerializeField] private int biteDamage = 4;
    [SerializeField] private int biteMinRange = 1;
    [SerializeField] private int biteMaxRange = 1;

    [Header("Poison Spit")]
    [SerializeField] private int poisonSpitDamage = 3;
    [SerializeField] private int poisonSpitMinRange = 2;
    [SerializeField] private int poisonSpitMaxRange = 4;
    [SerializeField] private int poisonDuration = 3;
    [SerializeField] private int poisonDamage = 2;

    private void Start()
    {
        Character character = GetComponent<Character>();

        if (character == null)
        {
            Debug.LogError("LarvaSkillSetup necesita Character.");
            return;
        }

        character.AddSkill(
            new BiteSkill(
            )
        );

        character.AddSkill(
            new PoisonSpitSkill(
                poisonSpitDamage,
                poisonSpitMinRange,
                poisonSpitMaxRange,
                poisonDuration,
                poisonDamage
            )
        );
    }
}
