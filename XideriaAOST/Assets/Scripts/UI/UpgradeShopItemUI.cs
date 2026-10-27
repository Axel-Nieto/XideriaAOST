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
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseButtonText;

    public UpgradeDefinition Definition { get; private set; }
    private Action<UpgradeDefinition> purchaseRequested;
    private Button listeningButton;

    // Presentation filter, not purchase authorization. TryPurchaseUpgrade remains
    // responsible for validating every actual transaction. Extend this filter only
    // when the corresponding upgrade is supported by the purchase system.
    public static bool CanDisplay(UpgradeDefinition definition) =>
        definition != null && definition.FragmentCost > 0 &&
        definition.Type == UpgradeType.VitalImpulse && definition.Id == UpgradeDefinition.VitalImpulseId;

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
            costText.text = valid ? Definition.FragmentCost.ToString(CultureInfo.InvariantCulture) : "";

        string label;
        bool available = false;
        if (!valid)
            label = "Mejora no disponible";
        else if (session == null)
            label = "Sesión no disponible";
        else if (session.HasUpgrade(Definition.Id))
            label = "Adquirida";
        else if (session.XideriaFragments < Definition.FragmentCost)
            label = "Fragmentos insuficientes";
        else
        {
            label = "Comprar";
            available = true;
        }

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
        if (purchaseButton != null)
            purchaseButton.interactable = false;
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
