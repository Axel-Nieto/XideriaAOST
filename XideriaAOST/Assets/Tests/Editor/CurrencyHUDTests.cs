using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class CurrencyHUDTests
{
    private GameObject sessionObject;
    private PlayerSessionData session;
    private GameObject hudObject;
    private CurrencyHUD hud;
    private TMP_Text fragmentsText;
    private TMP_Text goldText;

    [SetUp]
    public void SetUp()
    {
        sessionObject = new GameObject("Currency HUD test session");
        session = sessionObject.AddComponent<PlayerSessionData>();
        session.AbandonRun();
        CreateHUD();
    }

    [TearDown]
    public void TearDown()
    {
        if (hud != null)
            Call(hud, "OnDestroy");
        UnityEngine.Object.DestroyImmediate(hudObject);
        UnityEngine.Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void EmptySessionAndMissingSessionDisplayZero()
    {
        AssertBalances("0", "0");
        Call(hud, "BindSession", new object[] { null });
        AssertBalances("0", "0");
    }

    [Test]
    public void CurrencyEventsImmediatelyUpdateOnlyTheirAuthoritativeTotals()
    {
        session.TryAddCurrency(RunCurrency.Gold, 7);
        AssertBalances("0", "7");
        session.TryAddCurrency(RunCurrency.XideriaFragments, 11);
        AssertBalances("11", "7");
        session.TryAddCurrency(RunCurrency.Gold, 3);
        AssertBalances("11", "10");
    }

    [Test]
    public void EnablingReadsEarlierEarningsAndDoesNotSubscribeTwice()
    {
        Call(hud, "OnDisable");
        session.TryAddCurrency(RunCurrency.Gold, 20);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 13);
        AssertBalances("0", "0");

        Call(hud, "OnEnable");
        Call(hud, "BindSession", session);
        Call(hud, "OnEnable");
        Call(hud, "BindSession", session);
        AssertBalances("13", "20");
        Assert.That(SubscriberCount(session, "BalanceChanged", hud), Is.EqualTo(1));
        Assert.That(SubscriberCount(null, "InstanceChanged", hud), Is.EqualTo(1));

        Call(hud, "OnDisable");
        Assert.That(SubscriberCount(session, "BalanceChanged", hud), Is.Zero);
        Assert.That(SubscriberCount(null, "InstanceChanged", hud), Is.Zero);
    }

    [Test]
    public void LateSessionArrivalAndReplacementRebindWithoutKeepingOldListeners()
    {
        Call(hud, "BindSession", new object[] { null });
        session.TryAddCurrency(RunCurrency.Gold, 9);
        Call(hud, "HandleSessionChanged", session);
        AssertBalances("0", "9");
        Assert.That(SubscriberCount(session, "BalanceChanged", hud), Is.EqualTo(1));

        Call(hud, "HandleSessionChanged", new object[] { null });
        AssertBalances("0", "0");
        Assert.That(SubscriberCount(session, "BalanceChanged", hud), Is.Zero);
    }

    [Test]
    public void RecreatedHUDReadsTheSameRunAndTemporaryResetPreservesBalances()
    {
        session.TryAddCurrency(RunCurrency.Gold, 19);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 12);
        RunState run = session.RunState;
        Call(hud, "OnDestroy");
        Assert.That(SubscriberCount(session, "BalanceChanged", hud), Is.Zero);
        UnityEngine.Object.DestroyImmediate(hudObject);

        session.ResetSession();
        CreateHUD();
        Assert.That(session.RunState, Is.SameAs(run));
        AssertBalances("12", "19");
        Assert.That(SubscriberCount(session, "BalanceChanged", hud), Is.EqualTo(1));
    }

    [Test]
    public void AbandonDisplaysBothZerosAndLaterCurrencyStillUpdates()
    {
        session.TryAddCurrency(RunCurrency.Gold, 8);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 6);
        session.AbandonRun();
        AssertBalances("0", "0");
        session.TryAddCurrency(RunCurrency.Gold, 1);
        AssertBalances("0", "1");
    }

    [Test]
    public void FullNumericBalanceFitsTheTextWithoutNumericConversionLoss()
    {
        session.TryAddCurrency(RunCurrency.Gold, int.MaxValue);
        AssertBalances("0", "2147483647");
    }

    private void CreateHUD()
    {
        hudObject = new GameObject("CurrencyHUD test", typeof(RectTransform));
        hudObject.SetActive(false);
        hud = hudObject.AddComponent<CurrencyHUD>();
        fragmentsText = CreateText("FragmentsAmount");
        goldText = CreateText("GoldAmount");
        SetField(hud, "xideriaFragmentsText", fragmentsText);
        SetField(hud, "goldText", goldText);
        hudObject.SetActive(true);
        // Explicit calls make the tests independent of EditMode's Awake ordering.
        Call(hud, "OnEnable");
        Call(hud, "BindSession", session);
    }

    private TMP_Text CreateText(string name)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(hudObject.transform, false);
        return textObject.AddComponent<TextMeshProUGUI>();
    }

    private void AssertBalances(string fragments, string gold)
    {
        Assert.That(fragmentsText.text, Is.EqualTo(fragments));
        Assert.That(goldText.text, Is.EqualTo(gold));
    }

    private static int SubscriberCount(PlayerSessionData owner, string eventName, object target)
    {
        var field = typeof(PlayerSessionData).GetField(eventName,
            BindingFlags.NonPublic | (owner == null ? BindingFlags.Static : BindingFlags.Instance));
        var listeners = (Delegate)field.GetValue(owner);
        int count = 0;
        if (listeners != null)
            foreach (Delegate listener in listeners.GetInvocationList())
                if (ReferenceEquals(listener.Target, target))
                    count++;
        return count;
    }

    private static void SetField(object owner, string field, object value) =>
        owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

    private static void Call(object owner, string method, params object[] args) =>
        owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
}

