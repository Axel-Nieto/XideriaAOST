using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One reusable shop view. It never owns currency or purchased state.</summary>
public sealed class UpgradeShopItemUI : MonoBehaviour
{
    [SerializeField] private Image upgradeIcon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image costCurrencyIcon;
    [SerializeField] private Sprite goldCurrencySprite;
    [SerializeField] private Sprite fragmentsCurrencySprite;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseButtonText;

    public UpgradeDefinition Definition { get; private set; }
    private Action<UpgradeDefinition> purchaseRequested;
    private Button listeningButton;
    private static readonly Color AcquiredColor = new Color32(66, 107, 80, 255);
    private static readonly Color InsufficientCurrencyColor = new Color32(153, 45, 45, 255);
    private Color? originalDisabledColor;
    private Color? originalButtonTextColor;
    private Vector4 originalButtonTextMargin;
    private GameObject acquiredCheck;

    // Presentation filter, not purchase authorization. TryPurchaseUpgrade remains
    // responsible for validating every actual transaction. Extend this filter only
    // when the corresponding upgrade is supported by the purchase system.
    public static bool CanDisplay(UpgradeDefinition definition) =>
        definition != null && definition.Cost > 0 && definition.IsSupported;

    private void OnEnable() => BindButton();
    private void OnDisable() => UnbindButton();
    private void OnDestroy() => UnbindButton();

    public void Bind(UpgradeDefinition definition, Action<UpgradeDefinition> onPurchaseRequested)
    {
        Definition = definition;
        purchaseRequested = onPurchaseRequested;
        if (isActiveAndEnabled)
            BindButton();
    }

    public void Refresh(PlayerSessionData session, bool purchaseInProgress = false)
    {
        bool valid = CanDisplay(Definition);
        if (upgradeIcon != null)
        {
            upgradeIcon.sprite = valid ? Definition.Icon : null;
            upgradeIcon.enabled = upgradeIcon.sprite != null;
            upgradeIcon.preserveAspect = true;
            upgradeIcon.raycastTarget = false;
        }
        if (nameText != null)
            nameText.text = valid ? (string.IsNullOrWhiteSpace(Definition.DisplayName) ? Definition.Id : Definition.DisplayName) : "";
        if (descriptionText != null)
            descriptionText.text = valid ? Definition.Description : "";
        if (costText != null)
            costText.text = valid ? Definition.Cost.ToString(CultureInfo.InvariantCulture) : "";
        if (costCurrencyIcon != null)
        {
            costCurrencyIcon.sprite = valid ? (Definition.Currency == RunCurrency.Gold ? goldCurrencySprite : fragmentsCurrencySprite) : null;
            costCurrencyIcon.enabled = costCurrencyIcon.sprite != null;
        }

        string label;
        bool available = false;
        bool acquired = false;
        Color? stateColor = null;
        if (!valid)
            label = "Mejora no disponible";
        else if (session == null)
            label = "Sesión no disponible";
        else if (session.HasUpgrade(Definition.Id))
        {
            label = "Adquirida";
            acquired = true;
            stateColor = AcquiredColor;
        }
        else if (session.GetBalance(Definition.Currency) < Definition.Cost)
        {
            label = Definition.Currency == RunCurrency.Gold ? "Oro insuficiente" : "Fragmentos insuficientes";
            stateColor = InsufficientCurrencyColor;
        }
        else
        {
            label = "Comprar";
            available = true;
        }

        ApplyPurchaseAppearance(stateColor, acquired);
        if (purchaseButton != null)
            purchaseButton.interactable = available && !purchaseInProgress && purchaseRequested != null;
        if (purchaseButtonText != null)
            purchaseButtonText.text = label;
    }

    public void Clear()
    {
        Definition = null;
        purchaseRequested = null;
        UnbindButton();
        ApplyPurchaseAppearance(null, false);
        if (purchaseButton != null)
            purchaseButton.interactable = false;
    }

    private void ApplyPurchaseAppearance(Color? stateColor, bool acquired)
    {
        if (purchaseButton != null)
        {
            ColorBlock colors = purchaseButton.colors;
            originalDisabledColor ??= colors.disabledColor;
            // Both states remain disabled; give each its own tint through the
            // Button transition so hover/selection cannot overwrite the color.
            colors.disabledColor = stateColor ?? originalDisabledColor.Value;
            purchaseButton.colors = colors;
        }

        if (purchaseButtonText == null)
            return;
        if (!originalButtonTextColor.HasValue)
        {
            originalButtonTextColor = purchaseButtonText.color;
            originalButtonTextMargin = purchaseButtonText.margin;
        }
        purchaseButtonText.color = stateColor.HasValue ? Color.white : originalButtonTextColor.Value;
        purchaseButtonText.margin = originalButtonTextMargin + (acquired ? new Vector4(20f, 0f, 0f, 0f) : Vector4.zero);

        if (acquired && acquiredCheck == null)
        {
            // The shop font has no checkmark glyph. Draw it with UI geometry
            // instead of depending on a font fallback or a new sprite asset.
            acquiredCheck = new GameObject("AcquiredCheck", typeof(RectTransform));
            acquiredCheck.layer = purchaseButtonText.gameObject.layer;
            var rect = (RectTransform)acquiredCheck.transform;
            rect.SetParent(purchaseButtonText.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(originalButtonTextMargin.x + 4f, 0f);
            rect.sizeDelta = new Vector2(16f, 16f);
            AddCheckStroke(rect, 7f, 45f);
            AddCheckStroke(rect, 14f, -40f);
        }
        if (acquiredCheck != null)
            acquiredCheck.SetActive(acquired);
    }

    private static void AddCheckStroke(RectTransform parent, float length, float angle)
    {
        var stroke = new GameObject("Stroke", typeof(RectTransform), typeof(Image));
        stroke.layer = parent.gameObject.layer;
        var rect = (RectTransform)stroke.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(5f, 2f);
        rect.sizeDelta = new Vector2(2.5f, length);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = stroke.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private void BindButton()
    {
        UnbindButton();
        listeningButton = purchaseButton;
        if (listeningButton != null)
        {
            listeningButton.onClick.AddListener(RequestPurchase);
            if (Definition == null)
                listeningButton.interactable = false;
        }
    }

    private void UnbindButton()
    {
        if (listeningButton != null)
            listeningButton.onClick.RemoveListener(RequestPurchase);
        listeningButton = null;
    }

    private void RequestPurchase()
    {
        if (isActiveAndEnabled && purchaseButton != null && purchaseButton.interactable && CanDisplay(Definition))
            purchaseRequested?.Invoke(Definition);
    }
}
