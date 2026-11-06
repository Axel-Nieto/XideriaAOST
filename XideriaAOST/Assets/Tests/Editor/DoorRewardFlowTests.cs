using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorRewardFlowTests : UpgradeIntegrationFixture
{
    private readonly List<Scene> scenes = new();
    private Scene previousActiveScene;

    [SetUp]
    public void RememberActiveScene() => previousActiveScene = SceneManager.GetActiveScene();

    [TearDown]
    public void CloseRooms()
    {
        foreach (Scene scene in scenes)
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        scenes.Clear();
        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);
    }

    [TestCase(RunCurrency.Gold, 1)] [TestCase(RunCurrency.Gold, 2)]
    [TestCase(RunCurrency.XideriaFragments, 1)] [TestCase(RunCurrency.XideriaFragments, 2)]
    public void PreviousRoomSignSelectionVictoryAndHudUseTheSameCurrency(RunCurrency currency, int size)
    {
        Scene source = NewRoom("RewardSource" + Guid.NewGuid().ToString("N"));
        string destinationName = "RewardDestination" + Guid.NewGuid().ToString("N");
        Sprite gold = Sprite("24c4ff6cc5e7f704c84be412cf0d8a67");
        Sprite fragments = Sprite("5b70c211105e9224fbeb7dee5791bb7b");
        GridSystem sourceGrid = Grid(source, destinationName, gold, fragments);
        Character actor = Actor(source, sourceGrid, true, size == 1 ? new Vector2Int(10, 5) : new Vector2Int(9, 5), size);
        RunRoomRecord sourceRoom = session.RunState.EnterScene(source.name);
        var settings = new DoorRewardSettings { GoldWeight = 1, XideriaFragmentsWeight = 1, GoldAmount = 10, XideriaFragmentsAmount = 10 };
        LogicalDoor door = LogicalDoor.Build(Get<List<TerrainCellData>>(sourceGrid, "terrainCells"))[0];
        int draws = 0;
        Assert.That(session.RunState.TryPrepareDoorOffer(sourceRoom.RoomId, door.Id,
            new[] { destinationName }, new[] { destinationName }, settings,
            () => ++draws == 1 ? 0 : currency == RunCurrency.Gold ? 0 : 0.75,
            out RunRewardRecord announced, out string error), Is.True, error);

        sourceGrid.SetSpecialCellsVisible(true);
        var sourceIcon = sourceGrid.GetComponentInChildren<DoorRewardIcon>();
        Assert.That(sourceIcon, Is.Not.Null);
        Assert.That(sourceIcon.GetComponent<SpriteRenderer>().sprite, Is.SameAs(currency == RunCurrency.Gold ? gold : fragments));
        Assert.That(sourceGrid.TryGetDoorOffer(actor, out RunRewardRecord selected), Is.True);
        Assert.That(selected, Is.SameAs(announced));
        Assert.That(session.RunState.TrySelectDoor(selected), Is.True);
        Assert.That(session.Gold + session.XideriaFragments, Is.Zero);
        EditorSceneManager.CloseScene(source, true);
        Scene destination = NewRoom(destinationName);

        // The next exit deliberately announces the opposite currency. Its offer
        // must not replace the reward carried in from the previous room.
        GridSystem destinationGrid = Grid(destination, "RewardNextRoom", gold, fragments);
        LogicalDoor nextDoor = LogicalDoor.Build(Get<List<TerrainCellData>>(destinationGrid, "terrainCells"))[0];
        draws = 0;
        Assert.That(session.RunState.TryPrepareDoorOffer(announced.RoomId, nextDoor.Id,
            new[] { "RewardNextRoom" }, new[] { "RewardNextRoom" }, settings,
            () => ++draws == 1 ? 0 : currency == RunCurrency.Gold ? 0.75 : 0,
            out RunRewardRecord next, out error), Is.True, error);
        Character player = Actor(destination, destinationGrid, true, new Vector2Int(2, 2));
        Character enemy = Actor(destination, destinationGrid, false, new Vector2Int(6, 6));
        var manager = ObjectInScene("State manager", destination).AddComponent<GameStateManager>();
        Set(manager, "gridSystem", destinationGrid);
        Call(manager, "Start");
        Assert.That(Get<string>(manager, "rewardRoomId"), Is.EqualTo(announced.RoomId));

        var hudObject = new GameObject("Receipt HUD", typeof(RectTransform));
        hudObject.transform.SetParent(root.transform, false);
        var hud = hudObject.AddComponent<CurrencyHUD>();
        TMP_Text goldText = Text("Gold", hudObject.transform);
        TMP_Text fragmentsText = Text("Fragments", hudObject.transform);
        Set(hud, "goldText", goldText); Set(hud, "xideriaFragmentsText", fragmentsText);
        Call(hud, "BindSession", session);
        RunCurrency? credited = null;
        session.BalanceChanged += (kind, amount) => { credited = kind; Assert.That(amount, Is.EqualTo(10)); };
        enemy.Stats.SetCurrentHP(0);
        Call(manager, "CheckVictory");

        Assert.That(credited, Is.EqualTo(currency));
        Assert.That(announced.IsDelivered, Is.True);
        Assert.That(next.IsDelivered, Is.False);
        Assert.That(session.GetBalance(currency), Is.EqualTo(10));
        Assert.That(session.GetBalance(currency == RunCurrency.Gold ? RunCurrency.XideriaFragments : RunCurrency.Gold), Is.Zero);
        Assert.That(goldText.text, Is.EqualTo(currency == RunCurrency.Gold ? "10" : "0"));
        Assert.That(fragmentsText.text, Is.EqualTo(currency == RunCurrency.XideriaFragments ? "10" : "0"));
        Call(manager, "CheckVictory");
        Assert.That(session.GetBalance(currency), Is.EqualTo(10));
        Call(hud, "OnDestroy");
    }

    private Scene NewRoom(string prefix)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                Assert.Ignore("Save untitled scenes before running the room-flow integration tests.");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = prefix;
        scenes.Add(scene); return scene;
    }
    private static GameObject ObjectInScene(string name, Scene scene)
    {
        var go = new GameObject(name); go.SetActive(false); SceneManager.MoveGameObjectToScene(go, scene); return go;
    }
    private GridSystem Grid(Scene scene, string target, Sprite gold, Sprite fragments)
    {
        var go = ObjectInScene("Grid", scene);
        var grid = go.AddComponent<GridSystem>();
        var cells = new List<TerrainCellData>();
        foreach (int y in new[] { 5, 6 })
        {
            var cell = new TerrainCellData();
            Set(cell, "position", new Vector2Int(10, y)); Set(cell, "type", CellType.ExitRun);
            Set(cell, "targetScenes", new List<string> { target }); cells.Add(cell);
        }
        Set(grid, "terrainCells", cells); Set(grid, "combatRewardScenes", new List<string> { target });
        Set(grid, "doorRewards", new DoorRewardSettings { GoldWeight = 1, XideriaFragmentsWeight = 1, GoldAmount = 10, XideriaFragmentsAmount = 10 });
        Set(grid, "goldRewardSprite", gold); Set(grid, "xideriaFragmentsRewardSprite", fragments);
        Call(grid, "Awake");
        go.SetActive(true);
        return grid;
    }
    private static Character Actor(Scene scene, GridSystem grid, bool player, Vector2Int cell, int size = 1)
    {
        var go = ObjectInScene(player ? "Player" : "Enemy", scene);
        var actor = go.AddComponent<Character>(); actor.enabled = false;
        Set(actor, "_grid", grid); Set(actor, "isPlayer", player); Set(actor, "startGridPosition", cell);
        Set(actor, "baseStats", new CharacterStatsData { maxHP = 100, maxEnergy = 100 });
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size);
        go.SetActive(true);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(5, 0, 100, 100, 0));
        actor.SetGridPosition(cell); grid.RegisterCharacter(actor); return actor;
    }
    private static Sprite Sprite(string guid)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
        Assert.That(sprite, Is.Not.Null); return sprite;
    }
    private static TMP_Text Text(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false); return go.GetComponent<TMP_Text>();
    }
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
}