public class StartingCurrencyTests
{
    [Test]
    public void FreshRunReceivesTenOfEachOnlyOnce()
    {
        var run = new RunState();
        Initialize(run);
        Assert.That(run.Gold, Is.EqualTo(10));
        Assert.That(run.XideriaFragments, Is.EqualTo(10));
        Initialize(run);
        Assert.That(run.Gold, Is.EqualTo(10));
        Assert.That(run.XideriaFragments, Is.EqualTo(10));
    }

    [Test]
    public void ExistingProgressDoesNotReceiveAnotherStartingGrant()
    {
        var run = new RunState();
        run.EnterScene("Level01");
        Initialize(run);
        Assert.That(run.Gold, Is.Zero);
        Assert.That(run.XideriaFragments, Is.Zero);
    }

    [Test]
    public void ExistingBalanceDoesNotReceiveAnotherStartingGrant()
    {
        var run = new RunState();
        typeof(RunState).GetMethod("TryAddCurrency", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(run, new object[] { RunCurrency.Gold, 7 });
        Initialize(run);
        Assert.That(run.Gold, Is.EqualTo(7));
        Assert.That(run.XideriaFragments, Is.Zero);
    }

    [Test]
    public void ResetCannotBeUsedToRegrantTheStartingCurrencies()
    {
        var run = new RunState();
        Initialize(run);
        typeof(RunState).GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, null);
        Initialize(run);
        Assert.That(run.Gold, Is.Zero);
        Assert.That(run.XideriaFragments, Is.Zero);
    }

    [Test]
    public void StartingGrantMarkerSurvivesSerialization()
    {
        var run = new RunState();
        Initialize(run);
        typeof(RunState).GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, null);
        var restored = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(run));
        Initialize(restored);
        Assert.That(restored.Gold, Is.Zero);
        Assert.That(restored.XideriaFragments, Is.Zero);
    }

    private static void Initialize(RunState run) =>
        typeof(RunState).GetMethod("InitializeStartingBalances", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(run, null);
}
