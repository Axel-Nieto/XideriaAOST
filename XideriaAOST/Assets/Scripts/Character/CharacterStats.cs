using UnityEngine;

public class CharacterStats
{
    private readonly int baseAttack;
    private readonly int baseDefense;
    private readonly int baseMaxHP;
    public int Attack { get; private set; }
    public int Defense { get; private set; }

    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }

    public int MaxEnergy { get; private set; }
    public int CurrentEnergy { get; private set; }
    public int EnergyRegen { get; private set; }

    public CharacterStats(int attack, int defense, int maxHP, int maxEnergy, int energyRegen)
    {
        baseAttack = attack;
        baseDefense = defense;
        baseMaxHP = maxHP;
        Attack = attack;
        Defense = defense;

        MaxHP = maxHP;
        CurrentHP = maxHP;

        MaxEnergy = maxEnergy;
        CurrentEnergy = 0;
        EnergyRegen = energyRegen;
    }

    // Absolute bonuses keep purchase notifications, re-enabling and room loads
    // idempotent. Increasing the maximum does not heal the current character.
    internal void ApplyRunUpgradeBonuses(int healthBonus, int defenseBonus, int damageBonus)
    {
        Attack = AddBonus(baseAttack, damageBonus);
        Defense = AddBonus(baseDefense, defenseBonus);
        MaxHP = AddBonus(baseMaxHP, healthBonus);
        CurrentHP = Mathf.Min(CurrentHP, MaxHP);
    }

    private static int AddBonus(int value, int bonus) =>
        (int)System.Math.Min(int.MaxValue, (long)value + Mathf.Max(0, bonus));

    public void RegenEnergy()
    {
        CurrentEnergy = Mathf.Min(CurrentEnergy + EnergyRegen, MaxEnergy);
    }

    public bool HasEnergy(int amount) => CurrentEnergy >= amount;

    public void ConsumeEnergy(int amount)
    {
        CurrentEnergy -= amount;
    }

    public void SetCurrentHP(int value)
    {
        CurrentHP = Mathf.Clamp(value, 0, MaxHP);
    }

    public void SetCurrentEnergy(int value)
    {
        CurrentEnergy = Mathf.Clamp(value, 0, MaxEnergy);
    }

    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }

    public void Heal(int amount)
    {
        CurrentHP += amount;

        if (CurrentHP > MaxHP)
            CurrentHP = MaxHP;
    }

    public bool IsAlive => CurrentHP > 0;
}
