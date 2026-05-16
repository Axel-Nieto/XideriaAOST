using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private Button button;

    private int skillIndex;
    private SkillBase currentSkill;
    private Character currentCharacter;
    private BattleHUD battleHUD;

    public void Initialize(
        int index,
        string key,
        BattleHUD hud)
    {
        skillIndex = index;
        battleHUD = hud;

        keyText.text = key;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    public void SetSkill(Character character, SkillBase skill)
    {
        currentCharacter = character;
        currentSkill = skill;

        if (skill == null)
        {
            cooldownText.text = "";
            darkOverlay.SetActive(true);
            button.interactable = false;
            return;
        }

        int cooldown = character.GetSkillCooldown(skill);
        bool hasEnergy = character.Stats.HasEnergy(skill.EnergyCost);
        bool usable = cooldown <= 0 && hasEnergy;

        cooldownText.text = cooldown > 0 ? cooldown.ToString() : "";

        darkOverlay.SetActive(!usable);
        button.interactable = usable;
    }

    private void OnClick()
    {
        battleHUD.SelectSkill(skillIndex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentSkill == null)
            return;

        battleHUD.ShowSkillTooltip(currentSkill);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        battleHUD.HideTooltip();
    }
}