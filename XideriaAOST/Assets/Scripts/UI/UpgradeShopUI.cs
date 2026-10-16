using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Event-driven shop presentation. Attach to a host outside panelRoot so closing
/// the panel does not disable its controller. Input/modal integration is separate.
/// </summary>
public sealed class UpgradeShopUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text fragmentsBalanceText;
    [SerializeField] private Button closeButton;

    [Header("Items")]
    [SerializeField] private Transform itemsContainer;
    [SerializeField] private UpgradeShopItemUI itemPrefab;
    [SerializeField] private List<UpgradeDefinition> upgrades = new();

    public bool IsOpen { get; private set; }
    public event System.Action<bool> OpenStateChanged;
    private PlayerSessionData session;
    private Button listeningCloseButton;
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
        IsOpen = true;
        panelRoot.SetActive(true);
        Unsubscribe();
        listeningCloseButton = closeButton;
        if (listeningCloseButton != null)
            listeningCloseButton.onClick.AddListener(Close);
        // The session may appear later or be replaced while this panel is open.
        PlayerSessionData.InstanceChanged += HandleSessionChanged;
        BindSession(PlayerSessionData.Instance);
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
            fragmentsBalanceText.text = (session != null ? session.XideriaFragments : 0).ToString(CultureInfo.InvariantCulture);
        SynchronizeItems();
    }

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
        if (currency == RunCurrency.XideriaFragments)
            Refresh();
    }

    private void SynchronizeItems()
    {
        if (itemsContainer == null || itemPrefab == null)
            return;

        var configuredIds = new HashSet<string>();
        int siblingIndex = 0;
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

                item.gameObject.SetActive(true);
                item.Bind(definition, Purchase);
                item.transform.SetSiblingIndex(siblingIndex++);
                item.Refresh(session, purchaseInProgress);
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
    }

    private void Purchase(UpgradeDefinition definition)
    {
        if (!IsOpen || !isActiveAndEnabled || purchaseInProgress || session == null ||
            !UpgradeShopItemUI.CanDisplay(definition) ||
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
