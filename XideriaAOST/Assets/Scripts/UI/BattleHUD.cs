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
    [SerializeField] private TextMeshProUGUI tooltipTitleText;
    [SerializeField] private TextMeshProUGUI tooltipDescriptionText;
    [SerializeField] private TextMeshProUGUI tooltipStatsText;

    [Header("Turn")]
    [SerializeField] private Button endTurnButton;

    [Header("Input")]
    [SerializeField] private GridInputHandler inputHandler;

    

    [Header("End Turn Button")]
    [SerializeField] private EndTurnButtonPulse endTurnPulse;

    [Header("Theme Controller")]
    [SerializeField] private BattleHUDThemeController themeController;

    [Header("Helmet Charges")]
    [SerializeField] private GameObject helmetChargesBadge;
    [SerializeField] private TextMeshProUGUI helmetChargesText;

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
        RefreshHelmetCharges();

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

        tooltipTitleText.text = title;
        tooltipDescriptionText.text = description;
        tooltipStatsText.text = "";
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

    public void ShowSkillTooltip(SkillBase skill, int skillIndex)
    {
        if (skill == null)
            return;

        tooltipPanel.SetActive(true);

        // TÍTULO
        string keyText =
            skillIndex >= 0 && skillIndex < keys.Length
                ? $" [{keys[skillIndex]}]"
                : "";

        tooltipTitleText.text = $"{skill.Name}{keyText}";


        // DESCRIPCIÓN
        string details = skill.GetTooltipDetails(currentCharacter);

        tooltipDescriptionText.text =
            string.IsNullOrWhiteSpace(details)
                ? skill.Description
                : $"{skill.Description}\n{details}";


        // STATS
        string rangeText = skill.TargetType == SkillTargetType.Self
            ? "Personal"
            : $"{skill.MinRange}-{skill.MaxRange}";

        string areaText = skill.AreaRadius > 0
            ? $"   Área: {skill.AreaRadius}"
            : "";

        tooltipStatsText.text =
            $"Costo: {skill.EnergyCost}   " +
            $"CD: {skill.Cooldown}   " +
            $"Rango: {rangeText}" +
            areaText;
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

    private void RefreshHelmetCharges()
    {
        if (helmetChargesBadge == null ||
            helmetChargesText == null)
            return;

        if (currentCharacter == null ||
            !currentCharacter.IsPlayer ||
            currentCharacter.EquippedHelmet == null ||
            currentCharacter.EquippedHelmet.Type == HelmetType.Default)
        {
            helmetChargesBadge.SetActive(false);
            return;
        }

        helmetChargesBadge.SetActive(true);

        helmetChargesText.text =
            currentCharacter.HelmetCharges.ToString();
    }
}