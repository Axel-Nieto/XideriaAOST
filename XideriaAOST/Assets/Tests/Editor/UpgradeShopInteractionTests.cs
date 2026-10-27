using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class UpgradeShopInteractionTests : UpgradeIntegrationFixture
{
    private Character player;
    private CharacterMovement movement;
    private GridSystem grid;
    private GridInputHandler input;
    private GameStateManager gameState;
    private UpgradeShopUI shop;
    private UpgradeShopInteraction interaction;
    private bool previousGameOver;

    [SetUp]
    public void CreateInteraction()
    {
        previousGameOver = GameSession.IsGameOver;
        GameSession.IsGameOver = false;
        var scene = root.scene;
        scene.name = "Lobby";
        var gridObject = Child("Test grid");
        grid = gridObject.AddComponent<GridSystem>();
        Set(grid, "width", 8); Set(grid, "height", 8); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        grid.SetCellType(new Vector2Int(1, 1), CellType.Shop);
        var playerObject = Child("Test Sir Tete");
        player = playerObject.AddComponent<Character>();
        player.enabled = false;
        Set(player, "isPlayer", true);
        Set(player, "baseStats", new CharacterStatsData());
        Set(player, "_grid", grid);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(player, new CharacterStats(5, 2, 20, 7, 3));
        movement = playerObject.AddComponent<CharacterMovement>();
        Set(movement, "_character", player); Set(movement, "_grid", grid);
        playerObject.SetActive(true);
        var inputObject = Child("Test input");
        input = inputObject.AddComponent<GridInputHandler>();
        Set(input, "playerCharacter", player); Set(input, "gridSystem", grid); Set(input, "isPlayerTurn", true);
        gameState = inputObject.AddComponent<GameStateManager>();
        typeof(GameStateManager).GetProperty(nameof(GameStateManager.CurrentPhase)).SetValue(gameState, LevelPhase.Exploration);
        inputObject.SetActive(true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UpgradeShop.prefab");
        Assert.That(prefab, Is.Not.Null, "Installed shop prefab is required for this integration test.");
        var shopObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        shop = shopObject.GetComponent<UpgradeShopUI>();
        Call(shop, "Awake");
        var interactionObject = Child("Shop interaction");
        interaction = interactionObject.AddComponent<UpgradeShopInteraction>();
        Set(interaction, "playerCharacter", player); Set(interaction, "gridSystem", grid);
        Set(interaction, "inputHandler", input); Set(interaction, "gameStateManager", gameState); Set(interaction, "shop", shop);
        interactionObject.SetActive(true);
        // Explicit hooks avoid relying on EditMode's MonoBehaviour ordering.
        Call(interaction, "OnEnable");
    }

    [TearDown]
    public void DestroyInteraction()
    {
        if (interaction != null) Call(interaction, "OnDisable");
        if (shop != null) shop.Close();
        GameSession.IsGameOver = previousGameOver;
    }

    [Test]
    public void InitialEnableDoesNotOpenShopEvenWhenPlayerStartsOnShop()
    {
        player.SetGridPosition(new Vector2Int(1, 1));
        Call(interaction, "OnEnable");
        Assert.That(shop.IsOpen, Is.False);
        Assert.That(input.IsUpgradeShopInputBlocked, Is.False);
    }

    [TestCase(1, 1, 1, 1, true)]
    [TestCase(1, 1, 2, 1, false)]
    [TestCase(2, 2, 0, 0, true)]
    [TestCase(2, 2, 2, 2, false)]
    public void FinalDestinationUsesTheWholeFootprint(int width, int height, int x, int y, bool expected)
    {
        Set(player, "gridWidth", width); Set(player, "gridHeight", height);
        player.SetGridPosition(new Vector2Int(x, y));
        Assert.That(Call(interaction, "IsShopDestination", player), Is.EqualTo(expected));
        Set(player, "isPlayer", false);
        Assert.That(Call(interaction, "IsShopDestination", player), Is.False);
    }

    [Test]
    public void WaitsForActionAndThenOpensAndLocksControls()
    {
        player.SetGridPosition(new Vector2Int(1, 1));
        Set(input, "isMoving", true);
        var wait = (IEnumerator)Call(interaction, "OpenAfterMovement", player, player.GridPosition);
        Assert.That(wait.MoveNext(), Is.True);
        Assert.That(wait.MoveNext(), Is.True);
        Assert.That(shop.IsOpen, Is.False);
        Set(input, "isMoving", false);
        Assert.That(wait.MoveNext(), Is.False);
        Assert.That(shop.IsOpen, Is.True);
        Assert.That(input.IsUpgradeShopInputBlocked, Is.True);
        Assert.That(input.CanOpenHelmetInventory(), Is.False);
        input.EndTurnFromUI();
        input.SelectSkillFromUI(0);
        Assert.That(player.IsTurnFinished, Is.False);
        shop.Close();
        Assert.That(shop.IsOpen, Is.False);
        Assert.That(input.IsUpgradeShopInputBlocked, Is.True, "The closing click is blocked for its frame.");
        Set(input, "upgradeShopClosedFrame", -1);
        Assert.That(input.IsUpgradeShopInputBlocked, Is.False);
        Assert.That(input.CanOpenHelmetInventory(), Is.True);
    }

    [TestCase("moved")]
    [TestCase("dead")]
    [TestCase("combat")]
    [TestCase("inventory")]
    [TestCase("gameover")]
    public void DeferredOpenRejectsStaleOrInvalidState(string invalidation)
    {
        player.SetGridPosition(new Vector2Int(1, 1));
        var wait = (IEnumerator)Call(interaction, "OpenAfterMovement", player, player.GridPosition);
        Assert.That(wait.MoveNext(), Is.True);
        switch (invalidation)
        {
            case "moved": player.SetGridPosition(new Vector2Int(2, 1)); break;
            case "dead": player.Stats.SetCurrentHP(0); break;
            case "combat": typeof(GameStateManager).GetProperty(nameof(GameStateManager.CurrentPhase)).SetValue(gameState, LevelPhase.Combat); break;
            case "inventory": Set(input, "isHelmetInventoryOpen", true); break;
            case "gameover": GameSession.IsGameOver = true; break;
        }
        Assert.That(wait.MoveNext(), Is.False);
        Assert.That(shop.IsOpen, Is.False);
        Assert.That(input.IsUpgradeShopInputBlocked, Is.False);
    }

    [Test]
    public void DisablingInteractionClosesShopReleasesLockAndUnsubscribes()
    {
        player.SetGridPosition(new Vector2Int(1, 1));
        shop.Open();
        Assert.That(shop.IsOpen, Is.True);
        Assert.That(input.IsUpgradeShopInputBlocked, Is.True);
        Call(interaction, "OnDisable");
        Assert.That(shop.IsOpen, Is.False);
        Set(input, "upgradeShopClosedFrame", -1);
        Assert.That(input.IsUpgradeShopInputBlocked, Is.False);
        Assert.That(CountListeners(movement, "MovementCompleted", interaction), Is.Zero);
        Assert.That(CountListeners(shop, "OpenStateChanged", interaction), Is.Zero);
    }

    [Test]
    public void RepeatedEnableKeepsOneSubscriptionPerEvent()
    {
        Call(interaction, "OnEnable");
        Call(interaction, "OnEnable");
        Assert.That(CountListeners(movement, "MovementCompleted", interaction), Is.EqualTo(1));
        Assert.That(CountListeners(shop, "OpenStateChanged", interaction), Is.EqualTo(1));
    }

    [Test]
    public void CrossingShopDoesNotNotifyCompletionUntilTheFinalStep()
    {
        // Observe the real movement coroutine without starting an EditMode coroutine.
        Call(interaction, "OnDisable");
        int completions = 0;
        movement.MovementCompleted += actor =>
        {
            completions++;
            Assert.That(actor.GridPosition, Is.EqualTo(new Vector2Int(2, 1)));
        };
        player.transform.position = grid.GridToWorld(new Vector2Int(1, 1), player) + player.VisualOffset;
        var path = new List<Vector2Int> { new(1, 1), new(2, 1) };
        var routine = (IEnumerator)Call(movement, "FollowPath", path, null, null);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(player.GridPosition, Is.EqualTo(new Vector2Int(1, 1)));
        Assert.That(completions, Is.Zero);
        Assert.That(shop.IsOpen, Is.False);
        player.transform.position = grid.GridToWorld(new Vector2Int(2, 1), player) + player.VisualOffset;
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(Call(interaction, "IsShopDestination", player), Is.False);
    }

    [Test]
    public void RejectedMovementDoesNotPublishCompletion()
    {
        Call(interaction, "OnDisable");
        int completions = 0;
        movement.MovementCompleted += _ => completions++;
        var routine = (IEnumerator)Call(movement, "FollowPath", new List<Vector2Int> { new(1, 1) }, null, (System.Func<bool>)(() => false));
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(completions, Is.Zero);
        Assert.That(player.GridPosition, Is.EqualTo(Vector2Int.zero));
    }

    [Test]
    public void InterruptedMovementDoesNotPublishCompletionOrCommitTheUnfinishedStep()
    {
        Call(interaction, "OnDisable");
        int completions = 0;
        movement.MovementCompleted += _ => completions++;
        var routine = (IEnumerator)Call(movement, "FollowPath", new List<Vector2Int> { new(1, 1) }, null, null);
        Assert.That(routine.MoveNext(), Is.True);
        movement.enabled = false;
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(completions, Is.Zero);
        Assert.That(player.GridPosition, Is.EqualTo(Vector2Int.zero));
    }

    private GameObject Child(string name)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(root.transform, false); return go;
    }

    private static int CountListeners(object owner, string field, object target)
    {
        var handlers = (System.Delegate)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        return handlers == null ? 0 : handlers.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, target));
    }
}
