using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class UpgradeShopUITests : UpgradeIntegrationFixture
{
    private GameObject host;
    private GameObject panel;
    private Transform container;
    private UpgradeShopUI shop;
    private TMP_Text balanceText;
    private Button closeButton;
    private Button previousSectionButton;
    private Button nextSectionButton;
    private TMP_Text sectionTitleText;
    private TMP_Text emptySectionText;
    private UpgradeShopItemUI template;

    [SetUp]
    public void CreateShop()
    {
        Set(definition, "displayName", "Impulso Vital");
        Set(definition, "description", "Recupera hasta 2 puntos de energía.");
        host = NewUI("UpgradeShop", root.transform);
        host.SetActive(false);
        panel = NewUI("Panel", host.transform);
        balanceText = NewText("FragmentsBalance", panel.transform);
        closeButton = NewUI("CloseButton", panel.transform).AddComponent<Button>();
        previousSectionButton = NewUI("PreviousSectionButton", panel.transform).AddComponent<Button>();
        nextSectionButton = NewUI("NextSectionButton", panel.transform).AddComponent<Button>();
        sectionTitleText = NewText("SectionTitle", panel.transform);
        emptySectionText = NewText("EmptySection", panel.transform);
        container = NewUI("Content", panel.transform).transform;
        var templateObject = NewUI("Item template", root.transform);
        templateObject.SetActive(false);
        template = templateObject.AddComponent<UpgradeShopItemUI>();
        Set(template, "upgradeIcon", NewUI("Icon", template.transform).AddComponent<Image>());
        Set(template, "nameText", NewText("Name", template.transform));
        Set(template, "descriptionText", NewText("Description", template.transform));
        Set(template, "costText", NewText("Cost", template.transform));
        Button purchaseButton = NewUI("PurchaseButton", template.transform).AddComponent<Button>();
        Set(template, "purchaseButton", purchaseButton);
        Set(template, "purchaseButtonText", NewText("Label", purchaseButton.transform));
        shop = host.AddComponent<UpgradeShopUI>();
        Set(shop, "panelRoot", panel);
        Set(shop, "fragmentsBalanceText", balanceText);
        Set(shop, "closeButton", closeButton);
        Set(shop, "previousSectionButton", previousSectionButton);
        Set(shop, "nextSectionButton", nextSectionButton);
        Set(shop, "sectionTitleText", sectionTitleText);
        Set(shop, "emptySectionText", emptySectionText);
        Set(shop, "itemsContainer", container);
        Set(shop, "itemPrefab", template);
        Set(shop, "upgrades", new List<UpgradeDefinition> { definition });
        host.SetActive(true);
        // Explicit lifecycle calls keep EditMode setup independent of Awake ordering.
        Call(shop, "Awake");
    }

    [TearDown]
    public void DestroyShop()
    {
        if (shop != null)
        {
            shop.Close();
            Call(shop, "OnDestroy");
        }
    }

    [Test]
    public void ArrowsVisitAllSectionsInOrderAndWrapInBothDirections()
    {
        shop.Open();
        Assert.That(sectionTitleText.text, Is.EqualTo("Sir Tete"));
        Assert.That(emptySectionText.gameObject.activeSelf, Is.False);
        UpgradeShopItemUI original = Items().Single();
        foreach (string section in new[] { "Fuego", "Agua", "Tierra", "Sir Tete" })
        {
            nextSectionButton.onClick.Invoke();
            Assert.That(sectionTitleText.text, Is.EqualTo(section));
            Assert.That(original.gameObject.activeSelf, Is.EqualTo(section == "Sir Tete"));
            Assert.That(emptySectionText.gameObject.activeSelf, Is.EqualTo(section != "Sir Tete"));
            Assert.That(Items().Single(), Is.SameAs(original));
        }
        foreach (string section in new[] { "Tierra", "Agua", "Fuego", "Sir Tete" })
        {
            previousSectionButton.onClick.Invoke();
            Assert.That(sectionTitleText.text, Is.EqualTo(section));
        }
        Assert.That(session.RunState.PurchasedUpgradeIds, Is.Empty);
    }

    [Test]
    public void HiddenOfferCannotBePurchasedAndReturningReadsCurrentSessionState()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        session.TryAddCurrency(RunCurrency.Gold, 25);
        shop.Open();
        var item = Items().Single();
        nextSectionButton.onClick.Invoke();
        // Even a retained callback must not buy an offer from a different section.
        var request = Get<Action<UpgradeDefinition>>(item, "purchaseRequested");
        request.Invoke(definition);
        BuyButton().onClick.Invoke();
        Assert.That(session.XideriaFragments, Is.EqualTo(10));
        Assert.That(session.HasUpgrade(definition.Id), Is.False);
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
        Assert.That(sectionTitleText.text, Is.EqualTo("Fuego"));
        Assert.That(balanceText.text, Is.EqualTo("25"));
        previousSectionButton.onClick.Invoke();
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Adquirida"));
        Assert.That(BuyButton().interactable, Is.False);
    }

    [Test]
    public void FireSectionShowsTwoGoldUpgradesAndUpdatesPurchasesWithoutSpendingFragments()
    {
        var fireball = ScriptableObject.CreateInstance<UpgradeDefinition>();
        var dash = ScriptableObject.CreateInstance<UpgradeDefinition>();
        try
        {
            Set(fireball, "upgradeType", UpgradeType.FireballCooldown);
            Set(fireball, "upgradeId", UpgradeDefinition.FireballCooldownId);
            Set(fireball, "goldCost", 20);
            Set(dash, "upgradeType", UpgradeType.FlameDashCooldown);
            Set(dash, "upgradeId", UpgradeDefinition.FlameDashCooldownId);
            Set(dash, "goldCost", 20);
            Set(shop, "upgrades", new List<UpgradeDefinition> { definition, fireball, dash });
            TMP_Text currencyLabel = NewText("Currency label", panel.transform);
            Set(shop, "currencyLabelText", currencyLabel);
            session.TryAddCurrency(RunCurrency.XideriaFragments, 50);
            session.TryAddCurrency(RunCurrency.Gold, 19);
            shop.Open();
            Assert.That(balanceText.text, Is.EqualTo("50"));
            nextSectionButton.onClick.Invoke();
            Assert.That(shop.CurrentCurrency, Is.EqualTo(RunCurrency.Gold));
            Assert.That(currencyLabel.text, Is.EqualTo("Oro"));
            Assert.That(balanceText.text, Is.EqualTo("19"));
            UpgradeShopItemUI[] visible = Items().Where(item => item.gameObject.activeSelf).ToArray();
            CollectionAssert.AreEquivalent(new[] { fireball, dash }, visible.Select(item => item.Definition));
            var item = visible.Single(view => view.Definition == fireball);
            Assert.That(Get<TMP_Text>(item, "costText").text, Is.EqualTo("20"));
            Assert.That(Get<TMP_Text>(item, "purchaseButtonText").text, Is.EqualTo("Oro insuficiente"));
            session.TryAddCurrency(RunCurrency.Gold, 1);
            Assert.That(balanceText.text, Is.EqualTo("20"));
            Get<Button>(item, "purchaseButton").onClick.Invoke();
            Get<Button>(item, "purchaseButton").onClick.Invoke();
            Assert.That(balanceText.text, Is.EqualTo("0"));
            Assert.That(session.XideriaFragments, Is.EqualTo(50));
            Assert.That(session.HasUpgrade(fireball.Id), Is.True);
            Assert.That(session.HasUpgrade(dash.Id), Is.False);
            Assert.That(Get<TMP_Text>(item, "purchaseButtonText").text, Is.EqualTo("Adquirida"));
            previousSectionButton.onClick.Invoke();
            Assert.That(balanceText.text, Is.EqualTo("50"));
            Assert.That(currencyLabel.text, Is.EqualTo("Fragmentos de Xideria"));
        }
        finally
        {
            shop.Close();
            UnityEngine.Object.DestroyImmediate(fireball);
            UnityEngine.Object.DestroyImmediate(dash);
        }
    }

    [Test]
    public void RepeatedOpenKeepsSectionAndSingleArrowListenerButReopeningStartsAtSirTete()
    {
        shop.Open();
        nextSectionButton.onClick.Invoke();
        for (int i = 0; i < 5; i++)
        {
            shop.Open();
            shop.Refresh();
            Assert.That(shop.CurrentSection, Is.EqualTo(UpgradeShopSection.Fire));
        }
        nextSectionButton.onClick.Invoke();
        Assert.That(shop.CurrentSection, Is.EqualTo(UpgradeShopSection.Water));
        shop.Close();
        nextSectionButton.onClick.Invoke();
        previousSectionButton.onClick.Invoke();
        Assert.That(shop.CurrentSection, Is.EqualTo(UpgradeShopSection.Water));
        shop.Open();
        Assert.That(shop.CurrentSection, Is.EqualTo(UpgradeShopSection.SirTete));
        Assert.That(Items().Single().gameObject.activeSelf, Is.True);
    }

    [Test]
    public void StartsClosedAndOpenCloseOnlyChangePresentation()
    {
        Assert.That(shop.IsOpen, Is.False);
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(Items(), Is.Empty);
        shop.Open();
        Assert.That(shop.IsOpen, Is.True);
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(Items().Length, Is.EqualTo(1));
        closeButton.onClick.Invoke();
        Assert.That(shop.IsOpen, Is.False);
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(session.RunState.PurchasedUpgradeIds, Is.Empty);
    }

    [Test]
    public void OpeningReadsExistingBalancesAndRendersDefinition()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        session.TryAddCurrency(RunCurrency.Gold, 25);
        shop.Open();
        Assert.That(balanceText.text, Is.EqualTo("10"));
        Assert.That(ItemText("nameText").text, Is.EqualTo("Impulso Vital"));
        Assert.That(ItemText("descriptionText").text, Is.EqualTo(definition.Description));
        Assert.That(ItemText("costText").text, Is.EqualTo("3"));
        Assert.That(BuyButton().interactable, Is.True);
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Comprar"));
    }

    [Test]
    public void InsufficientFragmentsDisablePurchaseAndBalanceEventsEnableIt()
    {
        shop.Open();
        Assert.That(balanceText.text, Is.EqualTo("0"));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Fragmentos insuficientes"));
        Assert.That(BuyButton().interactable, Is.False);
        BuyButton().onClick.Invoke();
        Assert.That(session.HasUpgrade(definition.Id), Is.False);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 2);
        Assert.That(BuyButton().interactable, Is.False);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 1);
        Assert.That(balanceText.text, Is.EqualTo("3"));
        Assert.That(BuyButton().interactable, Is.True);
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Comprar"));
    }

    [Test]
    public void RapidRepeatedClicksPurchaseOnceUpdateImmediatelyAndNeverSpendGold()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        session.TryAddCurrency(RunCurrency.Gold, 12);
        shop.Open();
        Button button = BuyButton();
        for (int click = 0; click < 8; click++)
            button.onClick.Invoke();
        Assert.That(session.XideriaFragments, Is.EqualTo(7));
        Assert.That(session.Gold, Is.EqualTo(12));
        Assert.That(session.RunState.PurchasedUpgradeIds.Count, Is.EqualTo(1));
        Assert.That(balanceText.text, Is.EqualTo("7"));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Adquirida"));
        Assert.That(button.interactable, Is.False);
    }

    [Test]
    public void ReentrantClickDuringPurchaseCannotStartAnotherPurchase()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        shop.Open();
        session.BalanceChanged += (_, _) => BuyButton().onClick.Invoke();
        BuyButton().onClick.Invoke();
        Assert.That(session.XideriaFragments, Is.EqualTo(7));
        Assert.That(session.RunState.PurchasedUpgradeIds.Count, Is.EqualTo(1));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Adquirida"));
    }

    [Test]
    public void ExternalPurchaseUpdatesShopAndAbandonRestoresLockedState()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 3);
        shop.Open();
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
        Assert.That(balanceText.text, Is.EqualTo("0"));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Adquirida"));
        session.AbandonRun();
        Assert.That(balanceText.text, Is.EqualTo("0"));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Fragmentos insuficientes"));
        Assert.That(BuyButton().interactable, Is.False);
    }

    [Test]
    public void FailedPurchaseAfterStaleVisualRereadsStateWithoutSpending()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        shop.Open();
        Set(definition, "fragmentCost", 11);
        // The view still says Comprar; the transaction must independently reject it.
        BuyButton().onClick.Invoke();
        Assert.That(session.XideriaFragments, Is.EqualTo(10));
        Assert.That(session.HasUpgrade(definition.Id), Is.False);
        Assert.That(ItemText("costText").text, Is.EqualTo("11"));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Fragmentos insuficientes"));
    }

    [Test]
    public void ReopeningAndRefreshReuseViewsAndDoNotDuplicateSubscriptions()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        shop.Open();
        UpgradeShopItemUI original = Items().Single();
        for (int i = 0; i < 6; i++)
        {
            shop.Open();
            shop.Refresh();
            Assert.That(Items().Single(), Is.SameAs(original));
            Assert.That(ListenerCount(session, "BalanceChanged", shop), Is.EqualTo(1));
            Assert.That(ListenerCount(session, "UpgradesChanged", shop), Is.EqualTo(1));
            Assert.That(ListenerCount(null, "InstanceChanged", shop), Is.EqualTo(1));
            shop.Close();
            AssertNoListeners();
        }
        shop.Open();
        BuyButton().onClick.Invoke();
        Assert.That(session.XideriaFragments, Is.EqualTo(7));
    }

    [Test]
    public void RebindingItemAddsOnlyOneButtonListenerAndClearRemovesItsCallback()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        shop.Open();
        var item = Items().Single();
        int requests = 0;
        Action<UpgradeDefinition> onPurchase = configured =>
        {
            Assert.That(configured, Is.SameAs(definition));
            requests++;
        };
        for (int i = 0; i < 6; i++)
        {
            item.Bind(definition, onPurchase);
            Call(item, "OnEnable");
            item.Refresh(session);
        }
        BuyButton().onClick.Invoke();
        Assert.That(requests, Is.EqualTo(1));
        item.Clear();
        BuyButton().onClick.Invoke();
        Assert.That(requests, Is.EqualTo(1));
        Assert.That(session.RunState.PurchasedUpgradeIds, Is.Empty);
    }

    [Test]
    public void ClosedShopIgnoresClicksAndReopeningReadsChangesMadeWhileClosed()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        shop.Open();
        Button button = BuyButton();
        shop.Close();
        button.onClick.Invoke();
        Assert.That(session.XideriaFragments, Is.EqualTo(10));
        session.TryPurchaseUpgrade(definition, out _);
        shop.Open();
        Assert.That(balanceText.text, Is.EqualTo("7"));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Adquirida"));
    }

    [Test]
    public void MissingThenLateSessionArrivalRebindsWithoutDuplicatingEvents()
    {
        SetSession(null);
        shop.Open();
        Assert.That(balanceText.text, Is.EqualTo("0"));
        Assert.That(BuyButton().interactable, Is.False);
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Sesión no disponible"));
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        SetSession(session);
        RaiseInstanceChanged(session);
        RaiseInstanceChanged(session);
        Assert.That(balanceText.text, Is.EqualTo("10"));
        Assert.That(BuyButton().interactable, Is.True);
        Assert.That(ListenerCount(session, "BalanceChanged", shop), Is.EqualTo(1));
        Assert.That(ListenerCount(session, "UpgradesChanged", shop), Is.EqualTo(1));
        RaiseInstanceChanged(null);
        Assert.That(balanceText.text, Is.EqualTo("0"));
        Assert.That(BuyButton().interactable, Is.False);
        Assert.That(ListenerCount(session, "BalanceChanged", shop), Is.Zero);
        Assert.That(ListenerCount(session, "UpgradesChanged", shop), Is.Zero);
    }

    [Test]
    public void DisablingAndDestroyingControllerRemovesAllListeners()
    {
        shop.Open();
        host.SetActive(false);
        Call(shop, "OnDisable");
        Assert.That(shop.IsOpen, Is.False);
        Assert.That(panel.activeSelf, Is.False);
        AssertNoListeners();
        host.SetActive(true);
        shop.Open();
        var destroyedShop = shop;
        // EditMode does not guarantee native lifecycle callbacks for this host.
        Call(shop, "OnDestroy");
        UnityEngine.Object.DestroyImmediate(host);
        Assert.That(ListenerCount(session, "BalanceChanged", destroyedShop), Is.Zero);
        Assert.That(ListenerCount(session, "UpgradesChanged", destroyedShop), Is.Zero);
        Assert.That(ListenerCount(null, "InstanceChanged", destroyedShop), Is.Zero);
        Assert.DoesNotThrow(() => session.TryAddCurrency(RunCurrency.XideriaFragments, 1));
    }

    [Test]
    public void RecreatedControllerReadsOwnershipAndBalanceFromTheSameSession()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        shop.Open();
        BuyButton().onClick.Invoke();
        shop.Close();
        Call(shop, "OnDestroy");
        UnityEngine.Object.DestroyImmediate(host);
        CreateShop();
        shop.Open();
        Assert.That(balanceText.text, Is.EqualTo("7"));
        Assert.That(ItemText("purchaseButtonText").text, Is.EqualTo("Adquirida"));
    }

    [Test]
    public void ConfiguredListSkipsNullUnsupportedInvalidAndDuplicateStableIds()
    {
        var unsupported = ScriptableObject.CreateInstance<UpgradeDefinition>();
        var sameId = ScriptableObject.CreateInstance<UpgradeDefinition>();
        try
        {
            Set(unsupported, "upgradeId", "sir_tete.future_stat");
            Set(unsupported, "fragmentCost", 1);
            Set(unsupported, "upgradeType", UpgradeType.StatIncrease);
            Set(sameId, "upgradeId", definition.Id);
            Set(sameId, "fragmentCost", 4);
            Set(shop, "upgrades", new List<UpgradeDefinition> { null, unsupported, definition, definition, sameId });
            shop.Open();
            shop.Open();
            shop.Refresh();
            Assert.That(Items().Length, Is.EqualTo(1));
            Assert.That(Items().Single().Definition, Is.SameAs(definition));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unsupported);
            UnityEngine.Object.DestroyImmediate(sameId);
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void InvalidPriceDefinitionsAreNotDisplayed(int cost)
    {
        Set(definition, "fragmentCost", cost);
        shop.Open();
        Assert.That(Items(), Is.Empty);
    }

    [Test]
    public void NullListAndMissingOptionalVisualReferencesDoNotThrow()
    {
        Set(shop, "upgrades", null);
        Set(shop, "fragmentsBalanceText", null);
        Set(shop, "closeButton", null);
        Assert.DoesNotThrow(shop.Open);
        Assert.That(Items(), Is.Empty);
        foreach (string field in new[] { "upgradeIcon", "nameText", "descriptionText", "costText", "purchaseButton", "purchaseButtonText" })
            Set(template, field, null);
        Set(shop, "upgrades", new List<UpgradeDefinition> { definition });
        Assert.DoesNotThrow(shop.Refresh);
        Assert.DoesNotThrow(shop.Close);
    }

    [Test]
    public void MissingRequiredLayoutFailsClosedWithoutNullReference()
    {
        Set(shop, "itemsContainer", null);
        LogAssert.Expect(LogType.Warning, "[UPGRADE SHOP] Asignar un Panel Root separado del controlador, su Items Container y un Item Prefab.");
        Assert.DoesNotThrow(shop.Open);
        Assert.That(shop.IsOpen, Is.False);
        AssertNoListeners();
    }

    [Test]
    public void WrongPanelPlacementCannotDisableItsOwnController()
    {
        Set(shop, "panelRoot", host);
        Call(shop, "Awake");
        Assert.That(host.activeSelf, Is.True);
        LogAssert.Expect(LogType.Warning, "[UPGRADE SHOP] Asignar un Panel Root separado del controlador, su Items Container y un Item Prefab.");
        shop.Open();
        Assert.That(shop.IsOpen, Is.False);
        AssertNoListeners();
    }

    private UpgradeShopItemUI[] Items() => container.GetComponentsInChildren<UpgradeShopItemUI>(true);
    private Button BuyButton() => Get<Button>(Items().Single(), "purchaseButton");
    private TMP_Text ItemText(string field) => Get<TMP_Text>(Items().Single(), field);
    private static T Get<T>(object owner, string field) => (T)owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
    private static TMP_Text NewText(string name, Transform parent) => NewUI(name, parent).AddComponent<TextMeshProUGUI>();
    private void AssertNoListeners()
    {
        Assert.That(ListenerCount(session, "BalanceChanged", shop), Is.Zero);
        Assert.That(ListenerCount(session, "UpgradesChanged", shop), Is.Zero);
        Assert.That(ListenerCount(null, "InstanceChanged", shop), Is.Zero);
    }
    private static int ListenerCount(PlayerSessionData owner, string field, object target)
    {
        var listeners = GetEvent(owner, field);
        return listeners == null ? 0 : listeners.GetInvocationList().Count(listener => ReferenceEquals(listener.Target, target));
    }
    private static Delegate GetEvent(PlayerSessionData owner, string field) => (Delegate)typeof(PlayerSessionData)
        .GetField(field, BindingFlags.NonPublic | (ReferenceEquals(owner, null) ? BindingFlags.Static : BindingFlags.Instance)).GetValue(owner);
    private static void SetSession(PlayerSessionData value) => typeof(PlayerSessionData)
        .GetProperty(nameof(PlayerSessionData.Instance)).SetValue(null, value);
    private static void RaiseInstanceChanged(PlayerSessionData value) =>
        ((Action<PlayerSessionData>)GetEvent(null, "InstanceChanged"))?.Invoke(value);
}
