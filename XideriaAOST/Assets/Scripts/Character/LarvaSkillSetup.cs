using UnityEngine;

public class LarvaSkillSetup : MonoBehaviour
{
    private void Start()
    {
        Character character = GetComponent<Character>();

        if (character == null)
        {
            Debug.LogError("LarvaSkillSetup necesita Character.");
            return;
        }

        character.AddSkill(new BiteSkill());
        character.AddSkill(new PoisonSpitSkill());
    }
}
