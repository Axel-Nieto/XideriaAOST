using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FireUpgradePurchaseTests : UpgradeIntegrationFixture
{
    [TestCase(UpgradeType.FireballCooldown, UpgradeDefinition.FireballCooldownId)]
    [TestCase(UpgradeType.FlameDashCooldown, UpgradeDefinition.FlameDashCooldownId)]
    public void GoldPurchaseIsAtomicUsesOnlyGoldAndCannotBeRepeated(UpgradeType type, string id)
    {
        Configure(type, id);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 100);
        session.TryAddCurrency(RunCurrency.Gold, 19);
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.InsufficientGold));
        Assert.That(session.HasUpgrade(id), Is.False);
        Assert.That(session.Gold, Is.EqualTo(19));
        session.TryAddCurrency(RunCurrency.Gold, 1);
        int events = 0;
        Action<RunCurrency, int> onBalanceChanged = (currency, balance) =>
        {
            events++;
            Assert.That(currency, Is.EqualTo(RunCurrency.Gold));
            Assert.That(balance, Is.Zero);
            Assert.That(session.HasUpgrade(id), Is.True);
            Assert.That(session.TryPurchaseUpgrade(definition, out var duplicate), Is.False);
            Assert.That(duplicate, Is.EqualTo(UpgradePurchaseResult.AlreadyPurchased));
        };
        session.BalanceChanged += onBalanceChanged;
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.True);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.Success));
        Assert.That(events, Is.EqualTo(1));
        Assert.That(session.Gold, Is.Zero);
        Assert.That(session.XideriaFragments, Is.EqualTo(100));
        session.BalanceChanged -= onBalanceChanged;
        session.ResetSession();
        session.RunState.EnterScene("Level01");
        RunState restored = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(session.RunState));
        Assert.That(restored.HasUpgrade(id), Is.True);
        session.AbandonRun();
        Assert.That(session.HasUpgrade(id), Is.False);
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
    public void InvalidGoldPriceCannotUseTheLegacyFragmentPrice(int goldCost)
    {
        Configure(UpgradeType.FireballCooldown, UpgradeDefinition.FireballCooldownId);
        Set(definition, "goldCost", goldCost);
        session.TryAddCurrency(RunCurrency.Gold, 50);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 50);
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.InvalidCost));
        Assert.That(session.Gold, Is.EqualTo(50));
        Assert.That(session.XideriaFragments, Is.EqualTo(50));
        Assert.That(session.RunState.PurchasedUpgradeIds, Is.Empty);
    }

    [Test]
    public void MismatchedFireTypeAndIdCannotPurchase()
    {
        Configure(UpgradeType.FlameDashCooldown, UpgradeDefinition.FireballCooldownId);
        session.TryAddCurrency(RunCurrency.Gold, 50);
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.UnsupportedUpgrade));
        Assert.That(session.Gold, Is.EqualTo(50));
    }

    private void Configure(UpgradeType type, string id)
    {
        Set(definition, "upgradeType", type);
        Set(definition, "upgradeId", id);
        Set(definition, "goldCost", 20);
    }
}

