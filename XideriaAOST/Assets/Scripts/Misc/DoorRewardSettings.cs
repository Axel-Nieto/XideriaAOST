using System;
using UnityEngine;

[Serializable]
public sealed class DoorRewardSettings
{
    [Min(0)] public int GoldWeight = 1;
    [Min(0)] public int XideriaFragmentsWeight = 1;
    [Tooltip("Set a positive test amount before enabling rewarded doors; no balance value is prescribed.")]
    [Min(0)] public int GoldAmount;
    [Min(0)] public int XideriaFragmentsAmount;

    public bool IsValid => GoldWeight >= 0 && XideriaFragmentsWeight >= 0 &&
        (long)GoldWeight + XideriaFragmentsWeight > 0 &&
        (GoldWeight == 0 || GoldAmount > 0) &&
        (XideriaFragmentsWeight == 0 || XideriaFragmentsAmount > 0);

    public bool TryRoll(double sample, out RunCurrency currency, out int amount)
    {
        currency = default;
        amount = 0;
        if (!IsValid || !RunState.IsRandomSample(sample))
            return false;

        long totalWeight = (long)GoldWeight + XideriaFragmentsWeight;
        currency = sample * totalWeight < GoldWeight ? RunCurrency.Gold : RunCurrency.XideriaFragments;
        amount = currency == RunCurrency.Gold ? GoldAmount : XideriaFragmentsAmount;
        return true;
    }
}
