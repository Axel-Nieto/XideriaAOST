using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TeteUpgradeTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character player;
    private HelmetCatalog previousCatalog;
    private HelmetInventoryData previousInventory;
    private SkillVFXLibrary previousVfx;
    private HelmetCatalog catalog;
    private HelmetInventoryData inventory;
    private readonly List<Character> actors = new();

    [SetUp]
    public void CreateCharacters()
    {
        actors.Clear();
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 20); Set(grid, "height", 20); Call(grid, "GenerateGrid");
        previousCatalog = HelmetCatalog.Instance;
        previousInventory = HelmetInventoryData.Instance;
        previousVfx = SkillVFXLibrary.Instance;
        catalog = Child("Catalog").AddComponent<HelmetCatalog>();
        inventory = Child("Inventory").AddComponent<HelmetInventoryData>();
        var vfx = Child("Empty VFX").AddComponent<SkillVFXLibrary>();
        typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, catalog);
        typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, inventory);
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, vfx);
        Set(catalog, "helmets", new List<HelmetCatalogEntry>
        {
            new() { Type = HelmetType.Fire, MaxCharges = 3 },
            new() { Type = HelmetType.Water, MaxCharges = 3 },
            new() { Type = HelmetType.Rock, MaxCharges = 3 }
        });
        Call(inventory, "InitializeInventory");
        foreach (HelmetType type in new[] { HelmetType.Fire, HelmetType.Water, HelmetType.Rock })
        {
            inventory.UnlockHelmet(type); inventory.AddHelmet(type, 2);
        }
        player = Actor(true, new Vector2Int(1, 1));
    }

    [TearDown]
    public void RemoveSubscriptions()
    {
        foreach (Character actor in actors) if (actor != null) Call(actor, "OnDisable");
        typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, previousCatalog);
        typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, previousInventory);
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, previousVfx);
    }

    [TestCase(UpgradeType.MaxHealth, UpgradeDefinition.MaxHealthId)]
    [TestCase(UpgradeType.BaseDefense, UpgradeDefinition.BaseDefenseId)]
    [TestCase(UpgradeType.BaseDamage, UpgradeDefinition.BaseDamageId)]
    [TestCase(UpgradeType.HelmetDurability, UpgradeDefinition.HelmetDurabilityId)]
    public void PurchasesUseTwentyFragmentsOnceAndNeverSpendGold(UpgradeType type, string id)
    {
        Configure(type, id);
        session.TryAddCurrency(RunCurrency.Gold, 100);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 19);
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.InsufficientFragments));
        Assert.That(session.HasUpgrade(id), Is.False);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 1);
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.AlreadyPurchased));
        Assert.That(session.XideriaFragments, Is.Zero);
        Assert.That(session.Gold, Is.EqualTo(100));
        Assert.That(definition.Currency, Is.EqualTo(RunCurrency.XideriaFragments));
        Assert.That(definition.Cost, Is.EqualTo(20));
    }

    [Test]
    public void PurchasesApplyImmediatelyWithoutHealingAndRepeatedRefreshDoesNotStack()
    {
        player.Stats.SetCurrentHP(9);
        Buy(UpgradeType.MaxHealth, UpgradeDefinition.MaxHealthId);
        Buy(UpgradeType.BaseDefense, UpgradeDefinition.BaseDefenseId);
        Buy(UpgradeType.BaseDamage, UpgradeDefinition.BaseDamageId);
        for (int i = 0; i < 5; i++) Call(player, "RefreshRunUpgradeBonuses");
        AssertStats(player, 7, 3, 22, 9);
        Call(player, "OnDisable"); Call(player, "OnEnable");
        AssertStats(player, 7, 3, 22, 9);
        Assert.That(player.Stats.MaxEnergy, Is.EqualTo(7));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(7));
        Assert.That(player.Stats.EnergyRegen, Is.EqualTo(3));
    }

    [TestCase(1)] [TestCase(2)]
    public void EnemyStatsNeverReceivePlayerBonuses(int size)
    {
        BuyStats();
        Character enemy = Actor(false, new Vector2Int(5, 5), size);
        Call(enemy, "RefreshRunUpgradeBonuses");
        AssertStats(enemy, 5, 2, 20, 20);
        foreach (Vector2Int cell in enemy.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(enemy));
    }

    [Test]
    public void SavedHpAboveTheOldMaximumLoadsAfterBonusesAndDoesNotGrowBetweenRooms()
    {
        BuyStats(); player.Stats.SetCurrentHP(21); player.SavePlayerSession();
        RunState restored = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(session.RunState));
        Set(session, "runState", restored);
        Character recreated = Actor(true, new Vector2Int(5, 5));
        AssertStats(recreated, 7, 3, 22, 22);
        recreated.LoadPlayerSession(); recreated.LoadPlayerSession();
        AssertStats(recreated, 7, 3, 22, 21);
        session.ResetSession();
        Character fresh = Actor(true, new Vector2Int(8, 8));
        Call(fresh, "Start");
        AssertStats(fresh, 7, 3, 22, 22);
        session.AbandonRun();
        AssertStats(recreated, 5, 2, 20, 20);
        AssertStats(fresh, 5, 2, 20, 20);
    }

    [Test]
    public void LateSessionArrivalAppliesBonusesOnceAndFreshSpawnStartsFull()
    {
        BuyStats();
        SetSession(null); RaiseSession(null);
        Character late = Actor(true, new Vector2Int(6, 6));
        AssertStats(late, 5, 2, 20, 20);
        SetSession(session); RaiseSession(session); RaiseSession(session);
        Call(late, "Start");
        AssertStats(late, 7, 3, 22, 22);
    }

    [Test]
    public void HealthPurchaseRefreshesTheDisplayedMaximumWithoutHealing()
    {
        var hud = Child("HUD").AddComponent<BattleHUD>();
        Set(hud, "currentCharacter", player); Set(hud, "characterPanel", Child("Panel"));
        foreach (string field in new[] { "hpCurrentText", "hpMaxText", "energyCurrentText", "energyMaxText" })
        {
            var text = new GameObject(field, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            text.SetActive(false); text.transform.SetParent(root.transform, false);
            Set(hud, field, text.GetComponent<TMPro.TextMeshProUGUI>());
        }
        player.Stats.SetCurrentHP(15);
        Call(hud, "Update");
        Buy(UpgradeType.MaxHealth, UpgradeDefinition.MaxHealthId);
        Call(hud, "Update");
        Assert.That(((TMPro.TMP_Text)typeof(BattleHUD).GetField("hpMaxText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud)).text, Is.EqualTo("22"));
        Assert.That(((TMPro.TMP_Text)typeof(BattleHUD).GetField("hpCurrentText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud)).text, Is.EqualTo("15"));
    }

    [TestCase(HelmetType.Fire)] [TestCase(HelmetType.Water)] [TestCase(HelmetType.Rock)]
    public void DurabilityOnlyBonusesFreshEquipmentAndDoesNotRefillOnPurchaseOrSessionLoad(HelmetType type)
    {
        Assert.That(player.EquipHelmetFromInventory(type), Is.True);
        Call(player, "ProcessHelmetCharges"); Call(player, "ProcessHelmetCharges");
        Assert.That(player.HelmetCharges, Is.EqualTo(1));
        Buy(UpgradeType.HelmetDurability, UpgradeDefinition.HelmetDurabilityId);
        Assert.That(player.HelmetCharges, Is.EqualTo(1));
        player.SavePlayerSession();
        Character restored = Actor(true, new Vector2Int(6, 6));
        restored.LoadPlayerSession(); restored.LoadPlayerSession();
        Assert.That(restored.HelmetCharges, Is.EqualTo(1));
        Assert.That(inventory.GetAmount(type), Is.EqualTo(1));
        Assert.That(restored.EquipHelmetFromInventory(type), Is.True);
        Assert.That(restored.HelmetCharges, Is.EqualTo(4));
        Assert.That(catalog.GetEntry(type).MaxCharges, Is.EqualTo(3));
        Assert.That(inventory.GetAmount(type), Is.Zero);
        Call(restored, "ProcessHelmetCharges");
        Assert.That(restored.HelmetCharges, Is.EqualTo(3));
    }

    [Test]
    public void DurabilityKeepsLobbyRestrictionAndCannotOverflow()
    {
        Buy(UpgradeType.HelmetDurability, UpgradeDefinition.HelmetDurabilityId);
        var scene = root.scene; string original = scene.name; scene.name = "Lobby";
        Assert.That(player.EquipHelmetFromInventory(HelmetType.Fire), Is.False);
        Assert.That(inventory.GetAmount(HelmetType.Fire), Is.EqualTo(2));
        scene.name = original;
        catalog.GetEntry(HelmetType.Fire).MaxCharges = int.MaxValue;
        Assert.That(player.EquipHelmetFromInventory(HelmetType.Fire), Is.True);
        Assert.That(player.HelmetCharges, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void DamageAndDefenseBonusesAffectRealSkillDamage()
    {
        Character enemy = Actor(false, new Vector2Int(2, 1), 2);
        var skill = new BasicAttackSkill();
        Drain(skill.Execute(player, enemy.GridPosition, grid));
        int before = 20 - enemy.Stats.CurrentHP;
        enemy.Stats.SetCurrentHP(20); player.Stats.SetCurrentEnergy(7);
        Buy(UpgradeType.BaseDamage, UpgradeDefinition.BaseDamageId);
        Drain(skill.Execute(player, enemy.GridPosition, grid));
        Assert.That(20 - enemy.Stats.CurrentHP, Is.EqualTo(before + 2));
        enemy.Stats.SetCurrentEnergy(7);
        Drain(skill.Execute(enemy, player.GridPosition, grid));
        before = 20 - player.Stats.CurrentHP;
        player.Stats.SetCurrentHP(20); enemy.Stats.SetCurrentEnergy(7);
        Buy(UpgradeType.BaseDefense, UpgradeDefinition.BaseDefenseId);
        Drain(skill.Execute(enemy, player.GridPosition, grid));
        Assert.That(20 - player.Stats.CurrentHP, Is.EqualTo(before - 1));
    }

    private void BuyStats()
    {
        Buy(UpgradeType.MaxHealth, UpgradeDefinition.MaxHealthId);
        Buy(UpgradeType.BaseDefense, UpgradeDefinition.BaseDefenseId);
        Buy(UpgradeType.BaseDamage, UpgradeDefinition.BaseDamageId);
    }
    private void Configure(UpgradeType type, string id)
    {
        Set(definition, "upgradeType", type); Set(definition, "upgradeId", id); Set(definition, "fragmentCost", 20);
    }
    private void Buy(UpgradeType type, string id)
    {
        Configure(type, id); session.TryAddCurrency(RunCurrency.XideriaFragments, 20);
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
    }
    private Character Actor(bool isPlayer, Vector2Int cell, int size = 1)
    {
        var actor = Child("Actor").AddComponent<Character>(); actor.enabled = false;
        Set(actor, "isPlayer", isPlayer); Set(actor, "_grid", grid); Set(actor, "startGridPosition", cell);
        Set(actor, "baseStats", new CharacterStatsData()); Set(actor, "gridWidth", size); Set(actor, "gridHeight", size);
        Call(actor, "Awake"); actor.Stats.SetCurrentEnergy(7); Call(actor, "OnEnable");
        actors.Add(actor); return actor;
    }
    private GameObject Child(string name)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(root.transform, false); return go;
    }
    private static void AssertStats(Character actor, int attack, int defense, int maxHp, int hp)
    {
        Assert.That(actor.Stats.Attack, Is.EqualTo(attack)); Assert.That(actor.Stats.Defense, Is.EqualTo(defense));
        Assert.That(actor.Stats.MaxHP, Is.EqualTo(maxHp)); Assert.That(actor.Stats.CurrentHP, Is.EqualTo(hp));
    }
    private static void Drain(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine); int count = 0;
        while (stack.Count > 0)
        {
            Assert.That(++count, Is.LessThan(100)); var top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator child) stack.Push(child);
        }
    }
    private static void SetSession(PlayerSessionData value) => typeof(PlayerSessionData).GetProperty(nameof(PlayerSessionData.Instance)).SetValue(null, value);
    private static void RaiseSession(PlayerSessionData value) => ((Action<PlayerSessionData>)typeof(PlayerSessionData).GetField("InstanceChanged", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))?.Invoke(value);
}
