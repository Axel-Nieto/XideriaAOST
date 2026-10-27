using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunCurrency
{
    XideriaFragments = 0,
    Gold = 1
}

/// <summary>
/// Serializable run data. It contains no references to scene objects.
/// Currency changes are owned by PlayerSessionData, which publishes notifications.
/// </summary>
[Serializable]
public sealed class RunState : ISerializationCallbackReceiver
{
    public const int CurrentSchemaVersion = 3;

    [SerializeField] private int schemaVersion = CurrentSchemaVersion;
    [SerializeField] private string runId = Guid.NewGuid().ToString("N");
    [SerializeField] private int xideriaFragments;
    [SerializeField] private int gold;
    [SerializeField] private bool startingBalancesInitialized;

    [SerializeField] private string currentRoomId;
    [SerializeField] private string pendingRoomId;

    [SerializeField] private List<RunRoomRecord> rooms = new();
    [SerializeField] private List<RunRewardRecord> rewards = new();
    [SerializeField] private List<string> purchasedUpgradeIds = new();

    public int SchemaVersion => schemaVersion;
    public string RunId => runId;
    public int XideriaFragments => xideriaFragments;
    public int Gold => gold;
    public IReadOnlyList<RunRoomRecord> Rooms => rooms;
    public IReadOnlyList<RunRewardRecord> Rewards => rewards;
    public IReadOnlyList<string> PurchasedUpgradeIds => purchasedUpgradeIds.AsReadOnly();
    public string CurrentRoomId => currentRoomId;
    public string PendingRoomId => pendingRoomId;

    public RunRoomRecord FindRoom(string roomId) => rooms.Find(room => room.RoomId == roomId);

    public RunRewardRecord FindDoorOffer(string sourceRoomId, string doorId) =>
        rewards.Find(offer => offer.SourceRoomId == sourceRoomId && offer.DoorId == doorId);

    // A reload retains the current instance. Unannounced scene loads use a stable
    // entry room, so returning to the Lobby cannot recreate its door offers.
    public RunRoomRecord EnterScene(string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
            throw new ArgumentException("A room requires a scene identity.", nameof(sceneId));

        RunRoomRecord pending = FindRoom(pendingRoomId);
        RunRoomRecord current = FindRoom(currentRoomId);
        if (pending != null && pending.SceneId == sceneId)
            current = pending;
        else if (current == null || current.SceneId != sceneId)
        {
            current = rooms.Find(room => room.IsSceneEntry && room.SceneId == sceneId);
            if (current == null)
            {
                current = new RunRoomRecord
                {
                    RoomId = Guid.NewGuid().ToString("N"), SceneId = sceneId, IsSceneEntry = true
                };
                rooms.Add(current);
            }
        }

        currentRoomId = current.RoomId;
        pendingRoomId = null;
        return current;
    }

