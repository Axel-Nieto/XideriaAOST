using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BattleHUD : MonoBehaviour
{
    [Header("Character Panel")]
    [SerializeField] private GameObject characterPanel;
    [SerializeField] private TextMeshProUGUI hpCurrentText;
    [SerializeField] private TextMeshProUGUI hpMaxText;
    [SerializeField] private TextMeshProUGUI energyCurrentText;
    [SerializeField] private TextMeshProUGUI energyMaxText;

    [Header("Status / Passive Icons")]
    [SerializeField] private Transform statusIconContainer;
    [SerializeField] private StatusIconUI statusIconPrefab;
    [SerializeField] private Sprite defaultStatusIcon;
    [SerializeField] private Sprite defaultPassiveIcon;

    [Header("Skills")]
    [SerializeField] private SkillSlotUI[] skillSlots;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Header("Turn")]
    [SerializeField] private Button endTurnButton;

    [Header("Input")]
    [SerializeField] private GridInputHandler inputHandler;

    

    [Header("End Turn Button")]
    [SerializeField] private EndTurnButtonPulse endTurnPulse;

    [Header("Theme Controller")]
    [SerializeField] private BattleHUDThemeController themeController;

    private Character currentCharacter;
    private int selectedSkillIndex = -1;

    private bool themeInitialized = false;

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
            ClearSkills();
            return;
        }

        RefreshHUDTheme();

        RefreshCharacterPanel();
        RefreshStatusIcons();
        RefreshSkills();
        UpdateEndTurnPulse();
    }

    private void RefreshCharacterPanel()
    {
        if (currentCharacter == null)
        {
            characterPanel.SetActive(false);
            ClearStatusIcons();
            return;
        }

        characterPanel.SetActive(true);

        var stats = currentCharacter.Stats;

        hpCurrentText.text = stats.CurrentHP.ToString();
        hpMaxText.text = stats.MaxHP.ToString();

        energyCurrentText.text = stats.CurrentEnergy.ToString();
        energyMaxText.text = stats.MaxEnergy.ToString();
    }

    public void ShowGenericTooltip(string title, string description)
    {
        tooltipPanel.SetActive(true);

        tooltipText.text =
            $"{title}\n" +
            $"{description}";
    }

    public void SetPlayerTurn(bool isPlayerTurn)
    {
        endTurnButton.interactable = isPlayerTurn;

        if (!isPlayerTurn && endTurnPulse != null)
            endTurnPulse.SetPulsing(false);
    }

    public void SetSelectedSkill(int index)
    {
        selectedSkillIndex = index;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            skillSlots[i].SetSelected(i == selectedSkillIndex);
        }

        RefreshSkills();
    }

    private void RefreshSkills()
    {
        if (currentCharacter == null || !currentCharacter.IsPlayer)
        {
            ClearSkills();
            return;
        }

        List<SkillBase> skills = currentCharacter.GetActiveSkills();

        for (int i = 0; i < skillSlots.Length; i++)
        {
            SkillBase skill = null;

            if (skills != null && i < skills.Count)
                skill = skills[i];

            skillSlots[i].SetSkill(currentCharacter, skill);
            skillSlots[i].SetSelected(i == selectedSkillIndex);
        }
    }

    private void RefreshHUDTheme()
    {
        if (themeController == null)
            return;

        if (currentCharacter == null || !currentCharacter.IsPlayer)
            return;

        HelmetType helmetType = GetHelmetType(currentCharacter);

        themeController.SetHelmetTheme(
            helmetType,
            animate: themeInitialized
        );

        themeInitialized = true;
    }


    private HelmetType GetHelmetType(Character character)
    {
        if (character == null ||
            !character.IsPlayer ||
            character.EquippedHelmet == null)
        {
            return HelmetType.Default;
        }

        return character.EquippedHelmet.Name switch
        {
            "Casco de Fuego" => HelmetType.Fire,
            "Casco de Agua" => HelmetType.Water,
            _ => HelmetType.Default
        };
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
        inputHandler.SelectSkillFromUI(index);
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

        string details = skill.GetTooltipDetails(currentCharacter);

        string detailsText = string.IsNullOrWhiteSpace(details)
            ? ""
            : $"{details}\n\n";

        tooltipText.text =
            $"{skill.Name}\n" +
            $"{skill.Description}\n\n" +
            detailsText +
            $"Coste: {skill.EnergyCost}\n" +
            $"Cooldown: {skill.Cooldown}\n" +
            $"Tipo: {skill.DamageType}\n" +
            $"{rangeText}{areaText}";
    }

    public void HideTooltip()
    {
        tooltipPanel.SetActive(false);
    }

    public void FlashSkillSelected(int index)
    {
        if (index < 0 || index >= skillSlots.Length)
            return;

        skillSlots[index].FlashSelected();
    }

    public void FlashSkillError(int index)
    {
        if (index < 0 || index >= skillSlots.Length)
            return;

        skillSlots[index].FlashError();
    }

    public void Clear()
    {
        currentCharacter = null;
        selectedSkillIndex = -1;

        HideTooltip();
        ClearSkills();
    }

    private bool HasUsableSkill(Character character)
    {
        if (character == null)
            return false;

        if (!character.IsPlayer)
            return false;

        var skills = character.GetActiveSkills();

        if (skills == null || skills.Count == 0)
            return false;

        foreach (var skill in skills)
        {
            if (skill == null)
                continue;

            bool hasEnergy = character.Stats.HasEnergy(skill.EnergyCost);
            bool isOnCooldown = character.IsSkillOnCooldown(skill);

            if (hasEnergy && !isOnCooldown)
                return true;
        }

        return false;
    }

    private void UpdateEndTurnPulse()
    {
        if (endTurnPulse == null)
            return;

        bool shouldPulse =
            currentCharacter != null &&
            currentCharacter.IsPlayer &&
            !HasUsableSkill(currentCharacter);

        endTurnPulse.SetPulsing(shouldPulse);
    }

    private void RefreshStatusIcons()
    {
        ClearStatusIcons();

        if (currentCharacter == null)
            return;

        foreach (var effect in currentCharacter.GetStatusEffects())
        {
            StatusIconUI icon = Instantiate(
                statusIconPrefab,
                statusIconContainer
            );

            Sprite effectIcon =
                effect.Icon != null
                ? effect.Icon
                : StatusIconDatabase.Instance.defaultStatusIcon;

            icon.Setup(
                effectIcon,
                effect.EffectName,
                effect.Description,
                effect.RemainingTurns,
                this
            );
        }

        foreach (var passive in currentCharacter.GetPassiveEffects())
        {
            StatusIconUI icon = Instantiate(
                statusIconPrefab,
                statusIconContainer
            );

            Sprite passiveIcon =
                passive.Icon != null
                ? passive.Icon
                : StatusIconDatabase.Instance.defaultPassiveIcon;

            icon.Setup(
                passiveIcon,
                passive.Name,
                passive.Description,
                0,
                this
            );
        }
    }

    private void ClearStatusIcons()
    {
        foreach (Transform child in statusIconContainer)
        {
            Destroy(child.gameObject);
        }
    }
}