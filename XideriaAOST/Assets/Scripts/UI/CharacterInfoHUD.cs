using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.Timeline;

public class CharacterInfoHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI infoText;
    private int selectedSkillIndex = -1;

    private Character currentCharacter;

    public void Show(Character character)
    {
        currentCharacter = character;
        Refresh();
    }

    public void SetSelectedSkill(int index)
    {
        selectedSkillIndex = index;
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
            text += $"CASCO: {currentCharacter.EquippedHelmet.Name}\n";

        var effects = currentCharacter.GetStatusEffects();

        if (effects.Count > 0)
        {
            text += "\nESTADOS:\n";

            foreach (var effect in effects)
            {
                text += $"- {effect.EffectName} ({effect.RemainingTurns})\n";
            }
        }

        if (skills != null && skills.Count > 0)
        {
            text += "\nHABILIDADES:\n";

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];

                string marker = i == selectedSkillIndex ? "> " : "  ";

                string key = i switch
                {
                    0 => "Q",
                    1 => "W",
                    2 => "E",
                    3 => "R",
                    _ => "-"
                };

                string rangeText = skill.TargetType == SkillTargetType.Self
                    ? "No requiere objetivo"
                    : $"Rango: {skill.MinRange}-{skill.MaxRange}";
                
                string areaText = skill.AreaRadius > 0
                    ? $" | Área: radio {skill.AreaRadius}"
                    : "";

                text += $"{marker}{key} - {skill.Name} | Coste: {skill.EnergyCost}\n";
                text += $"    Tipo: {skill.DamageType} | {rangeText}{areaText}\n";
                text += $"    Cooldown base: {skill.Cooldown}\n";
                text += $"    Disponible en: {currentCharacter.GetSkillCooldown(skill)} turnos\n";
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