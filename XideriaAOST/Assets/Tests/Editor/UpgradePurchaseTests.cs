using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RunUpgradeStateTests
{
    private RunState run;

    [SetUp] public void SetUp() => run = new RunState();

    [Test]
    public void FreshRunHasNoPurchasedUpgrades()
    {
        Assert.That(run.PurchasedUpgradeIds, Is.Empty);
        Assert.That(run.HasUpgrade(UpgradeDefinition.VitalImpulseId), Is.False);
    }

    [TestCase(7, 3, 4)]
    [TestCase(7, 7, 0)]
    [TestCase(int.MaxValue, int.MaxValue, 0)]
    public void PurchaseCommitsOwnershipAndFragmentsTogetherWithoutTouchingGold(int balance, int cost, int remaining)
    {
        Add(RunCurrency.XideriaFragments, balance);
        Add(RunCurrency.Gold, int.MaxValue);
        Assert.That(Buy(cost), Is.EqualTo(UpgradePurchaseResult.Success));
        Assert.That(run.XideriaFragments, Is.EqualTo(remaining));
        Assert.That(run.Gold, Is.EqualTo(int.MaxValue));
        CollectionAssert.AreEqual(new[] { UpgradeDefinition.VitalImpulseId }, run.PurchasedUpgradeIds);
    }

    [TestCase(0, UpgradePurchaseResult.InvalidCost)]
    [TestCase(-1, UpgradePurchaseResult.InvalidCost)]
    [TestCase(int.MinValue, UpgradePurchaseResult.InvalidCost)]
    [TestCase(8, UpgradePurchaseResult.InsufficientFragments)]
    [TestCase(int.MaxValue, UpgradePurchaseResult.InsufficientFragments)]
    public void FailedPurchaseChangesNeitherBalanceNorOwnership(int cost, UpgradePurchaseResult expected)
    {
        Add(RunCurrency.XideriaFragments, 7);
        Add(RunCurrency.Gold, 12);
        Assert.That(Buy(cost), Is.EqualTo(expected));
        Assert.That(run.XideriaFragments, Is.EqualTo(7));
        Assert.That(run.Gold, Is.EqualTo(12));
        Assert.That(run.PurchasedUpgradeIds, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void InvalidIdentityCannotSpendFragments(string id)
    {
        Add(RunCurrency.XideriaFragments, 7);
        Assert.That(Buy(3, id), Is.EqualTo(UpgradePurchaseResult.InvalidDefinition));
        Assert.That(run.XideriaFragments, Is.EqualTo(7));
        Assert.That(run.PurchasedUpgradeIds, Is.Empty);
    }

    [Test]
    public void DuplicatePurchaseCannotSpendAgainEvenAtAnotherPrice()
    {
        Add(RunCurrency.XideriaFragments, 10);
        Assert.That(Buy(3), Is.EqualTo(UpgradePurchaseResult.Success));
        Assert.That(Buy(1), Is.EqualTo(UpgradePurchaseResult.AlreadyPurchased));
        Assert.That(run.XideriaFragments, Is.EqualTo(7));
        Assert.That(run.PurchasedUpgradeIds.Count, Is.EqualTo(1));
    }

    [Test]
    public void RoomChangesKeepOwnershipAndResetClearsIt()
    {
        Add(RunCurrency.XideriaFragments, 10);
        Add(RunCurrency.Gold, 4);
        Buy(3);
        string runId = run.RunId;
        foreach (string scene in new[] { "Lobby", "Level01", "Level02", "Lobby", "Level01" })
            run.EnterScene(scene);
        Assert.That(run.HasUpgrade(UpgradeDefinition.VitalImpulseId), Is.True);
        Assert.That(run.XideriaFragments, Is.EqualTo(7));
        Assert.That(run.Gold, Is.EqualTo(4));
        Call("Reset");
        Assert.That(run.PurchasedUpgradeIds, Is.Empty);
        Assert.That(run.XideriaFragments, Is.Zero);
        Assert.That(run.Gold, Is.Zero);
        Assert.That(run.RunId, Is.Not.EqualTo(runId));
    }

    [Test]
    public void OwnershipCannotBeMutatedThroughThePublicView()
    {
        var view = (IList<string>)run.PurchasedUpgradeIds;
        Assert.Throws<NotSupportedException>(() => view.Add(UpgradeDefinition.VitalImpulseId));
        Assert.That(run.PurchasedUpgradeIds, Is.Empty);
    }

    [Test]
    public void OwnershipOnlySnapshotCannotReceiveAnotherStartingGrant()
    {
        Add(RunCurrency.XideriaFragments, 3);
        Buy(3);
        Call("InitializeStartingBalances");
        Assert.That(run.XideriaFragments, Is.Zero);
        Assert.That(run.Gold, Is.Zero);
    }

    [Test]
    public void MissingAndDuplicateSnapshotIdsAreNormalizedWithoutDroppingFutureIds()
    {
        typeof(RunState).GetField("purchasedUpgradeIds", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(run, new List<string> { null, " ", UpgradeDefinition.VitalImpulseId, UpgradeDefinition.VitalImpulseId, "future.upgrade" });
        run.OnAfterDeserialize();
        CollectionAssert.AreEqual(new[] { UpgradeDefinition.VitalImpulseId, "future.upgrade" }, run.PurchasedUpgradeIds);
        typeof(RunState).GetField("purchasedUpgradeIds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(run, null);
        run.OnAfterDeserialize();
        Assert.That(run.PurchasedUpgradeIds, Is.Empty);
    }

    private void Add(RunCurrency currency, int amount) => Call("TryAddCurrency", currency, amount);
    private UpgradePurchaseResult Buy(int cost) => Buy(cost, UpgradeDefinition.VitalImpulseId);
    private UpgradePurchaseResult Buy(int cost, string id) => (UpgradePurchaseResult)Call("TryPurchaseUpgrade", id, cost);
    private object Call(string method, params object[] args) => typeof(RunState)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, args);
}

// All native objects live in a disposable preview scene. No project scene or asset is saved.
public abstract class UpgradeIntegrationFixture
{
    protected PlayerSessionData session;
    protected UpgradeDefinition definition;
    protected GameObject root;
    private Scene previewScene;
    private PlayerSessionData previousSession;
    private Action<Character> previousDeaths;
    private Action<Character, int> previousRecovery;

    [SetUp]
    public void SetUp()
    {
        previousSession = PlayerSessionData.Instance;
        previousDeaths = GameEvents.OnCharacterDeathCompleted;
        previousRecovery = GameEvents.OnEnergyRecovered;
        GameEvents.OnCharacterDeathCompleted = null;
        GameEvents.OnEnergyRecovered = null;
        previewScene = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Upgrade integration test");
        SceneManager.MoveGameObjectToScene(root, previewScene);
        session = root.AddComponent<PlayerSessionData>();
        // EditMode need not invoke Awake. Bind explicitly without exercising DontDestroyOnLoad.
        SetSession(session);
        session.AbandonRun();
        definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
        Set(definition, "upgradeId", UpgradeDefinition.VitalImpulseId);
        Set(definition, "fragmentCost", 3);
        Set(definition, "upgradeType", UpgradeType.VitalImpulse);
    }

    [TearDown]
    public void TearDown()
    {
        SetSession(previousSession);
        GameEvents.OnCharacterDeathCompleted = previousDeaths;
        GameEvents.OnEnergyRecovered = previousRecovery;
        UnityEngine.Object.DestroyImmediate(definition);
        if (previewScene.IsValid())
            EditorSceneManager.ClosePreviewScene(previewScene);
    }

    protected static void Set(object owner, string field, object value) => owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    protected static object Call(object owner, string method, params object[] args) => owner.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    private static void SetSession(PlayerSessionData value) => typeof(PlayerSessionData)
        .GetProperty(nameof(PlayerSessionData.Instance)).SetValue(null, value);
}

public class UpgradePurchaseTests : UpgradeIntegrationFixture
{
    [Test]
    public void PurchaseNotifiesCommittedStateOnceAndReentrantPurchaseCannotChargeTwice()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        session.TryAddCurrency(RunCurrency.Gold, 5);
        int balances = 0, upgrades = 0;
        session.BalanceChanged += (currency, total) =>
        {
            balances++;
            Assert.That(currency, Is.EqualTo(RunCurrency.XideriaFragments));
            Assert.That(total, Is.EqualTo(7));
            Assert.That(session.HasUpgrade(definition.Id), Is.True);
            Assert.That(session.TryPurchaseUpgrade(definition, out var duplicate), Is.False);
            Assert.That(duplicate, Is.EqualTo(UpgradePurchaseResult.AlreadyPurchased));
        };
        session.UpgradesChanged += () => upgrades++;
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.True);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.Success));
        Assert.That(session.XideriaFragments, Is.EqualTo(7));
        Assert.That(session.Gold, Is.EqualTo(5));
        Assert.That(balances, Is.EqualTo(1));
        Assert.That(upgrades, Is.EqualTo(1));
    }

    [TestCase(0, UpgradePurchaseResult.InvalidCost)]
    [TestCase(-1, UpgradePurchaseResult.InvalidCost)]
    [TestCase(int.MinValue, UpgradePurchaseResult.InvalidCost)]
    [TestCase(11, UpgradePurchaseResult.InsufficientFragments)]
    public void RejectedPurchaseDoesNotNotifyOrMutate(int cost, UpgradePurchaseResult expected)
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        session.TryAddCurrency(RunCurrency.Gold, 20);
        Set(definition, "fragmentCost", cost);
        int notifications = 0;
        session.BalanceChanged += (_, _) => notifications++;
        session.UpgradesChanged += () => notifications++;
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.False);
        Assert.That(result, Is.EqualTo(expected));
        Assert.That(session.XideriaFragments, Is.EqualTo(10));
        Assert.That(session.Gold, Is.EqualTo(20));
        Assert.That(session.RunState.PurchasedUpgradeIds, Is.Empty);
        Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void MissingWrongOrUnimplementedDefinitionsCannotUnlockOrSpend()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        Assert.That(session.TryPurchaseUpgrade(null, out var result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.InvalidDefinition));
        Set(definition, "upgradeId", " ");
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.InvalidDefinition));
        Set(definition, "upgradeId", "wrong.id");
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.UnsupportedUpgrade));
        Set(definition, "upgradeId", UpgradeDefinition.VitalImpulseId);
        Set(definition, "upgradeType", UpgradeType.StatIncrease);
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.UnsupportedUpgrade));
        Set(definition, "upgradeType", (UpgradeType)99);
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.False);
        Assert.That(session.XideriaFragments, Is.EqualTo(10));
        Assert.That(session.RunState.PurchasedUpgradeIds, Is.Empty);
    }

    [Test]
    public void ObserverExceptionCannotReportCommittedPurchaseAsFailureOrSkipOtherObservers()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        int notifications = 0;
        session.BalanceChanged += (_, _) => throw new InvalidOperationException("Test balance observer");
        session.BalanceChanged += (_, _) => notifications++;
        session.UpgradesChanged += () => throw new InvalidOperationException("Test upgrade observer");
        session.UpgradesChanged += () => notifications++;
        LogAssert.Expect(LogType.Exception, "InvalidOperationException: Test balance observer");
        LogAssert.Expect(LogType.Exception, "InvalidOperationException: Test upgrade observer");
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.True);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.Success));
        Assert.That(session.XideriaFragments, Is.EqualTo(7));
        Assert.That(session.HasUpgrade(definition.Id), Is.True);
        Assert.That(notifications, Is.EqualTo(2));
    }

    [Test]
    public void ResetAndSceneIdentitiesPreservePurchasedStateAndJsonSnapshot()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 10);
        session.TryAddCurrency(RunCurrency.Gold, 5);
        session.TryPurchaseUpgrade(definition, out _);
        RunState state = session.RunState;
        session.ResetSession();
        foreach (string scene in new[] { "Lobby", "Level01", "Lobby" })
            state.EnterScene(scene);
        var restored = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(state));
        Assert.That(session.RunState, Is.SameAs(state));
        Assert.That(restored.HasUpgrade(UpgradeDefinition.VitalImpulseId), Is.True);
        Assert.That(restored.XideriaFragments, Is.EqualTo(7));
        Assert.That(restored.Gold, Is.EqualTo(5));
        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(state)));
    }

    [Test]
    public void LegacySnapshotWithoutUpgradesRemainsLockedAndKeepsMoney()
    {
        var restored = JsonUtility.FromJson<RunState>("{\"schemaVersion\":2,\"gold\":5,\"xideriaFragments\":7}");
        Assert.That(restored.PurchasedUpgradeIds, Is.Empty);
        Assert.That(restored.Gold, Is.EqualTo(5));
        Assert.That(restored.XideriaFragments, Is.EqualTo(7));
        Assert.That(restored.SchemaVersion, Is.EqualTo(RunState.CurrentSchemaVersion));
    }

    [Test]
    public void AbandonWithNoRemainingMoneyStillNotifiesOwnershipRemovalOnlyOnce()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 3);
        session.TryPurchaseUpgrade(definition, out _);
        int notifications = 0;
        session.UpgradesChanged += () =>
        {
            notifications++;
            Assert.That(session.HasUpgrade(definition.Id), Is.False);
        };
        session.AbandonRun();
        session.AbandonRun();
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(session.RunState.PurchasedUpgradeIds, Is.Empty);
    }
}
