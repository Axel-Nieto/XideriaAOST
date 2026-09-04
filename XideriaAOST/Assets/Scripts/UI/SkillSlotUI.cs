using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private TextMeshProUGUI cooldownText;

    [Header("Images")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject darkOverlay;

    [Header("Button")]
    [SerializeField] private Button button;

    [Header("Feedback Colors")]
    [SerializeField] private string selectedHex = "#FFD700";
    [SerializeField] private string errorHex = "#FF2D2D";

    private int skillIndex;
    private SkillBase currentSkill;
    private Character currentCharacter;
    private BattleHUD battleHUD;

    private Color currentFrameColor = Color.white;
    private bool isSelected;
    private Coroutine flashRoutine;

    public void Initialize(int index, string key, BattleHUD hud)
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
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            cooldownText.text = "";

            if (darkOverlay != null)
                darkOverlay.SetActive(true);

            button.interactable = false;
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = skill.Icon;
            iconImage.enabled = skill.Icon != null;
        }

        int cooldown = character.GetSkillCooldown(skill);
        bool hasEnergy = character.Stats.HasEnergy(skill.EnergyCost);
        bool usable = cooldown <= 0 && hasEnergy;

        cooldownText.text = cooldown > 0 ? cooldown.ToString() : "";

        if (darkOverlay != null)
            darkOverlay.SetActive(!usable);

        button.interactable = true;

        ApplyFrameColor();
    }

    public void SetFrameColor(Color color)
    {
        currentFrameColor = color;
        ApplyFrameColor();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyFrameColor();
    }

    public void FlashSelected()
    {
        Flash(Hex(selectedHex));
    }

    public void FlashError()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashColorRoutine(Hex(errorHex)));
    }

    private IEnumerator FlashColorRoutine(Color color)
    {
        Color previousColor = frameImage.color;

        frameImage.color = color;

        yield return new WaitForSeconds(0.15f);

        ApplyFrameColor();

        flashRoutine = null;
    }

    private void Flash(Color color)
    {
        if (frameImage == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(color));
    }

    private IEnumerator FlashRoutine(Color color)
    {
        frameImage.color = color;

        yield return new WaitForSeconds(0.15f);

        ApplyFrameColor();

        flashRoutine = null;
    }

    private void ApplyFrameColor()
    {
        if (frameImage == null)
            return;

        frameImage.color = isSelected
            ? Hex(selectedHex)
            : currentFrameColor;
    }

    private void OnClick()
    {
        battleHUD.SelectSkill(skillIndex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentSkill == null)
            return;

        battleHUD.ShowSkillTooltip(currentSkill, skillIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        battleHUD.HideTooltip();
    }

    private Color Hex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
            return color;

        return Color.white;
    }
}