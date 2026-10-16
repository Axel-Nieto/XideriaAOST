using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class VitalImpulseUnlockTests : UpgradeIntegrationFixture
{
    private Character CreateCharacter(bool player, int width = 1, int height = 1)
    {
        var go = new GameObject(player ? "Test Sir Tete" : "Test enemy");
        go.SetActive(false);
        go.transform.SetParent(root.transform, false);
        var character = go.AddComponent<Character>();
        character.enabled = false;
        Set(character, "isPlayer", player);
        Set(character, "gridWidth", width);
        Set(character, "gridHeight", height);
        Set(character, "baseStats", new CharacterStatsData());
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(character, new CharacterStats(5, 2, 20, 7, 3));
        go.SetActive(true);
        return character;
    }

    private void Buy()
    {
        session.TryAddCurrency(RunCurrency.XideriaFragments, 3);
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
    }

    [Test]
    public void ExistingPlayerActivatesImmediatelyAndAbandonDeactivatesTheSameInstance()
    {
        var player = CreateCharacter(true);
        player.AddPassive(new VitalImpulsePassive());
        player.AddPassive(new VitalImpulsePassive());
        Assert.That(player.VitalImpulseEnabled, Is.False);
        Assert.That(player.GetPassiveEffects().OfType<VitalImpulsePassive>(), Is.Empty);
        Buy();
        Assert.That(player.VitalImpulseEnabled, Is.True);
        var passive = player.GetPassiveEffects().OfType<VitalImpulsePassive>().Single();
        Assert.That(player.GetPassiveEffects().OfType<VitalImpulsePassive>().Single(), Is.SameAs(passive));
        var enemy = CreateCharacter(false);
        Set(enemy, "isDying", true);
        session.ResetSession();
        Assert.That(player.VitalImpulseEnabled, Is.True);
        session.AbandonRun();
        Assert.That(player.VitalImpulseEnabled, Is.False);
        Assert.That(player.GetPassiveEffects().OfType<VitalImpulsePassive>(), Is.Empty);
        passive.OnOtherCharacterDeathCompleted(player, enemy);
        Assert.That(player.Stats.CurrentEnergy, Is.Zero);
    }

    [Test]
    public void RecreatedPlayerUsesOwnedUpgradeAndEnemiesNeverReceiveIt()
    {
        Buy();
        var player = CreateCharacter(true);
        Assert.That(player.VitalImpulseEnabled, Is.True);
        UnityEngine.Object.DestroyImmediate(player.gameObject);
        session.ResetSession();
        player = CreateCharacter(true);
        Assert.That(player.GetPassiveEffects().OfType<VitalImpulsePassive>().Count(), Is.EqualTo(1));
        var enemy = CreateCharacter(false, 2, 2);
        enemy.AddPassive(new VitalImpulsePassive());
        Assert.That(enemy.VitalImpulseEnabled, Is.False);
        Assert.That(enemy.GetPassiveEffects().OfType<VitalImpulsePassive>(), Is.Empty);
    }

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    public void LockedPassiveCannotRecoverEvenIfCalledDirectly(int width, int height)
    {
        var player = CreateCharacter(true);
        var enemy = CreateCharacter(false, width, height);
        Set(enemy, "isDying", true);
        int notifications = 0;
        GameEvents.OnEnergyRecovered += (_, _) => notifications++;
        new VitalImpulsePassive().OnOtherCharacterDeathCompleted(player, enemy);
        Call(player, "HandleCharacterDeathCompleted", enemy);
        Assert.That(player.Stats.CurrentEnergy, Is.Zero);
        Assert.That(notifications, Is.Zero);
    }

    [TestCase(0, 2)]
    [TestCase(6, 1)]
    [TestCase(7, 0)]
    public void PurchasedPassiveReportsOnlyActualRecoveryForFloatingText(int initialEnergy, int expectedRecovery)
    {
        var player = CreateCharacter(true);
        var enemy = CreateCharacter(false, 2, 2);
        player.Stats.SetCurrentEnergy(initialEnergy);
        Set(enemy, "isDying", true);
        int notifications = 0;
        GameEvents.OnEnergyRecovered += (owner, recovered) =>
        {
            notifications++;
            Assert.That(owner, Is.SameAs(player));
            Assert.That(recovered, Is.EqualTo(expectedRecovery));
        };
        Buy();
        player.AddPassive(new VitalImpulsePassive());
        Call(player, "HandleCharacterDeathCompleted", enemy);
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(initialEnergy + expectedRecovery));
        Assert.That(notifications, Is.EqualTo(expectedRecovery > 0 ? 1 : 0));
        session.AbandonRun();
        Call(player, "HandleCharacterDeathCompleted", enemy);
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(initialEnergy + expectedRecovery));
    }

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    public void DeathRoutineWaitsForDeathSequenceAndPublishesOnlyOnce(int width, int height)
    {
        var player = CreateCharacter(true);
        var enemy = CreateCharacter(false, width, height);
        var gridObject = new GameObject("Test grid");
        gridObject.SetActive(false);
        gridObject.transform.SetParent(root.transform, false);
        Set(enemy, "_grid", gridObject.AddComponent<GridSystem>());
        Set(enemy, "isDying", true);
        Buy();
        int completions = 0;
        GameEvents.OnCharacterDeathCompleted += deceased =>
        {
            completions++;
            Assert.That(deceased.gameObject.activeSelf, Is.True);
            Call(player, "HandleCharacterDeathCompleted", deceased);
        };
        var death = (IEnumerator)Call(enemy, "DieRoutine");
        Assert.That(death.MoveNext(), Is.True);
        Assert.That(player.Stats.CurrentEnergy, Is.Zero);
        Assert.That(completions, Is.Zero);
        // Exercise the existing no-Animator fallback. Real Animator timing is a PlayMode check.
        var wait = (IEnumerator)death.Current;
        while (wait.MoveNext()) { }
        Assert.That(player.Stats.CurrentEnergy, Is.Zero);
        Assert.That(death.MoveNext(), Is.False);
        Assert.That(enemy.gameObject.activeSelf, Is.False);
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(2));
        Assert.That(completions, Is.EqualTo(1));
        enemy.gameObject.SetActive(true);
        death = (IEnumerator)Call(enemy, "DieRoutine");
        Assert.That(death.MoveNext(), Is.True);
        wait = (IEnumerator)death.Current;
        while (wait.MoveNext()) { }
        Assert.That(death.MoveNext(), Is.False);
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(2));
    }
}
