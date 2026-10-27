using UnityEngine;

public class VitalImpulsePassive : PassiveEffect
{
    private const int EnergyPerDeath = 2;

    public override string Name => "Impulso Vital";
    public override Sprite Icon => StatusIconDatabase.Instance != null
        ? StatusIconDatabase.Instance.vitalImpulseIcon
        : null;

    public override string Description =>
        "Recupera hasta 2 puntos de energía cuando un enemigo termina su secuencia de muerte.";

    public override void OnOtherCharacterDeathCompleted(Character owner, Character deceased)
    {
        if (owner == null || deceased == null || owner == deceased ||
            !owner.IsPlayer || deceased.IsPlayer || !deceased.IsDying ||
            !owner.VitalImpulseEnabled || !owner.gameObject.activeInHierarchy ||
            owner.Stats == null || !owner.Stats.IsAlive || owner.IsDying ||
            owner.gameObject.scene != deceased.gameObject.scene)
        {
            return;
        }

        int previousEnergy = owner.Stats.CurrentEnergy;
        int recovery = Mathf.Min(EnergyPerDeath, owner.Stats.MaxEnergy - previousEnergy);

        if (recovery <= 0)
            return;

        owner.Stats.SetCurrentEnergy(previousEnergy + recovery);

        int recovered = owner.Stats.CurrentEnergy - previousEnergy;
        if (recovered > 0)
            GameEvents.OnEnergyRecovered?.Invoke(owner, recovered);
    }
}