    public bool TryPrepareDoorOffer(string sourceRoomId, string doorId,
        IReadOnlyList<string> targetScenes, IReadOnlyList<string> combatScenes,
        DoorRewardSettings settings, Func<double> nextRandom,
        out RunRewardRecord offer, out string error)
    {
        offer = null;
        error = null;
        if (string.IsNullOrWhiteSpace(sourceRoomId) || string.IsNullOrWhiteSpace(doorId) || FindRoom(sourceRoomId) == null)
        {
            error = "A door offer requires an existing source room and a door identity.";
            return false;
        }

        offer = FindDoorOffer(sourceRoomId, doorId);
        if (offer != null)
            return true;

        if (targetScenes == null || targetScenes.Count == 0 || nextRandom == null)
        {
            error = "Missing source room, door identity, or destination configuration.";
            return false;
        }

        bool mayReward = false;
        foreach (string target in targetScenes)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                error = "A door contains an empty destination.";
                return false;
            }
            mayReward |= IsCombatDestination(target, combatScenes);
        }

        // Validate before drawing anything: an invalid setup cannot reroll a destination.
        if (mayReward && (settings == null || !settings.IsValid))
        {
            error = "Reward weights must be nonnegative, with a positive total and positive amounts for enabled currencies.";
            return false;
        }

        double destinationRoll = nextRandom();
        if (!IsRandomSample(destinationRoll))
        {
            error = "Random samples must be in [0, 1).";
            return false;
        }
        string destination = targetScenes[(int)(destinationRoll * targetScenes.Count)];
        bool hasReward = IsCombatDestination(destination, combatScenes);
        RunCurrency currency = default;
        int amount = 0;
        if (hasReward && !settings.TryRoll(nextRandom(), out currency, out amount))
        {
            error = "The reward configuration or random sample is invalid.";
            return false;
        }

        RunRoomRecord targetRoom = !hasReward ?
            rooms.Find(room => room.IsSceneEntry && room.SceneId == destination) : null;
        bool newRoom = targetRoom == null;
        targetRoom ??= new RunRoomRecord
        {
            RoomId = Guid.NewGuid().ToString("N"), SceneId = destination, IsSceneEntry = !hasReward
        };
        offer = new RunRewardRecord
        {
            RewardId = Guid.NewGuid().ToString("N"), SourceRoomId = sourceRoomId,
            DoorId = doorId, RoomId = targetRoom.RoomId, DestinationScene = destination,
            HasReward = hasReward, Currency = currency, Amount = amount,
            RewardDefinitionId = hasReward ? currency.ToString() : null
        };
        if (newRoom)
            rooms.Add(targetRoom);
        rewards.Add(offer);
        return true;
    }

    public bool TrySelectDoor(RunRewardRecord offer)
    {
        if (offer == null || !rewards.Contains(offer) || offer.SourceRoomId != currentRoomId ||
            !string.IsNullOrEmpty(pendingRoomId))
            return false;

        RunRoomRecord destination = FindRoom(offer.RoomId);
        if (destination == null || destination.SceneId != offer.DestinationScene)
            return false;

        pendingRoomId = destination.RoomId;
        return true;
    }

    internal void CancelPendingDoor() => pendingRoomId = null;

    internal static bool IsRandomSample(double value) =>
        !double.IsNaN(value) && value >= 0 && value < 1;

    private static bool IsCombatDestination(string scene, IReadOnlyList<string> combatScenes)
    {
        // These existing project destinations never represent rewarded combat rooms.
        if (string.Equals(scene, "Lobby", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scene, "Tutorial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scene, "Inicio", StringComparison.OrdinalIgnoreCase))
            return false;

        if (combatScenes != null)
            foreach (string combatScene in combatScenes)
                if (combatScene == scene)
                    return true;
        return false;
    }

    public int GetBalance(RunCurrency currency)
    {
        return currency switch
        {
            RunCurrency.XideriaFragments => xideriaFragments,
            RunCurrency.Gold => gold,
            _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, "Unknown run currency.")
        };
    }

    // Reject the entire addition if it cannot be represented; never wrap or truncate it.
    internal bool TryAddCurrency(RunCurrency currency, int amount)
    {
        if (amount <= 0)
            return false;

        switch (currency)
        {
            case RunCurrency.XideriaFragments:
                if (amount > int.MaxValue - xideriaFragments)
                    return false;

                xideriaFragments += amount;
                return true;

            case RunCurrency.Gold:
                if (amount > int.MaxValue - gold)
                    return false;

                gold += amount;
                return true;

            default:
                return false;
        }
    }

    public bool HasUpgrade(string upgradeId) =>
        !string.IsNullOrWhiteSpace(upgradeId) && purchasedUpgradeIds.Contains(upgradeId);

    // No events or external callbacks inside this transaction. Validate everything
    // before changing state; allocate the list entry before subtracting the balance.
    internal UpgradePurchaseResult TryPurchaseUpgrade(string upgradeId, int fragmentCost)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return UpgradePurchaseResult.InvalidDefinition;
        if (fragmentCost <= 0)
            return UpgradePurchaseResult.InvalidCost;
        if (HasUpgrade(upgradeId))
            return UpgradePurchaseResult.AlreadyPurchased;
        if (xideriaFragments < fragmentCost)
            return UpgradePurchaseResult.InsufficientFragments;

        purchasedUpgradeIds.Add(upgradeId);
        xideriaFragments -= fragmentCost;
        return UpgradePurchaseResult.Success;
    }

    // Only the persistent session initializes a new run. Keep this decision in
    // serializable state so re-enabling UI or restoring an existing run cannot
    // grant the test funds again. A populated run is never treated as new.
    internal void InitializeStartingBalances()
    {
        if (startingBalancesInitialized)
            return;

        startingBalancesInitialized = true;
        if (xideriaFragments != 0 || gold != 0 || rooms.Count != 0 || rewards.Count != 0 ||
            purchasedUpgradeIds.Count != 0)
            return;

        TryAddCurrency(RunCurrency.XideriaFragments, 10);
        TryAddCurrency(RunCurrency.Gold, 10);
    }

    internal void Reset()
    {
        schemaVersion = CurrentSchemaVersion;
        runId = Guid.NewGuid().ToString("N");
        xideriaFragments = 0;
        gold = 0;
        // AbandonRun deliberately leaves zero balances, without another grant.
        startingBalancesInitialized = true;
        currentRoomId = null;
        pendingRoomId = null;
        rooms.Clear();
        rewards.Clear();
        purchasedUpgradeIds.Clear();
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        // Keep the currency invariant even when reading incomplete or invalid data.
        xideriaFragments = Math.Max(0, xideriaFragments);
        gold = Math.Max(0, gold);
        if (string.IsNullOrEmpty(runId))
            runId = Guid.NewGuid().ToString("N");

        rooms ??= new List<RunRoomRecord>();
        rewards ??= new List<RunRewardRecord>();
        purchasedUpgradeIds ??= new List<string>();
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        purchasedUpgradeIds.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !uniqueIds.Add(id));
        if (schemaVersion < CurrentSchemaVersion)
            schemaVersion = CurrentSchemaVersion;
    }
}

// Stable identifiers and plain values only, ready for a future save format.
[Serializable]
public sealed class RunRoomRecord
{
    public string RoomId;
    public string SceneId;
    public bool IsCompleted;
    public bool IsSceneEntry;
}

[Serializable]
public sealed class RunRewardRecord
{
    public string RewardId;
    public string RoomId;
    public string RewardDefinitionId;
    public int Amount;
    public bool IsDelivered;
    public string SourceRoomId;
    public string DoorId;
    public string DestinationScene;
    public bool HasReward;
    public RunCurrency Currency;
}
