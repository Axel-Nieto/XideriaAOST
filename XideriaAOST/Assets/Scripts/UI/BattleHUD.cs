using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleHUD : MonoBehaviour
{
    [Header("Character Panel")]
    [SerializeField] private TextMeshProUGUI characterInfoText;

    [Header("Skills")]
    [SerializeField] private SkillSlotUI[] skillSlots;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Header("Turn")]
    [SerializeField] private Button endTurnButton;

    [Header("Input")]
    [SerializeField] private GridInputHandler inputHandler;

    private Character currentCharacter;
    private int selectedSkillIndex = -1;

    private readonly string[] keys = { "Q", "W", "E", "R" };

    private void Awake()
    {
        for (int i = 0; i < skillSlots.Length; i++)
        {
            skillSlots[i].Initialize(i, keys[i], this);
        }

        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(EndTurn);

        HideTooltip();
    }

    public void ShowCharacter(Character character)
    {
        currentCharacter = character;
        selectedSkillIndex = -1;

        Refresh();
    }

    public void Refresh()
    {
        if (currentCharacter == null)
        {
            characterInfoText.text = "";
            ClearSkills();
            return;
        }

        var stats = currentCharacter.Stats;

        string text =
            $"{currentCharacter.name}\n" +
            $"HP: {stats.CurrentHP} / {stats.MaxHP}\n" +
            $"EN: {stats.CurrentEnergy} / {stats.MaxEnergy}\n";

        if (currentCharacter.IsPlayer && currentCharacter.EquippedHelmet != null)
        {
            text += $"Casco: {currentCharacter.EquippedHelmet.Name}\n";
        }

        var effects = currentCharacter.GetStatusEffects();

        if (effects != null && effects.Count > 0)
        {
            text += "\nEstados:\n";

            foreach (var effect in effects)
            {
                text +=
                    $"- {effect.EffectName} ({effect.RemainingTurns})\n";
            }
        }

        var passives = currentCharacter.GetPassiveEffects();

        if (passives != null && passives.Count > 0)
        {
            text += "\nPasivas:\n";

            foreach (var passive in passives)
            {
                text += $"- {passive.Name}\n";
            }
        }

        characterInfoText.text = text;

        RefreshSkills();
    }

    public void SetPlayerTurn(bool isPlayerTurn)
    {
        endTurnButton.interactable = isPlayerTurn;
    }

    public void SetSelectedSkill(int index)
    {
        selectedSkillIndex = index;
        RefreshSkills();
    }

    private void RefreshSkills()
    {
        List<SkillBase> skills = currentCharacter.GetActiveSkills();

        for (int i = 0; i < skillSlots.Length; i++)
        {
            SkillBase skill = null;

            if (skills != null && i < skills.Count)
                skill = skills[i];

            skillSlots[i].SetSkill(currentCharacter, skill);
        }
    }

    private void ClearSkills()
    {
        foreach (var slot in skillSlots)
        {
            slot.SetSkill(null, null);
        }
    }

    public void SelectSkill(int index)
    {
        if (currentCharacter == null)
            return;

        selectedSkillIndex = index;

        inputHandler.SelectSkillFromUI(index);

        RefreshSkills();
    }

    private void EndTurn()
    {
        inputHandler.EndTurnFromUI();
    }

    public void ShowSkillTooltip(SkillBase skill)
    {
        if (skill == null)
            return;

        tooltipPanel.SetActive(true);

        string rangeText = skill.TargetType == SkillTargetType.Self
            ? "No requiere objetivo"
            : $"Rango: {skill.MinRange}-{skill.MaxRange}";

        string areaText = skill.AreaRadius > 0
            ? $" | Área: radio {skill.AreaRadius}"
            : "";

        tooltipText.text =
            $"{skill.Name}\n" +
            $"{skill.Description}\n\n" +
            $"Coste: {skill.EnergyCost}\n" +
            $"Cooldown: {skill.Cooldown}\n" +
            $"Tipo: {skill.DamageType}\n" +
            $"{rangeText}{areaText}";
    }

    public void HideTooltip()
    {
        tooltipPanel.SetActive(false);
    }
}
