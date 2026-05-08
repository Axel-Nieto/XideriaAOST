using UnityEngine;

public class CharacterStats
{
    public int Attack { get; private set; }
    public int Defense { get; private set; }

    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }

    public int MaxEnergy { get; private set; }
    public int CurrentEnergy { get; private set; }
    public int EnergyRegen { get; private set; }

    public CharacterStats(int attack, int defense, int maxHP, int maxEnergy, int energyRegen)
    {
        Attack = attack;
        Defense = defense;

        MaxHP = maxHP;
        CurrentHP = maxHP;

        MaxEnergy = maxEnergy;
        CurrentEnergy = energyRegen;
        EnergyRegen = energyRegen;
    }

    public void RegenEnergy()
    {
        CurrentEnergy = Mathf.Min(CurrentEnergy + EnergyRegen, MaxEnergy);
    }

    public bool HasEnergy(int amount) => CurrentEnergy >= amount;

    public void ConsumeEnergy(int amount)
    {
        CurrentEnergy -= amount;
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