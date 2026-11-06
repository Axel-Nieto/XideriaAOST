using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UpgradeShopSection
{
    SirTete,
    Fire,
    Water,
    Earth
}

/// <summary>
/// Event-driven shop presentation. Attach to a host outside panelRoot so closing
/// the panel does not disable its controller. Input/modal integration is separate.
/// </summary>
public sealed class UpgradeShopUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text fragmentsBalanceText;
    [SerializeField] private TMP_Text currencyLabelText;
    [SerializeField] private Image balanceCurrencyIcon;
    [SerializeField] private Sprite goldCurrencySprite;
    [SerializeField] private Sprite fragmentsCurrencySprite;
    [SerializeField] private Button closeButton;

    [Header("Sections")]
    [SerializeField] private TMP_Text sectionTitleText;
    [SerializeField] private Image sectionHelmetIcon;
    [SerializeField] private Sprite sirTeteHelmetSprite;
    [SerializeField] private Sprite fireHelmetSprite;
    [SerializeField] private Sprite waterHelmetSprite;
    [SerializeField] private Sprite earthHelmetSprite;
    [SerializeField] private TMP_Text emptySectionText;
    [SerializeField] private Button previousSectionButton;
    [SerializeField] private Button nextSectionButton;
    [SerializeField] private ScrollRect itemsScrollRect;

    [Header("Items")]
    [SerializeField] private Transform itemsContainer;
    [SerializeField] private UpgradeShopItemUI itemPrefab;
    [SerializeField] private List<UpgradeDefinition> upgrades = new();

    public bool IsOpen { get; private set; }
    public UpgradeShopSection CurrentSection { get; private set; } = UpgradeShopSection.SirTete;
    public RunCurrency CurrentCurrency => CurrentSection == UpgradeShopSection.Fire
        ? RunCurrency.Gold : RunCurrency.XideriaFragments;
    public string CurrentSectionName => CurrentSection switch
    {
        UpgradeShopSection.Fire => "Fuego",
        UpgradeShopSection.Water => "Agua",
        UpgradeShopSection.Earth => "Tierra",
        _ => "Sir Tete"
    };
    public event System.Action<bool> OpenStateChanged;
    private PlayerSessionData session;
    private Button listeningCloseButton;
    private Button listeningPreviousSectionButton;
    private Button listeningNextSectionButton;
    private bool purchaseInProgress;
    private readonly Dictionary<string, UpgradeShopItemUI> items = new();

    private void Awake()
    {
        IsOpen = false;
        if (HasSeparatePanel())
            panelRoot.SetActive(false);
    }

    private void OnDisable() => Close();

    private void OnDestroy()
    {
        Close();
        foreach (UpgradeShopItemUI item in items.Values)
            if (item != null)
                item.Clear();
        items.Clear();
    }

    public void Open()
    {
        if (!isActiveAndEnabled)
            return;
        if (!HasSeparatePanel() || itemsContainer == null || itemPrefab == null ||
            !itemsContainer.IsChildOf(panelRoot.transform))
        {
            Debug.LogWarning("[UPGRADE SHOP] Asignar un Panel Root separado del controlador, su Items Container y un Item Prefab.", this);
            return;
        }

        bool wasOpen = IsOpen;
        if (!wasOpen)
            CurrentSection = UpgradeShopSection.SirTete;
        IsOpen = true;
        panelRoot.SetActive(true);
        Unsubscribe();
        listeningCloseButton = closeButton;
        if (listeningCloseButton != null)
            listeningCloseButton.onClick.AddListener(Close);
        listeningPreviousSectionButton = previousSectionButton;
        listeningNextSectionButton = nextSectionButton;
        if (listeningPreviousSectionButton != null)
            listeningPreviousSectionButton.onClick.AddListener(ShowPreviousSection);
        if (listeningNextSectionButton != null)
            listeningNextSectionButton.onClick.AddListener(ShowNextSection);
        // The session may appear later or be replaced while this panel is open.
        PlayerSessionData.InstanceChanged += HandleSessionChanged;
        BindSession(PlayerSessionData.Instance);
        if (!wasOpen)
            ResetScroll();
        if (!wasOpen)
            OpenStateChanged?.Invoke(true);
    }

    public void Close()
    {
        bool wasOpen = IsOpen;
        IsOpen = false;
        Unsubscribe();
        if (HasSeparatePanel())
            panelRoot.SetActive(false);
        if (wasOpen)
            OpenStateChanged?.Invoke(false);
    }

    public void Refresh()
    {
        if (!IsOpen || !isActiveAndEnabled)
            return;

        if (fragmentsBalanceText != null)
            fragmentsBalanceText.text = (session != null ? session.GetBalance(CurrentCurrency) : 0).ToString(CultureInfo.InvariantCulture);
        if (currencyLabelText != null)
            currencyLabelText.text = CurrentCurrency == RunCurrency.Gold ? "Oro" : "Fragmentos de Xideria";
        if (balanceCurrencyIcon != null)
        {
            balanceCurrencyIcon.sprite = CurrentCurrency == RunCurrency.Gold ? goldCurrencySprite : fragmentsCurrencySprite;
            balanceCurrencyIcon.enabled = balanceCurrencyIcon.sprite != null;
        }
        if (sectionTitleText != null)
            sectionTitleText.text = CurrentSectionName;
        if (sectionHelmetIcon != null)
        {
            sectionHelmetIcon.sprite = CurrentSection switch
            {
                UpgradeShopSection.Fire => fireHelmetSprite,
                UpgradeShopSection.Water => waterHelmetSprite,
                UpgradeShopSection.Earth => earthHelmetSprite,
                _ => sirTeteHelmetSprite
            };
            sectionHelmetIcon.enabled = sectionHelmetIcon.sprite != null;
            sectionHelmetIcon.preserveAspect = true;
            sectionHelmetIcon.raycastTarget = false;
        }
        SynchronizeItems();
    }

    public void ShowPreviousSection() => ChangeSection(-1);
    public void ShowNextSection() => ChangeSection(1);

    private void ChangeSection(int direction)
    {
        if (!IsOpen || !isActiveAndEnabled || purchaseInProgress)
            return;

        const int sectionCount = (int)UpgradeShopSection.Earth + 1;
        CurrentSection = (UpgradeShopSection)(((int)CurrentSection + direction + sectionCount) % sectionCount);
        Refresh();
        ResetScroll();
    }

    private void ResetScroll()
    {
        if (itemsScrollRect == null)
            return;
        itemsScrollRect.StopMovement();
        if (itemsContainer is RectTransform content)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        itemsScrollRect.verticalNormalizedPosition = 1f;
    }

    private bool BelongsToCurrentSection(UpgradeDefinition definition) =>
        UpgradeShopItemUI.CanDisplay(definition) && (definition.Currency == RunCurrency.Gold
            ? CurrentSection == UpgradeShopSection.Fire : CurrentSection == UpgradeShopSection.SirTete);

    private bool HasSeparatePanel() => panelRoot != null && panelRoot != gameObject &&
        !transform.IsChildOf(panelRoot.transform);

    private void HandleSessionChanged(PlayerSessionData newSession) => BindSession(newSession);

    private void BindSession(PlayerSessionData newSession)
    {
        UnbindSession();
        session = newSession;
        if (session != null)
        {
            session.BalanceChanged += HandleBalanceChanged;
            session.UpgradesChanged += Refresh;
        }
        Refresh();
    }

    private void HandleBalanceChanged(RunCurrency currency, int newBalance)
    {
        if (currency == CurrentCurrency)
            Refresh();
    }

    private void SynchronizeItems()
    {
        if (itemsContainer == null || itemPrefab == null)
            return;

        var configuredIds = new HashSet<string>();
        int siblingIndex = 0;
        int visibleCount = 0;
        if (upgrades != null)
            foreach (UpgradeDefinition definition in upgrades)
            {
                if (!UpgradeShopItemUI.CanDisplay(definition) || !configuredIds.Add(definition.Id))
                    continue;

                if (!items.TryGetValue(definition.Id, out UpgradeShopItemUI item) || item == null)
                {
                    item = Instantiate(itemPrefab, itemsContainer, false);
                    items[definition.Id] = item;
                }

                bool visible = BelongsToCurrentSection(definition);
                item.gameObject.SetActive(visible);
                item.Bind(definition, Purchase);
                item.transform.SetSiblingIndex(siblingIndex++);
                item.Refresh(session, purchaseInProgress);
                if (visible)
                    visibleCount++;
            }

        var obsoleteIds = new List<string>();
        foreach (var entry in items)
            if (!configuredIds.Contains(entry.Key))
            {
                if (entry.Value != null)
                {
                    entry.Value.Clear();
                    entry.Value.gameObject.SetActive(false);
                    Destroy(entry.Value.gameObject);
                }
                obsoleteIds.Add(entry.Key);
            }
        foreach (string id in obsoleteIds)
            items.Remove(id);

        if (emptySectionText != null)
        {
            emptySectionText.text = "Todav\u00eda no hay habilidades disponibles en esta secci\u00f3n.";
            emptySectionText.gameObject.SetActive(visibleCount == 0);
        }
    }

    private void Purchase(UpgradeDefinition definition)
    {
        if (!IsOpen || !isActiveAndEnabled || purchaseInProgress || session == null ||
            !BelongsToCurrentSection(definition) ||
            !items.TryGetValue(definition.Id, out UpgradeShopItemUI item) || item == null || item.Definition != definition)
            return;

        purchaseInProgress = true;
        try
        {
            // The session owns validation, fragment deduction and activation.
            // Always reread authoritative values, including after a rejected purchase.
            session.TryPurchaseUpgrade(definition, out _);
        }
        finally
        {
            purchaseInProgress = false;
            if (this != null)
                Refresh();
        }
    }

    private void Unsubscribe()
    {
        PlayerSessionData.InstanceChanged -= HandleSessionChanged;
        if (listeningCloseButton != null)
            listeningCloseButton.onClick.RemoveListener(Close);
        listeningCloseButton = null;
        if (listeningPreviousSectionButton != null)
            listeningPreviousSectionButton.onClick.RemoveListener(ShowPreviousSection);
        if (listeningNextSectionButton != null)
            listeningNextSectionButton.onClick.RemoveListener(ShowNextSection);
        listeningPreviousSectionButton = null;
        listeningNextSectionButton = null;
        UnbindSession();
    }

    private void UnbindSession()
    {
        if (!ReferenceEquals(session, null))
        {
            session.BalanceChanged -= HandleBalanceChanged;
            session.UpgradesChanged -= Refresh;
        }
        session = null;
    }
}
