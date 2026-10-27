using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RunStateTests
{
    private GameObject sessionObject;
    private PlayerSessionData session;

    [SetUp]
    public void SetUp()
    {
        sessionObject = new GameObject("RunState test session");
        session = sessionObject.AddComponent<PlayerSessionData>();
        // Currency-operation tests start at zero; startup funds have separate coverage.
        session.AbandonRun();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void NewRunHasIndependentIdentityAndEmptySerializableRecords()
    {
        Assert.That(session.RunState.SchemaVersion, Is.EqualTo(RunState.CurrentSchemaVersion));
        Assert.That(Guid.TryParse(session.RunState.RunId, out _), Is.True);
        Assert.That(new RunState().RunId, Is.Not.EqualTo(session.RunState.RunId));
        Assert.That(session.XideriaFragments, Is.Zero);
        Assert.That(session.Gold, Is.Zero);
        Assert.That(session.RunState.Rooms, Is.Empty);
        Assert.That(session.RunState.Rewards, Is.Empty);
    }

    [Test]
    public void CurrenciesAccumulateIndependently()
    {
        Assert.That(session.TryAddCurrency(RunCurrency.Gold, 7), Is.True);
        Assert.That(session.TryAddCurrency(RunCurrency.XideriaFragments, 3), Is.True);
        Assert.That(session.TryAddCurrency(RunCurrency.Gold, 5), Is.True);

        Assert.That(session.GetBalance(RunCurrency.Gold), Is.EqualTo(12));
        Assert.That(session.Gold, Is.EqualTo(12));
        Assert.That(session.GetBalance(RunCurrency.XideriaFragments), Is.EqualTo(3));
        Assert.That(session.XideriaFragments, Is.EqualTo(3));
    }

    [TestCase(RunCurrency.Gold, 0)]
    [TestCase(RunCurrency.Gold, -1)]
    [TestCase(RunCurrency.Gold, int.MinValue)]
    [TestCase(RunCurrency.XideriaFragments, 0)]
    [TestCase(RunCurrency.XideriaFragments, -1)]
    [TestCase(RunCurrency.XideriaFragments, int.MinValue)]
    public void NonpositiveAdditionChangesNothingAndDoesNotNotify(RunCurrency currency, int amount)
    {
        session.TryAddCurrency(currency, 9);
        int notifications = 0;
        session.BalanceChanged += (_, _) => notifications++;

        Assert.That(session.TryAddCurrency(currency, amount), Is.False);
        Assert.That(session.GetBalance(currency), Is.EqualTo(9));
        Assert.That(notifications, Is.Zero);
    }

    [TestCase(RunCurrency.Gold)]
    [TestCase(RunCurrency.XideriaFragments)]
    public void ExactNumericLimitIsAcceptedAndOverflowIsRejectedInFull(RunCurrency currency)
    {
        session.TryAddCurrency(currency, 1);
        int notifications = 0;
        session.BalanceChanged += (_, _) => notifications++;

        Assert.That(session.TryAddCurrency(currency, int.MaxValue), Is.False);
        Assert.That(session.GetBalance(currency), Is.EqualTo(1));
        Assert.That(notifications, Is.Zero);

        Assert.That(session.TryAddCurrency(currency, int.MaxValue - 1), Is.True);
        Assert.That(session.GetBalance(currency), Is.EqualTo(int.MaxValue));
        Assert.That(session.TryAddCurrency(currency, 1), Is.False);
        Assert.That(session.GetBalance(currency), Is.EqualTo(int.MaxValue));
        Assert.That(notifications, Is.EqualTo(1));
    }

    [Test]
    public void UnknownCurrencyCannotChangeBalances()
    {
        int notifications = 0;
        session.BalanceChanged += (_, _) => notifications++;
        var unknownCurrency = (RunCurrency)99;

        Assert.That(session.TryAddCurrency(unknownCurrency, 10), Is.False);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.GetBalance(unknownCurrency));
        Assert.That(session.Gold, Is.Zero);
        Assert.That(session.XideriaFragments, Is.Zero);
        Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void NotificationsReportCommittedBalances()
    {
        var changes = new List<(RunCurrency currency, int total)>();
        session.BalanceChanged += (currency, total) =>
        {
            Assert.That(session.GetBalance(currency), Is.EqualTo(total));
            changes.Add((currency, total));
        };

        session.TryAddCurrency(RunCurrency.Gold, 4);
        session.TryAddCurrency(RunCurrency.Gold, 6);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 2);

        CollectionAssert.AreEqual(new[]
        {
            (RunCurrency.Gold, 4),
            (RunCurrency.Gold, 10),
            (RunCurrency.XideriaFragments, 2)
        }, changes);
    }

    [Test]
    public void TemporarySessionResetPreservesTheEntireRunAndDoesNotNotify()
    {
        SeedRunRecords();
        session.TryAddCurrency(RunCurrency.Gold, 17);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 8);
        RunState original = session.RunState;
        string snapshot = JsonUtility.ToJson(original);
        int notifications = 0;
        session.BalanceChanged += (_, _) => notifications++;

        session.hasSavedData = true;
        session.hasHelmet = true;
        session.equippedHelmetCharges = 3;
        session.skillCooldowns["test"] = 2;
        session.ResetSession();
        session.ResetSession();

        Assert.That(session.RunState, Is.SameAs(original));
        Assert.That(JsonUtility.ToJson(session.RunState), Is.EqualTo(snapshot));
        Assert.That(notifications, Is.Zero);
        Assert.That(session.hasSavedData, Is.False);
        Assert.That(session.hasHelmet, Is.False);
        Assert.That(session.equippedHelmetCharges, Is.Zero);
        Assert.That(session.forceNewRun, Is.True);
        Assert.That(session.skillCooldowns, Is.Empty);
    }

    [Test]
    public void AbandonResetsRunDataAndNotifiesWithoutReplacingStateOrSubscribers()
    {
        SeedRunRecords();
        session.TryAddCurrency(RunCurrency.Gold, 17);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 8);
        RunState original = session.RunState;
        string oldRunId = original.RunId;
        var changes = new List<(RunCurrency currency, int total)>();
        session.BalanceChanged += (currency, total) => changes.Add((currency, total));
        session.hasSavedData = true;

        session.AbandonRun();

        Assert.That(session.RunState, Is.SameAs(original));
        Assert.That(original.RunId, Is.Not.EqualTo(oldRunId));
        Assert.That(original.Gold, Is.Zero);
        Assert.That(original.XideriaFragments, Is.Zero);
        Assert.That(original.Rooms, Is.Empty);
        Assert.That(original.Rewards, Is.Empty);
        Assert.That(session.hasSavedData, Is.True);
        CollectionAssert.AreEqual(new[]
        {
            (RunCurrency.XideriaFragments, 0),
            (RunCurrency.Gold, 0)
        }, changes);

        session.TryAddCurrency(RunCurrency.Gold, 1);
        Assert.That(changes[2], Is.EqualTo((RunCurrency.Gold, 1)));
    }

    [Test]
    public void AbandonEmptyRunDoesNotEmitBalanceChanges()
    {
        int notifications = 0;
        session.BalanceChanged += (_, _) => notifications++;

        session.AbandonRun();
        session.AbandonRun();

        Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void SnapshotRoundTripPreservesCurrenciesIdentitiesAndDeliveryRecords()
    {
        SeedRunRecords();
        session.TryAddCurrency(RunCurrency.Gold, int.MaxValue);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 8);
        string json = JsonUtility.ToJson(session.RunState);

        RunState restored = JsonUtility.FromJson<RunState>(json);

        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(json));
        Assert.That(restored.RunId, Is.EqualTo(session.RunState.RunId));
        Assert.That(restored.Gold, Is.EqualTo(int.MaxValue));
        Assert.That(restored.XideriaFragments, Is.EqualTo(8));
        Assert.That(restored.Rooms[0].RoomId, Is.EqualTo("room-1"));
        Assert.That(restored.Rooms[0].IsCompleted, Is.True);
        Assert.That(restored.Rewards[0].RewardId, Is.EqualTo("reward-1"));
        Assert.That(restored.Rewards[0].IsDelivered, Is.True);
    }

    [Test]
    public void DeserializationNormalizesInvalidBalancesAndMissingContainers()
    {
        RunState restored = JsonUtility.FromJson<RunState>(
            "{\"runId\":\"\",\"gold\":-1,\"xideriaFragments\":-2147483648,\"rooms\":null,\"rewards\":null}");

        Assert.That(restored.Gold, Is.Zero);
        Assert.That(restored.XideriaFragments, Is.Zero);
        Assert.That(Guid.TryParse(restored.RunId, out _), Is.True);
        Assert.That(restored.Rooms, Is.Not.Null);
        Assert.That(restored.Rewards, Is.Not.Null);
    }

    private void SeedRunRecords()
    {
        // Populate the future data containers without implementing room/reward gameplay.
        JsonUtility.FromJsonOverwrite(
            "{\"rooms\":[{\"RoomId\":\"room-1\",\"SceneId\":\"Level01\",\"IsCompleted\":true}]," +
            "\"rewards\":[{\"RewardId\":\"reward-1\",\"RoomId\":\"room-1\"," +
            "\"RewardDefinitionId\":\"gold\",\"Amount\":17,\"IsDelivered\":true}]}",
            session.RunState);
    }
}
