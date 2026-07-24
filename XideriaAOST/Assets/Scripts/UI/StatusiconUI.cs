using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatusIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text turnText;

    private string title;
    private string description;
    private BattleHUD battleHUD;

    public void Setup(
        Sprite icon,
        string title,
        string description,
        int remainingTurns,
        BattleHUD hud)
    {
        this.title = title;
        this.description = description;
        battleHUD = hud;

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;

        if (remainingTurns > 0)
        {
            turnText.gameObject.SetActive(true);
            turnText.text = remainingTurns.ToString();
        }
        else
        {
            turnText.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        battleHUD.ShowGenericTooltip(title, description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        battleHUD.HideTooltip();
    }
}