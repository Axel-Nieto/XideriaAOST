using UnityEngine;

public class ArmoredLarvaSetup : MonoBehaviour
{
    [Header("Cornada")]
    [SerializeField] private int hornDamage = 5;
    [SerializeField] private int hornPushDistance = 1;

    [Header("Embestida")]
    [SerializeField] private int chargeDamage = 7;
    [SerializeField] private int chargeMinPushDistance = 2;
    [SerializeField] private int chargeMaxPushDistance = 3;
    [SerializeField] private float chargeSpeed = 12f;

    private void Start()
    {
        Character character = GetComponent<Character>();

        if (character == null)
        {
            Debug.LogError(
                "ArmoredLarvaSetup necesita Character."
            );

            return;
        }

        // Pasivas
        character.AddPassive(
            new LarvaArmorPassive()
        );

        character.AddPassive(
            new ColossusPassive()
        );

        // Skills
        character.AddSkill(
            new HornStrikeSkill(
                hornDamage,
                hornPushDistance
            )
        );

        character.AddSkill(
            new ChargeSkill(
                chargeDamage,
                chargeMinPushDistance,
                chargeMaxPushDistance,
                chargeSpeed
            )
        );
    }
}