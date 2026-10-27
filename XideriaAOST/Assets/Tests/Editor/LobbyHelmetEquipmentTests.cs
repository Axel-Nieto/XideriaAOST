using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class LobbyHelmetEquipmentTests : UpgradeIntegrationFixture
{
    private Character player;
    private HelmetCatalog catalog;
    private HelmetInventoryData inventory;
    private HelmetCatalog previousCatalog;
    private HelmetInventoryData previousInventory;

    [SetUp]
    public void CreateHelmets()
    {
        previousCatalog = HelmetCatalog.Instance;
        previousInventory = HelmetInventoryData.Instance;
        catalog = Child("Helmet catalog").AddComponent<HelmetCatalog>();
        inventory = Child("Helmet inventory").AddComponent<HelmetInventoryData>();
        typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, catalog);
        typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, inventory);
        Set(catalog, "helmets", new List<HelmetCatalogEntry>
        {
            new() { Type = HelmetType.Fire, MaxCharges = 5 },
            new() { Type = HelmetType.Water, MaxCharges = 4 },
            new() { Type = HelmetType.Rock, MaxCharges = 3 }
        });
        Call(inventory, "InitializeInventory");
        foreach (HelmetType type in new[] { HelmetType.Fire, HelmetType.Water, HelmetType.Rock })
        {
            inventory.UnlockHelmet(type);
            Assert.That(inventory.AddHelmet(type, 2), Is.True);
        }

        player = Child("Player").AddComponent<Character>();
        Set(player, "isPlayer", true);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(player, new CharacterStats(5, 2, 20, 7, 3));
        player.UnequipHelmet();
    }

    [TearDown]
    public void RestoreHelmetSingletons()
    {
        typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, previousCatalog);
        typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, previousInventory);
    }

    [TestCase(HelmetType.Fire)]
    [TestCase(HelmetType.Water)]
    [TestCase(HelmetType.Rock)]
    public void LobbyRejectsInventoryAndDirectEquipmentWithoutConsumingHelmets(HelmetType type)
    {
        RenameScene("Lobby");
        HelmetData original = player.EquippedHelmet;

        Assert.That(player.EquipHelmetFromInventory(type), Is.False);
        player.EquipHelmet(catalog.CreateHelmet(type));

        Assert.That(player.CanEquipHelmet, Is.False);
        Assert.That(player.EquippedHelmet, Is.SameAs(original));
        Assert.That(player.HelmetCharges, Is.Zero);
        Assert.That(inventory.GetAmount(type), Is.EqualTo(2));
        Assert.That(player.GetActiveSkills(), Has.Count.EqualTo(1));
        Assert.That(player.GetSkill(0), Is.TypeOf<BasicAttackSkill>());
        Assert.That(player.GetPassiveEffects(), Is.Empty);
    }

    [TestCase("Tutorial", HelmetType.Fire)]
    [TestCase("Level01", HelmetType.Water)]
    [TestCase("LevelBossZ1", HelmetType.Fire)]
    public void OtherMapsStillEquipAndConsumeOneHelmet(string sceneName, HelmetType type)
    {
        RenameScene(sceneName);

        Assert.That(player.CanEquipHelmet, Is.True);
        Assert.That(player.EquipHelmetFromInventory(type), Is.True);
        Assert.That(player.EquippedHelmet.Type, Is.EqualTo(type));
        Assert.That(player.HelmetCharges, Is.EqualTo(catalog.GetEntry(type).MaxCharges));
        Assert.That(inventory.GetAmount(type), Is.EqualTo(1));
        Assert.That(player.GetActiveSkills(), Has.Count.EqualTo(4));
    }

    [TestCase("Lobby", HelmetType.Default, 0)]
    [TestCase("Level01", HelmetType.Fire, 3)]
    public void SessionRestoresStatsButOnlyRestoresHelmetOutsideLobby(string sceneName, HelmetType expected, int charges)
    {
        RenameScene(sceneName);
        session.forceNewRun = false;
        session.hasSavedData = true;
        session.currentHP = 12;
        session.currentEnergy = 4;
        session.hasHelmet = true;
        session.equippedHelmetType = HelmetType.Fire;
        session.equippedHelmetCharges = 3;

        player.LoadPlayerSession();

        Assert.That(player.EquippedHelmet.Type, Is.EqualTo(expected));
        Assert.That(player.HelmetCharges, Is.EqualTo(charges));
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(12));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(4));
        Assert.That(inventory.GetAmount(HelmetType.Fire), Is.EqualTo(2));
    }

    [TestCase("Lobby", false)]
    [TestCase("Level01", true)]
    public void InventorySlotReflectsEquipmentAvailability(string sceneName, bool enabled)
    {
        RenameScene(sceneName);
        GameObject slotObject = Child("Fire helmet slot");
        Button button = slotObject.AddComponent<Button>();
        HelmetSlotUI slot = slotObject.AddComponent<HelmetSlotUI>();
        Set(slot, "helmetType", HelmetType.Fire);
        Set(slot, "button", button);
        slot.Initialize(player);

        slot.Refresh();

        Assert.That(button.interactable, Is.EqualTo(enabled));
        if (!enabled)
        {
            slot.SelectHelmet();
            Assert.That(player.EquippedHelmet.Type, Is.EqualTo(HelmetType.Default));
            Assert.That(inventory.GetAmount(HelmetType.Fire), Is.EqualTo(2));
        }
    }

    [Test]
    public void LobbyCheckIgnoresCaseAndUpdatesWhenTheCharacterChangesScene()
    {
        RenameScene("lObBy");
        Assert.That(player.CanEquipHelmet, Is.False);
        RenameScene("Level01");
        Assert.That(player.EquipHelmetFromInventory(HelmetType.Fire), Is.True);
    }

    [Test]
    public void LobbyRestrictionDoesNotChangeEnemyEquipment()
    {
        RenameScene("Lobby");
        Character enemy = Child("Large enemy").AddComponent<Character>();
        Set(enemy, "gridWidth", 2);
        Set(enemy, "gridHeight", 2);
        HelmetData helmet = catalog.CreateHelmet(HelmetType.Water);

        enemy.EquipHelmet(helmet);

        Assert.That(enemy.EquippedHelmet, Is.SameAs(helmet));
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.SetActive(false);
        child.transform.SetParent(root.transform, false);
        return child;
    }

    private void RenameScene(string name)
    {
        var scene = root.scene;
        scene.name = name;
    }
}
