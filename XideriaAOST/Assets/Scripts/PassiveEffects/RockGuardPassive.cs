using System.Collections.Generic;
using UnityEngine;

public sealed class RockGuardPassive : PassiveEffect
{
    // Every hit contributes its own two-turn stack; a later Q never refreshes old stacks.
    private readonly List<int> stackTurns = new();
    public int ArmorStacks => stackTurns.Count;
    public int ShieldCharges { get; private set; }
    private int shieldTurns;
    public int ShieldTurnsRemaining => ShieldCharges > 0 ? shieldTurns : 0;
    public override UnityEngine.Sprite Icon => StatusIconDatabase.Instance != null
        ? StatusIconDatabase.Instance.rockPassiveIcon : null;
    public override string Name => "Coraza de Tierra";
    public override string Description => $"Giro de Tierra: {ArmorStacks * 10}% de reducción de daño.\n+10% por enemigo golpeado durante dos turnos propios.\n\nEscudo de Roca: {ShieldCharges}/3 cargas.\nCada impacto consume una y añade 10% de reducción.\nDuración restante: {ShieldTurnsRemaining} turnos propios.";

    public void AddArmor(int enemiesHit)
    {
        for (int i = 0; i < enemiesHit; i++) stackTurns.Add(2);
    }

    public void ActivateShield(int turns = 1)
    {
        ShieldCharges = 3;
        shieldTurns = Mathf.Max(1, turns);
    }

    internal static bool HasUpgrade(Character user, string id) => user != null && user.IsPlayer &&
        user.EquippedHelmet != null && user.EquippedHelmet.Type == HelmetType.Rock &&
        PlayerSessionData.Instance != null && PlayerSessionData.Instance.HasUpgrade(id);

    public static int ShieldDuration(Character user) =>
        HasUpgrade(user, UpgradeDefinition.RockShieldDurationId) ? 2 : 1;

    public override int ModifyIncomingDamage(Character owner, int damage, DamageType damageType)
    {
        if (damage <= 0) return damage;
        int stacks = ArmorStacks;
        if (ShieldCharges > 0)
        {
            ShieldCharges--;
            stacks++;
        }
        return Mathf.Max(0, Mathf.CeilToInt(damage * (1f - Mathf.Clamp01(stacks * 0.1f))));
    }

    public override void OnTurnStart(Character owner)
    {
        if (shieldTurns > 0 && --shieldTurns == 0)
            ShieldCharges = 0;
        for (int i = stackTurns.Count - 1; i >= 0; i--)
            if (--stackTurns[i] <= 0) stackTurns.RemoveAt(i);
    }

    public static RockGuardPassive For(Character user)
    {
        foreach (PassiveEffect passive in user.GetPassiveEffects())
            if (passive is RockGuardPassive guard) return guard;
        var result = new RockGuardPassive();
        user.AddPassive(result);
        return result;
    }
}