public class FireUpgradeCombatTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character user;
    private SkillVFXLibrary previousVfx;

    [SetUp]
    public void CreateCombat()
    {
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 20); Set(grid, "height", 20);
        Call(grid, "GenerateGrid");
        previousVfx = SkillVFXLibrary.Instance;
        var vfx = Child("Empty VFX library").AddComponent<SkillVFXLibrary>();
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, vfx);
        user = Actor(true, new Vector2Int(3, 5));
    }

    [TearDown]
    public void RestoreVfx() => typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, previousVfx);

    [TestCase(false, 1, false, 2)]
    [TestCase(false, 2, true, 2)]
    [TestCase(true, 1, false, 2)]
    [TestCase(true, 2, false, 2)]
    [TestCase(true, 1, true, 1)]
    [TestCase(true, 2, true, 1)]
    public void FireballRequiresTwoDistinctEnemiesAndPurchase(bool purchased, int size, bool secondEnemy, int expected)
    {
        if (purchased) Buy(UpgradeType.FireballCooldown, UpgradeDefinition.FireballCooldownId);
        Character first = Actor(false, new Vector2Int(6, 5), size);
        Character second = secondEnemy ? Actor(false, new Vector2Int(8, 5)) : null;
        Character ally = Actor(true, new Vector2Int(6, 4));
        var skill = new FireballSkill();
        Drain(skill.Execute(user, new Vector2Int(6, 5), grid));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(expected));
        Assert.That(skill.Cooldown, Is.EqualTo(2));
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(96));
        Assert.That(first.Stats.CurrentHP, Is.EqualTo(86));
        if (second != null) Assert.That(second.Stats.CurrentHP, Is.EqualTo(86));
        Assert.That(ally.Stats.CurrentHP, Is.EqualTo(100));
    }

    [Test]
    public void FireballDoesNotCountDyingEnemiesOrGrantItsUpgradeToAnEnemyCaster()
    {
        Buy(UpgradeType.FireballCooldown, UpgradeDefinition.FireballCooldownId);
        Actor(false, new Vector2Int(6, 5));
        Character dying = Actor(false, new Vector2Int(8, 5));
        Set(dying, "isDying", true);
        var skill = new FireballSkill();
        Drain(skill.Execute(user, new Vector2Int(6, 5), grid));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(2));
        Set(user, "isPlayer", false);
        Actor(true, new Vector2Int(6, 4)); Actor(true, new Vector2Int(8, 4));
        user.SetSkillCooldown(skill, 0);
        Drain(skill.Execute(user, new Vector2Int(6, 5), grid));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(2));
    }

    [TestCase(false, 1, false, 2)] [TestCase(true, 1, false, 1)]
    [TestCase(true, 2, false, 1)] [TestCase(true, 1, true, 2)] [TestCase(true, 2, true, 2)]
    public void DashReducesOnlyWhenCompletedWithoutEnemyImpact(bool purchased, int enemySize, bool hitEnemy, int expected)
    {
        if (purchased) Buy(UpgradeType.FlameDashCooldown, UpgradeDefinition.FlameDashCooldownId);
        Vector2Int destination = new(6, 5);
        Character target = hitEnemy ? Actor(false, new Vector2Int(6, 6), enemySize) : null;
        if (!hitEnemy) Actor(true, new Vector2Int(6, 6));
        var skill = new FlameDashSkill();
        Assert.That(skill.CanUse(user, destination, grid), Is.True);
        Drain(skill.Execute(user, destination, grid), user);
        Assert.That(user.GridPosition, Is.EqualTo(destination));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(expected));
        Assert.That(skill.Cooldown, Is.EqualTo(2));
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(97));
        if (target != null) Assert.That(target.Stats.CurrentHP, Is.EqualTo(88));
        foreach (Vector2Int cell in user.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(user));
    }

    [TestCase(1)] [TestCase(2)]
    public void DashSupportsLargeUsersAndDoesNotRewardAnInterruptedOrInvalidMove(int size)
    {
        Buy(UpgradeType.FlameDashCooldown, UpgradeDefinition.FlameDashCooldownId);
        grid.UnregisterCharacter(user);
        Set(user, "gridWidth", size); Set(user, "gridHeight", size);
        grid.RegisterCharacter(user);
        var skill = new FlameDashSkill();
        Drain(skill.Execute(user, new Vector2Int(6, 5), grid), user);
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(1));
        user.SetSkillCooldown(skill, 0);
        user.ApplyStatusEffect(new RootEffect(1));
        Drain(skill.Execute(user, new Vector2Int(9, 5), grid), user);
        Assert.That(user.GridPosition, Is.EqualTo(new Vector2Int(6, 5)));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(2));
        user.SetSkillCooldown(skill, 0);
        grid.SetCellType(new Vector2Int(7 + size - 1, 5), CellType.Blocked);
        int energy = user.Stats.CurrentEnergy;
        Drain(skill.Execute(user, new Vector2Int(9, 5), grid), user);
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(energy));
        Assert.That(user.GetSkillCooldown(skill), Is.Zero);
    }

    [Test]
    public void EnemyDashNeverUsesThePlayersPurchasedUpgrade()
    {
        Buy(UpgradeType.FlameDashCooldown, UpgradeDefinition.FlameDashCooldownId);
        Set(user, "isPlayer", false);
        var skill = new FlameDashSkill();
        Drain(skill.Execute(user, new Vector2Int(6, 5), grid), user);
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(2));
        Assert.That(skill.GetTooltipDetails(user), Does.Not.Contain("reduce en 1"));
    }

    [Test]
    public void ReductionDoesNotCarryOverToTheNextFireballUse()
    {
        Buy(UpgradeType.FireballCooldown, UpgradeDefinition.FireballCooldownId);
        Actor(false, new Vector2Int(6, 5));
        Character other = Actor(false, new Vector2Int(8, 5));
        var skill = new FireballSkill();
        Drain(skill.Execute(user, new Vector2Int(6, 5), grid));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(1));
        grid.UnregisterCharacter(other);
        other.SetGridPosition(new Vector2Int(15, 15)); grid.RegisterCharacter(other);
        user.SetSkillCooldown(skill, 0);
        Drain(skill.Execute(user, new Vector2Int(6, 5), grid));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(2));
        Assert.That(skill.Cooldown, Is.EqualTo(2));
    }

    [Test]
    public void DescriptionsTrackOwnershipImmediatelyWithoutChangingSkillIdentity()
    {
        var fireball = new FireballSkill(); var dash = new FlameDashSkill();
        string fireballName = fireball.Name, dashName = dash.Name;
        Assert.That(fireball.GetTooltipDetails(user), Does.Not.Contain("reduce en 1"));
        Buy(UpgradeType.FireballCooldown, UpgradeDefinition.FireballCooldownId);
        Assert.That(fireball.GetTooltipDetails(user), Does.Contain("reduce en 1"));
        Assert.That(dash.GetTooltipDetails(user), Does.Not.Contain("reduce en 1"));
        Buy(UpgradeType.FlameDashCooldown, UpgradeDefinition.FlameDashCooldownId);
        Assert.That(dash.GetTooltipDetails(user), Does.Contain("reduce en 1"));
        user.StartCooldown(fireball); user.StartCooldown(dash);
        Assert.That(user.GetSkillCooldown(fireball), Is.EqualTo(2));
        Assert.That(user.GetSkillCooldown(dash), Is.EqualTo(2));
        session.AbandonRun();
        Assert.That(fireball.GetTooltipDetails(user), Does.Not.Contain("reduce en 1"));
        Assert.That(dash.GetTooltipDetails(user), Does.Not.Contain("reduce en 1"));
        Assert.That(fireball.Name, Is.EqualTo(fireballName)); Assert.That(dash.Name, Is.EqualTo(dashName));
    }

    private void Buy(UpgradeType type, string id)
    {
        Set(definition, "upgradeType", type); Set(definition, "upgradeId", id); Set(definition, "goldCost", 20);
        session.TryAddCurrency(RunCurrency.Gold, 20);
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
    }
    private GameObject Child(string name)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(root.transform, false); return go;
    }
    private Character Actor(bool player, Vector2Int cell, int size = 1)
    {
        var go = Child("Actor"); var actor = go.AddComponent<Character>(); actor.enabled = false;
        Set(actor, "baseStats", new CharacterStatsData { attack = 5, defense = 0, maxHP = 100, maxEnergy = 100, energyRegen = 0 });
        Set(actor, "isPlayer", player); Set(actor, "_grid", grid); Set(actor, "startGridPosition", cell);
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size);
        var movement = go.AddComponent<CharacterMovement>();
        Set(movement, "_grid", grid); Set(movement, "_character", actor);
        go.SetActive(true);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(5, 0, 100, 100, 0));
        typeof(Character).GetProperty(nameof(Character.Movement)).SetValue(actor, movement);
        actor.Stats.SetCurrentEnergy(100); actor.SetGridPosition(cell);
        actor.transform.position = grid.GridToWorld(cell, actor) + actor.VisualOffset;
        grid.RegisterCharacter(actor); return actor;
    }
    private void Drain(IEnumerator routine, Character moving = null)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        int iterations = 0;
        while (stack.Count > 0)
        {
            Assert.That(++iterations, Is.LessThan(200));
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator child) { stack.Push(child); continue; }
            // Advance presentation between real movement steps; logical commits stay in CharacterMovement.
            if (moving != null && top.Current == null)
                moving.transform.position = grid.GridToWorld(moving.GridPosition + Vector2Int.right, moving) + moving.VisualOffset;
        }
    }
}
