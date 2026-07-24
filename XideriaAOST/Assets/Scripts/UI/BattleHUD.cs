using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BattleHUD : MonoBehaviour
{
    [Header("Character Panel")]
    [SerializeField] private GameObject characterPanel;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image energyFillImage;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private TextMeshProUGUI helmetText;

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

    [Header("Skill Frame Colors")]
    [SerializeField] private string defaultFrameHex = "#FFFFFF";
    [SerializeField] private string fireFrameHex = "#D73C3C";
    [SerializeField] private string waterFrameHex = "#4E9BEE";

    [Header("End Turn Button")]
    [SerializeField] private EndTurnButtonPulse endTurnPulse;

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
                text += $"- {effect.EffectName} ({effect.RemainingTurns})\n";
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

        characterNameText.text = currentCharacter.name;

        hpFillImage.fillAmount =
            stats.MaxHP <= 0 ? 0f : (float)stats.CurrentHP / stats.MaxHP;

        hpText.text = $"{stats.CurrentHP} / {stats.MaxHP}";

        energyFillImage.fillAmount =
            stats.MaxEnergy <= 0 ? 0f : (float)stats.CurrentEnergy / stats.MaxEnergy;

        energyText.text = $"{stats.CurrentEnergy} / {stats.MaxEnergy}";

        helmetText.text =
            currentCharacter.IsPlayer && currentCharacter.EquippedHelmet != null
                ? currentCharacter.EquippedHelmet.Name
                : "";
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
        Color frameColor = GetFrameColor(currentCharacter);

        for (int i = 0; i < skillSlots.Length; i++)
        {
            SkillBase skill = null;

            if (skills != null && i < skills.Count)
                skill = skills[i];

            skillSlots[i].SetSkill(currentCharacter, skill);
            skillSlots[i].SetFrameColor(frameColor);
            skillSlots[i].SetSelected(i == selectedSkillIndex);
        }
    }

    private Color GetFrameColor(Character character)
    {
        if (character == null || !character.IsPlayer || character.EquippedHelmet == null)
            return Hex(defaultFrameHex);

        return character.EquippedHelmet.Name switch
        {
            "Casco de Fuego" => Hex(fireFrameHex),
            "Casco de Agua" => Hex(waterFrameHex),
            _ => Hex(defaultFrameHex)
        };
    }

    private Color Hex(string hex)
    {
        if (UnityEngine.ColorUtility.TryParseHtmlString(hex, out Color color))
            return color;

        return Color.white;
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