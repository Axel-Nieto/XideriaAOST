using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CharacterInfoHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI infoText;

    private Character currentCharacter;

    public void Show(Character character)
    {
        currentCharacter = character;
        Refresh();
    }

    public void Refresh()
    {
        if (currentCharacter == null)
        {
            Clear();
            return;
        }

        var stats = currentCharacter.Stats;
        List<SkillBase> skills = currentCharacter.GetActiveSkills();

        string text =
            $"{currentCharacter.name}\n" +
            $"HP: {stats.CurrentHP} / {stats.MaxHP}\n" +
            $"Energía: {stats.CurrentEnergy} / {stats.MaxEnergy}\n" +
            $"Ataque: {stats.Attack}\n" +
            $"Defensa: {stats.Defense}\n\n";

        if (currentCharacter.IsPlayer && currentCharacter.EquippedHelmet != null)
            text += $"Casco: {currentCharacter.EquippedHelmet.Name}\n";

        if (skills != null && skills.Count > 0)
        {
            text += "\nHabilidades:\n";

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];

                text += $"{i + 1} - {skill.Name} | Coste: {skill.EnergyCost}\n";
                text += $"    {skill.Description}\n";
            }
        }
        else
        {
            text += "\nSin habilidades activas\n";
        }

        infoText.text = text;
    }

    public void Clear()
    {
        currentCharacter = null;
        infoText.text = "";
    }
}