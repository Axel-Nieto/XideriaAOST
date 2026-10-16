using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSessionData : MonoBehaviour
{
    public static PlayerSessionData Instance { get; private set; }

    /// <summary>Reports the ready persistent session, or null when it is destroyed.</summary>
    public static event Action<PlayerSessionData> InstanceChanged;

    [SerializeField, HideInInspector] private RunState runState = new();

    public RunState RunState => runState ??= new RunState();
    public int XideriaFragments => RunState.XideriaFragments;
    public int Gold => RunState.Gold;

    /// <summary>Reports the currency and its new total after a balance changes.</summary>
    public event Action<RunCurrency, int> BalanceChanged;

    /// <summary>Ownership changed after a committed purchase or explicit abandonment.</summary>
    public event Action UpgradesChanged;

    public bool hasSavedData = false;

    public int currentHP;
    public int currentEnergy;

    public bool hasHelmet;
    public HelmetType equippedHelmetType = HelmetType.Default;

    public Dictionary<string, int> skillCooldowns = new();

    public bool forceNewRun = false;
    public int equippedHelmetCharges = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        RunState.InitializeStartingBalances();
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InstanceChanged?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        InstanceChanged?.Invoke(null);
    }

    public int GetBalance(RunCurrency currency)
    {
        return RunState.GetBalance(currency);
    }

    /// <summary>
    /// Adds a positive amount in full. Invalid currencies, nonpositive amounts,
    /// and additions exceeding int.MaxValue return false without changing balances.
    /// </summary>
    public bool TryAddCurrency(RunCurrency currency, int amount)
    {
        if (!RunState.TryAddCurrency(currency, amount))
            return false;

        BalanceChanged?.Invoke(currency, GetBalance(currency));
        return true;
    }

    public bool HasUpgrade(string upgradeId) => RunState.HasUpgrade(upgradeId);

    /// <summary>
    /// Atomically buys one supported upgrade with fragments only. A false result
    /// leaves both ownership and balances unchanged. Notifications run after commit.
    /// Call on Unity's main thread, like the other session operations.
    /// </summary>
    public bool TryPurchaseUpgrade(UpgradeDefinition definition, out UpgradePurchaseResult result)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            result = UpgradePurchaseResult.InvalidDefinition;
        else if (definition.FragmentCost <= 0)
            result = UpgradePurchaseResult.InvalidCost;
        else if (definition.Type != UpgradeType.VitalImpulse || definition.Id != UpgradeDefinition.VitalImpulseId)
            result = UpgradePurchaseResult.UnsupportedUpgrade;
        else
            result = RunState.TryPurchaseUpgrade(definition.Id, definition.FragmentCost);

        if (result != UpgradePurchaseResult.Success)
            return false;

        // Both values are already committed: even a reentrant purchase cannot charge twice.
        // A broken observer must not turn a successful transaction into a reported failure
        // or prevent the remaining observers from learning about the committed change.
        int balance = XideriaFragments;
        NotifySafely(BalanceChanged, listener => ((Action<RunCurrency, int>)listener)(RunCurrency.XideriaFragments, balance));
        NotifySafely(UpgradesChanged, listener => ((Action)listener)());
        return true;
    }

    private static void NotifySafely(Delegate listeners, Action<Delegate> invoke)
    {
        if (listeners == null)
            return;

        foreach (Delegate listener in listeners.GetInvocationList())
        {
            try { invoke(listener); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    /// <summary>Called after successful combat completion, never by scene loading or enemy loot.</summary>
    public bool TryCompleteRoom(string roomId)
    {
        RunRoomRecord room = RunState.FindRoom(roomId);
        if (room == null || roomId != RunState.CurrentRoomId)
            return false;

        room.IsCompleted = true;
        RunRewardRecord reward = null;
        foreach (RunRewardRecord candidate in RunState.Rewards)
            if (candidate.RoomId == roomId && candidate.HasReward)
            {
                reward = candidate;
                break;
            }

        if (reward == null || reward.IsDelivered)
            return false;

        // Reserve before BalanceChanged: a reentrant listener cannot pay twice.
        // If a listener throws after crediting, the delivered marker stays committed.
        reward.IsDelivered = true;
        if (!TryAddCurrency(reward.Currency, reward.Amount))
        {
            reward.IsDelivered = false;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Explicitly discards run data, preserving the RunState reference and subscribers.
    /// No death, victory, scene transition, or menu currently calls this operation.
    /// Temporary character data remains the responsibility of ResetSession.
    /// </summary>
    public void AbandonRun()
    {
        bool hadFragments = XideriaFragments != 0;
        bool hadGold = Gold != 0;
        bool hadUpgrades = RunState.PurchasedUpgradeIds.Count != 0;

        RunState.Reset();

        if (hadUpgrades)
            NotifySafely(UpgradesChanged, listener => ((Action)listener)());

        if (hadFragments)
            BalanceChanged?.Invoke(RunCurrency.XideriaFragments, 0);

        if (hadGold)
            BalanceChanged?.Invoke(RunCurrency.Gold, 0);
    }

    /// <summary>Resets temporary character data only; run progress and currencies survive.</summary>
    public void ResetSession()
    {
        hasSavedData = false;
        forceNewRun = true;

        hasHelmet = false;
        equippedHelmetCharges = 0;
        equippedHelmetType = HelmetType.Default;

        skillCooldowns.Clear();
    }
}
