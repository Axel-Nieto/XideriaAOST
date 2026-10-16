using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DoorRewardTests
{
    public static DoorRewardSettings Settings() => new()
    {
        GoldWeight = 1, XideriaFragmentsWeight = 1, GoldAmount = 7, XideriaFragmentsAmount = 11
    };

    public static RunRewardRecord Prepare(RunState state, string doorId = "door-a", double rewardRoll = 0)
    {
        string source = state.CurrentRoomId ?? state.EnterScene("Lobby").RoomId;
        int draws = 0;
        Assert.That(state.TryPrepareDoorOffer(source, doorId,
            new[] { "Level01", "Level02" }, new[] { "Level01", "Level02" }, Settings(),
            () => ++draws == 1 ? 0.75 : rewardRoll, out RunRewardRecord offer, out string error), Is.True, error);
        return offer;
    }

    [Test]
    public void OfferIsDrawnOnceAndSurvivesReloadAndConfigurationChanges()
    {
        var state = new RunState();
        RunRewardRecord first = Prepare(state);
        string source = state.CurrentRoomId;
        Assert.That(first.DestinationScene, Is.EqualTo("Level02"));
        Assert.That(state.EnterScene("Lobby").RoomId, Is.EqualTo(source));

        Assert.That(state.TryPrepareDoorOffer(source, "door-a", null, null, null,
            () => throw new Exception("An existing offer must not draw again"),
            out RunRewardRecord existing, out _), Is.True);
        Assert.That(existing, Is.SameAs(first));
        Assert.That(existing.Amount, Is.EqualTo(7));
        Assert.That(existing.Currency, Is.EqualTo(RunCurrency.Gold));
        Assert.That(state.Rewards.Count, Is.EqualTo(1));
    }

    [Test]
    public void IndependentDoorsCanAnnounceDifferentRewardsForTheSameSceneAsset()
    {
        var state = new RunState();
        RunRewardRecord gold = Prepare(state, "a", 0);
        RunRewardRecord fragments = Prepare(state, "b", 0.75);
        Assert.That(gold.DestinationScene, Is.EqualTo(fragments.DestinationScene));
        Assert.That(gold.RoomId, Is.Not.EqualTo(fragments.RoomId));
        Assert.That(gold.Currency, Is.EqualTo(RunCurrency.Gold));
        Assert.That(fragments.Currency, Is.EqualTo(RunCurrency.XideriaFragments));
        Assert.That(fragments.Amount, Is.EqualTo(11));
    }

    [Test]
    public void RepeatedTransitionsCannotReplaceThePendingChoice()
    {
        var state = new RunState();
        RunRewardRecord first = Prepare(state, "a");
        RunRewardRecord second = Prepare(state, "b");
        Assert.That(state.TrySelectDoor(first), Is.True);
        Assert.That(state.TrySelectDoor(first), Is.False);
        Assert.That(state.TrySelectDoor(second), Is.False);
        Assert.That(state.PendingRoomId, Is.EqualTo(first.RoomId));
        Assert.That(state.EnterScene(first.DestinationScene).RoomId, Is.EqualTo(first.RoomId));
        Assert.That(state.PendingRoomId, Is.Null);
        Assert.That(state.TrySelectDoor(second), Is.False);
    }

    [Test]
    public void DeathReturnAndSceneReloadRetainTheAnnouncedRoom()
    {
        var state = new RunState();
        RunRewardRecord offer = Prepare(state);
        string lobby = state.CurrentRoomId;
        state.TrySelectDoor(offer);
        state.EnterScene(offer.DestinationScene);
        Assert.That(state.EnterScene(offer.DestinationScene).RoomId, Is.EqualTo(offer.RoomId));
        Assert.That(state.EnterScene("Lobby").RoomId, Is.EqualTo(lobby));
        Assert.That(Prepare(state), Is.SameAs(offer));
        Assert.That(state.TrySelectDoor(offer), Is.True);
        Assert.That(state.EnterScene(offer.DestinationScene).RoomId, Is.EqualTo(offer.RoomId));
    }

    [TestCase("Lobby")]
    [TestCase("Tutorial")]
    [TestCase("Inicio")]
    [TestCase("EmptyRoom")]
    public void NonCombatDestinationsHaveNoReward(string destination)
    {
        var state = new RunState();
        string source = state.EnterScene("Level01").RoomId;
        Assert.That(state.TryPrepareDoorOffer(source, "exit", new[] { destination },
            new[] { "Lobby", "Tutorial", "Inicio", "Level01" }, null, () => 0,
            out RunRewardRecord offer, out _), Is.True);
        Assert.That(offer.HasReward, Is.False);
        Assert.That(offer.Amount, Is.Zero);
    }

    [Test]
    public void ReturnDoorUsesTheOriginalLobbyIdentity()
    {
        var state = new RunState();
        RunRewardRecord entry = Prepare(state);
        string lobby = state.CurrentRoomId;
        state.TrySelectDoor(entry);
        string combat = state.EnterScene(entry.DestinationScene).RoomId;
        state.TryPrepareDoorOffer(combat, "return", new[] { "Lobby" }, null, null, () => 0,
            out RunRewardRecord back, out _);
        Assert.That(back.RoomId, Is.EqualTo(lobby));
        state.TrySelectDoor(back);
        Assert.That(state.EnterScene("Lobby").RoomId, Is.EqualTo(lobby));
        Assert.That(Prepare(state), Is.SameAs(entry));
    }

    [TestCase(-1, 1, 7, 11)]
    [TestCase(1, -1, 7, 11)]
    [TestCase(0, 0, 7, 11)]
    [TestCase(1, 1, 0, 11)]
    [TestCase(1, 1, 7, -1)]
    public void InvalidRewardConfigurationsDoNotDrawOrRegisterOffers(int goldWeight, int fragmentWeight, int gold, int fragments)
    {
        var state = new RunState();
        string source = state.EnterScene("Lobby").RoomId;
        var settings = new DoorRewardSettings
        {
            GoldWeight = goldWeight, XideriaFragmentsWeight = fragmentWeight,
            GoldAmount = gold, XideriaFragmentsAmount = fragments
        };
        Assert.That(state.TryPrepareDoorOffer(source, "door", new[] { "Level01" }, new[] { "Level01" },
            settings, () => throw new Exception("Invalid config must not draw"), out _, out _), Is.False);
        Assert.That(state.Rewards, Is.Empty);
        Assert.That(state.Rooms.Count, Is.EqualTo(1));
    }

    [TestCase(double.NaN)]
    [TestCase(-0.1)]
    [TestCase(1.0)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidRandomSamplesDoNotCreatePartialRecords(double sample)
    {
        var state = new RunState();
        string source = state.EnterScene("Lobby").RoomId;
        Assert.That(state.TryPrepareDoorOffer(source, "door", new[] { "Level01" }, new[] { "Level01" },
            Settings(), () => sample, out _, out _), Is.False);
        Assert.That(state.Rewards, Is.Empty);
        Assert.That(state.Rooms.Count, Is.EqualTo(1));
    }

    [Test]
    public void WeightsSupportEqualSplitZeroWeightAndLargeTotals()
    {
        DoorRewardSettings settings = Settings();
        settings.TryRoll(0.499999, out RunCurrency low, out _);
        settings.TryRoll(0.5, out RunCurrency high, out _);
        Assert.That(low, Is.EqualTo(RunCurrency.Gold));
        Assert.That(high, Is.EqualTo(RunCurrency.XideriaFragments));
        settings.GoldWeight = 0;
        settings.GoldAmount = 0;
        Assert.That(settings.TryRoll(0, out low, out _), Is.True);
        Assert.That(low, Is.EqualTo(RunCurrency.XideriaFragments));
        settings.GoldWeight = int.MaxValue;
        settings.GoldAmount = 1;
        settings.XideriaFragmentsWeight = int.MaxValue;
        Assert.That(settings.IsValid, Is.True);
        Assert.That(settings.TryRoll(0.75, out high, out _), Is.True);
        Assert.That(high, Is.EqualTo(RunCurrency.XideriaFragments));
    }

    [Test]
    public void InvalidDestinationAndIdentityConfigurationsDoNotDraw()
    {
        var state = new RunState();
        string source = state.EnterScene("Lobby").RoomId;
        Func<double> failOnDraw = () => throw new Exception("Invalid setup must not draw");
        foreach (string[] targets in new[] { null, new string[0], new[] { "" }, new[] { "Level01", " " } })
            Assert.That(state.TryPrepareDoorOffer(source, "door", targets, new[] { "Level01" },
                Settings(), failOnDraw, out _, out _), Is.False);
        Assert.That(state.TryPrepareDoorOffer("unknown-room", "door", new[] { "Level01" }, null,
            Settings(), failOnDraw, out _, out _), Is.False);
        Assert.That(state.TryPrepareDoorOffer(source, " ", new[] { "Level01" }, null,
            Settings(), failOnDraw, out _, out _), Is.False);
        Assert.That(state.Rewards, Is.Empty);
        Assert.That(state.Rooms.Count, Is.EqualTo(1));
    }

    [Test]
    public void InvalidRewardSampleDoesNotLeaveATargetRoomBehind()
    {
        var state = new RunState();
        string source = state.EnterScene("Lobby").RoomId;
        int draws = 0;
        Assert.That(state.TryPrepareDoorOffer(source, "door", new[] { "Level01" }, new[] { "Level01" },
            Settings(), () => ++draws == 1 ? 0 : double.NaN, out _, out _), Is.False);
        Assert.That(state.Rewards, Is.Empty);
        Assert.That(state.Rooms.Count, Is.EqualTo(1));
    }

    [Test]
    public void TwoAdjacentCellsShareAnOrderIndependentLogicalDoor()
    {
        TerrainCellData a = Cell(11, 5);
        TerrainCellData b = Cell(11, 6);
        List<LogicalDoor> doors = LogicalDoor.Build(new[] { a, b });
        Assert.That(doors.Count, Is.EqualTo(1));
        Assert.That(doors[0].Cells.Count, Is.EqualTo(2));
        Assert.That(LogicalDoor.Build(new[] { b, a })[0].Id, Is.EqualTo(doors[0].Id));
        Assert.That(LogicalDoor.TryFindTouched(doors, new[] { new Vector2Int(11, 5) }, out LogicalDoor one), Is.True);
        // Logical origin (10,5) is outside the door; the right edge of a 2x2 touches it.
        Assert.That(LogicalDoor.TryFindTouched(doors, new[]
        {
            new Vector2Int(10, 5), new Vector2Int(10, 6), new Vector2Int(11, 5), new Vector2Int(11, 6)
        }, out LogicalDoor large), Is.True);
        Assert.That(large, Is.SameAs(one));
    }

    [Test]
    public void ExplicitIdsSeparateAdjacentDoorsAndRejectAmbiguousFootprints()
    {
        var doors = LogicalDoor.Build(new[] { Cell(11, 5, "a"), Cell(11, 6, "b") });
        Assert.That(doors.Count, Is.EqualTo(2));
        Assert.That(LogicalDoor.TryFindTouched(doors,
            new[] { new Vector2Int(11, 5), new Vector2Int(11, 6) }, out _), Is.False);
    }

    [Test]
    public void ExplicitIdWithInconsistentDestinationsIsInvalid()
    {
        TerrainCellData a = Cell(11, 5, "same");
        TerrainCellData b = Cell(11, 6, "same");
        SetField(b, "targetScenes", new List<string> { "Level02" });
        var doors = LogicalDoor.Build(new[] { a, b });
        Assert.That(doors.Count, Is.EqualTo(1));
        Assert.That(doors[0].IsValid, Is.False);
    }

    [Test]
    public void OfferSnapshotRetainsDestinationAndDeliveryState()
    {
        var state = new RunState();
        RunRewardRecord offer = Prepare(state);
        state.TrySelectDoor(offer);
        offer.IsDelivered = true;
        string json = JsonUtility.ToJson(state);
        RunState restored = JsonUtility.FromJson<RunState>(json);
        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(json));
        Assert.That(restored.FindDoorOffer(offer.SourceRoomId, offer.DoorId).IsDelivered, Is.True);
        Assert.That(restored.EnterScene(offer.DestinationScene).RoomId, Is.EqualTo(offer.RoomId));
    }

    private static TerrainCellData Cell(int x, int y, string id = null)
    {
        var cell = new TerrainCellData();
        SetField(cell, "position", new Vector2Int(x, y));
        SetField(cell, "type", CellType.ExitRun);
        SetField(cell, "logicalDoorId", id);
        SetField(cell, "targetScenes", new List<string> { "Level01" });
        return cell;
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
}

public class RoomRewardDeliveryTests
{
    private GameObject sessionObject;
    private PlayerSessionData session;

    [SetUp]
    public void SetUp()
    {
        sessionObject = new GameObject("Room reward test session");
        session = sessionObject.AddComponent<PlayerSessionData>();
        // Isolate reward amounts from the new session's initial test funds.
        session.AbandonRun();
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(sessionObject);

    [TestCase(0.0, RunCurrency.Gold, 7)]
    [TestCase(0.75, RunCurrency.XideriaFragments, 11)]
    public void OnlyTheAnnouncedRewardIsDeliveredOnce(double sample, RunCurrency currency, int amount)
    {
        RunRewardRecord offer = DoorRewardTests.Prepare(session.RunState, rewardRoll: sample);
        Assert.That(session.TryCompleteRoom(offer.RoomId), Is.False);
        Assert.That(session.GetBalance(currency), Is.Zero);
        session.RunState.TrySelectDoor(offer);
        session.RunState.EnterScene(offer.DestinationScene);
        Assert.That(session.TryCompleteRoom(offer.RoomId), Is.True);
        Assert.That(session.GetBalance(currency), Is.EqualTo(amount));
        Assert.That(offer.IsDelivered, Is.True);
        Assert.That(session.RunState.FindRoom(offer.RoomId).IsCompleted, Is.True);
        Assert.That(session.TryCompleteRoom(offer.RoomId), Is.False);
        session.ResetSession();
        session.RunState.EnterScene(offer.DestinationScene);
        Assert.That(session.TryCompleteRoom(offer.RoomId), Is.False);
        Assert.That(session.GetBalance(currency), Is.EqualTo(amount));
    }

    [Test]
    public void ReentrantBalanceListenerCannotDeliverTwice()
    {
        RunRewardRecord offer = EnterCombat();
        int notifications = 0;
        session.BalanceChanged += (_, _) =>
        {
            notifications++;
            Assert.That(offer.IsDelivered, Is.True);
            Assert.That(session.TryCompleteRoom(offer.RoomId), Is.False);
        };
        Assert.That(session.TryCompleteRoom(offer.RoomId), Is.True);
        Assert.That(session.Gold, Is.EqualTo(offer.Amount));
        Assert.That(notifications, Is.EqualTo(1));
    }

    [Test]
    public void ThrowingBalanceListenerDoesNotMakeTheRewardPayableAgain()
    {
        RunRewardRecord offer = EnterCombat();
        session.BalanceChanged += (_, _) => throw new InvalidOperationException("Listener failure");
        Assert.Throws<InvalidOperationException>(() => session.TryCompleteRoom(offer.RoomId));
        Assert.That(offer.IsDelivered, Is.True);
        Assert.That(session.TryCompleteRoom(offer.RoomId), Is.False);
        Assert.That(session.Gold, Is.EqualTo(offer.Amount));
    }

    [Test]
    public void OverflowKeepsTheWholeRewardPendingWithoutChangingTheBalance()
    {
        RunRewardRecord offer = EnterCombat();
        session.TryAddCurrency(RunCurrency.Gold, int.MaxValue);
        Assert.That(session.TryCompleteRoom(offer.RoomId), Is.False);
        Assert.That(session.Gold, Is.EqualTo(int.MaxValue));
        Assert.That(offer.IsDelivered, Is.False);
        Assert.That(session.RunState.FindRoom(offer.RoomId).IsCompleted, Is.True);
    }

    [Test]
    public void DeathResetAndLobbyReturnDoNotPayOrLoseThePendingOffer()
    {
        RunRewardRecord offer = EnterCombat();
        session.TryAddCurrency(RunCurrency.XideriaFragments, 5);
        session.ResetSession();
        session.RunState.EnterScene("Lobby");
        Assert.That(session.XideriaFragments, Is.EqualTo(5));
        Assert.That(session.Gold, Is.Zero);
        Assert.That(offer.IsDelivered, Is.False);
        Assert.That(DoorRewardTests.Prepare(session.RunState), Is.SameAs(offer));
    }

    [Test]
    public void CompletingAnUnannouncedEntryRoomDoesNotPay()
    {
        RunRoomRecord room = session.RunState.EnterScene("EmptyRoom");
        Assert.That(session.TryCompleteRoom(room.RoomId), Is.False);
        Assert.That(session.Gold, Is.Zero);
        Assert.That(session.XideriaFragments, Is.Zero);
    }

    [Test]
    public void AbandonClearsOffersRoomsAndPendingChoice()
    {
        RunRewardRecord offer = DoorRewardTests.Prepare(session.RunState);
        session.RunState.TrySelectDoor(offer);
        string oldId = session.RunState.RunId;
        session.AbandonRun();
        Assert.That(session.RunState.RunId, Is.Not.EqualTo(oldId));
        Assert.That(session.RunState.CurrentRoomId, Is.Null);
        Assert.That(session.RunState.PendingRoomId, Is.Null);
        Assert.That(session.RunState.Rooms, Is.Empty);
        Assert.That(session.RunState.Rewards, Is.Empty);
        Assert.That(session.RunState.TrySelectDoor(offer), Is.False);
    }

    private RunRewardRecord EnterCombat()
    {
        RunRewardRecord offer = DoorRewardTests.Prepare(session.RunState);
        session.RunState.TrySelectDoor(offer);
        session.RunState.EnterScene(offer.DestinationScene);
        return offer;
    }
}
