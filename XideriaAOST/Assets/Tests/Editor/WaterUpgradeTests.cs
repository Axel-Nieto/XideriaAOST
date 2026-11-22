using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WaterUpgradeTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character user;
    private SkillVFXLibrary previousVfx;
    private readonly List<Character> actors = new();

    [SetUp]
    public void CreateCombat()
    {
        actors.Clear();
        previousVfx = SkillVFXLibrary.Instance;
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null,
            Child("Empty VFX").AddComponent<SkillVFXLibrary>());
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 40); Set(grid, "height", 40); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        user = Actor(true, new Vector2Int(12, 12));
        user.EquipHelmet(HelmetFactory.CreateWaterHelmet());
    }

    [TearDown]
    public void RestoreEffects() => typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance))
        .SetValue(null, previousVfx);

    [TestCase(UpgradeType.WaterWavePush, UpgradeDefinition.WaterWavePushId, 20)]
    [TestCase(UpgradeType.WaterPrisonGlobal, UpgradeDefinition.WaterPrisonGlobalId, 30)]
    public void PurchasesUseExactGoldCostsPersistAndCannotBeRepeated(UpgradeType type, string id, int cost)
    {
        Configure(type);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 100);
        session.TryAddCurrency(RunCurrency.Gold, cost - 1);
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.InsufficientGold));
        Assert.That(session.HasUpgrade(id), Is.False);
        session.TryAddCurrency(RunCurrency.Gold, 1);
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.True);
        Assert.That(session.Gold, Is.Zero);
        Assert.That(session.XideriaFragments, Is.EqualTo(100));
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.AlreadyPurchased));
        session.ResetSession();
        Assert.That(JsonUtility.FromJson<RunState>(JsonUtility.ToJson(session.RunState)).HasUpgrade(id), Is.True);
        session.AbandonRun();
        Assert.That(user.GetPassiveEffects().OfType<WaterUpgradePassive>(), Is.Empty);
    }

    [TestCase(1, 1, 1, 0)] [TestCase(1, 2, 1, 0)] [TestCase(2, 1, 1, 0)] [TestCase(2, 2, 1, 0)]
    [TestCase(1, 1, -1, 0)] [TestCase(1, 2, -1, 0)] [TestCase(2, 1, -1, 0)] [TestCase(2, 2, -1, 0)]
    [TestCase(1, 1, 0, 1)] [TestCase(1, 2, 0, 1)] [TestCase(2, 1, 0, 1)] [TestCase(2, 2, 0, 1)]
    [TestCase(1, 1, 0, -1)] [TestCase(1, 2, 0, -1)] [TestCase(2, 1, 0, -1)] [TestCase(2, 2, 0, -1)]
    public void WavePushesTheWholeEnemyOneCellBeyondItsLeadingRangeAndHitsOnce(int userSize, int enemySize, int dx, int dy)
    {
        Buy(UpgradeType.WaterWavePush);
        Resize(user, userSize);
        Vector2Int direction = new(dx, dy), origin = user.GridPosition;
        Character enemy = Actor(false, AtTrailing(origin, direction, 3, enemySize), enemySize);
        int limit = 7 + (dx > 0 || dy > 0 ? userSize - 1 : 0);
        Vector2Int destination = AtTrailing(origin, direction, limit, enemySize);
        var skill = new WaterWaveSkill();
        Assert.That(skill.CanUse(user, origin + direction * 6, grid), Is.True);
        var preview = skill.GetPreviewCells(user, origin, origin + direction * 6, grid).ToHashSet();
        foreach (Vector2Int cell in enemy.GetOccupiedCells(destination)) Assert.That(preview, Does.Contain(cell));
        Assert.That(enemy.GridPosition, Is.EqualTo(AtTrailing(origin, direction, 3, enemySize)), "Preview must not move enemies");
        Drain(skill.Execute(user, origin + direction * 6, grid), direction,
            new Dictionary<Character, Vector2Int> { [enemy] = destination });
        Assert.That(enemy.GridPosition, Is.EqualTo(destination));
        Assert.That(user.GridPosition, Is.EqualTo(origin + direction * 6));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(78));
        Assert.That(enemy.GetStatusEffects().OfType<SlowEffect>().Count(), Is.EqualTo(1));
        Assert.That(enemy.GetStatusEffects().OfType<SlowEffect>().Single().RemainingTurns, Is.EqualTo(3));
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(95));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(2));
        AssertOccupancy();
    }

    [TestCase(1, CellType.Blocked)] [TestCase(2, CellType.Blocked)]
    public void WaveStopsAtAnyBlockedCellOfThePushedEnemyAndNeverOverlapsIt(int enemySize, CellType obstruction)
    {
        Buy(UpgradeType.WaterWavePush);
        Character enemy = Actor(false, new Vector2Int(15, 12), enemySize);
        grid.SetCellType(new Vector2Int(18, 12 + enemySize - 1), obstruction);
        Vector2Int destination = new(18 - enemySize, 12);
        Drain(new WaterWaveSkill().Execute(user, new Vector2Int(18, 12), grid), Vector2Int.right,
            new Dictionary<Character, Vector2Int> { [enemy] = destination });
        Assert.That(enemy.GridPosition, Is.EqualTo(destination));
        Assert.That(user.GridPosition, Is.EqualTo(destination + Vector2Int.left));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(78));
        AssertOccupancy();
    }

    [TestCase(1, 1, 3)] [TestCase(1, 2, 3)] [TestCase(2, 1, 3)] [TestCase(2, 2, 3)]
    [TestCase(1, 1, 1)] [TestCase(2, 2, 2)]
    public void ImmuneEnemyStaysPutAndContactBouncesTheUserExactlyOneCell(int userSize, int enemySize, int distance)
    {
        Buy(UpgradeType.WaterWavePush);
        Resize(user, userSize);
        Vector2Int origin = user.GridPosition;
        Character enemy = Actor(false, origin + Vector2Int.right * distance, enemySize);
        enemy.AddPassive(new ColossusPassive());
        Vector2Int destination = origin + Vector2Int.right * (distance - userSize - 1);
        var skill = new WaterWaveSkill();
        Assert.That(skill.CanUse(user, origin + Vector2Int.right * 6, grid), Is.True);
        Drain(skill.Execute(user, origin + Vector2Int.right * 6, grid), Vector2Int.right,
            new Dictionary<Character, Vector2Int> { [user] = destination });
        Assert.That(user.GridPosition, Is.EqualTo(destination));
        Assert.That(enemy.GridPosition, Is.EqualTo(origin + Vector2Int.right * distance));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(78));
        AssertOccupancy();
    }

    [TestCase(false)] [TestCase(true)]
    public void ImmuneEnemiesBesideThePathOrBeyondTheRangeDoNotBounceTheUser(bool beyondRange)
    {
        Buy(UpgradeType.WaterWavePush);
        Vector2Int origin = user.GridPosition;
        Character enemy = Actor(false, origin + (beyondRange ? new Vector2Int(7, 0) : new Vector2Int(3, 1)));
        enemy.AddPassive(new ColossusPassive());
        Drain(new WaterWaveSkill().Execute(user, origin + Vector2Int.right * 6, grid), Vector2Int.right);
        Assert.That(user.GridPosition, Is.EqualTo(origin + Vector2Int.right * 6));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(beyondRange ? 100 : 78));
        AssertOccupancy();
    }

    [Test]
    public void ImmuneContactDoesNotRetreatThroughACharacterBehindTheUser()
    {
        Buy(UpgradeType.WaterWavePush);
        Vector2Int origin = user.GridPosition;
        Character enemy = Actor(false, origin + Vector2Int.right);
        enemy.AddPassive(new ColossusPassive());
        Actor(true, origin + Vector2Int.left);
        Drain(new WaterWaveSkill().Execute(user, origin + Vector2Int.right * 6, grid), Vector2Int.right);
        Assert.That(user.GridPosition, Is.EqualTo(origin));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(78));
        AssertOccupancy();
    }

    [Test]
    public void WaveResolvesFarEnemiesFirstAndKeepsPackedEnemiesInSeparateCells()
    {
        Buy(UpgradeType.WaterWavePush);
        Character near = Actor(false, new Vector2Int(15, 12));
        Character far = Actor(false, new Vector2Int(17, 12));
        Drain(new WaterWaveSkill().Execute(user, new Vector2Int(18, 12), grid), Vector2Int.right,
            new Dictionary<Character, Vector2Int> { [near] = new(18, 12), [far] = new(19, 12) });
        Assert.That(far.GridPosition, Is.EqualTo(new Vector2Int(19, 12)));
        Assert.That(near.GridPosition, Is.EqualTo(new Vector2Int(18, 12)));
        Assert.That(user.GridPosition, Is.EqualTo(new Vector2Int(17, 12)));
        Assert.That(near.Stats.CurrentHP, Is.EqualTo(78));
        Assert.That(far.Stats.CurrentHP, Is.EqualTo(78));
        AssertOccupancy();
    }

    [Test]
    public void WaveWithoutPurchaseKeepsItsExistingMovementAndNeverAddsPush()
    {
        Character enemy = Actor(false, new Vector2Int(15, 12), 2);
        Drain(new WaterWaveSkill().Execute(user, new Vector2Int(18, 12), grid), Vector2Int.right);
        Assert.That(enemy.GridPosition, Is.EqualTo(new Vector2Int(15, 12)));
        Assert.That(user.GridPosition, Is.EqualTo(new Vector2Int(14, 12)));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(78));
        AssertOccupancy();
    }

    [TestCase("energy")] [TestCase("cooldown")] [TestCase("root")]
    public void UpgradedWaveRejectsUnavailableCastsWithoutMovingOrSpending(string reason)
    {
        Buy(UpgradeType.WaterWavePush);
        var skill = new WaterWaveSkill();
        Vector2Int origin = user.GridPosition;
        if (reason == "energy") user.Stats.SetCurrentEnergy(4);
        if (reason == "cooldown") user.SetSkillCooldown(skill, 1);
        if (reason == "root") user.ApplyStatusEffect(new RootEffect(3));
        int energy = user.Stats.CurrentEnergy;
        Assert.That(skill.CanUse(user, origin + Vector2Int.right * 6, grid), Is.False);
        Drain(skill.Execute(user, origin + Vector2Int.right * 6, grid), Vector2Int.right);
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(energy));
        Assert.That(user.GridPosition, Is.EqualTo(origin));
    }

    [TestCase(1)] [TestCase(2)]
    public void GlobalPrisonPreviewAndExecutionUseEveryLivingEnemyFootprintExactlyOnce(int size)
    {
        Buy(UpgradeType.WaterPrisonGlobal);
        Character adjacent = Actor(false, new Vector2Int(13, 12));
        Character far = Actor(false, new Vector2Int(30, 30), size);
        Character ally = Actor(true, new Vector2Int(15, 15));
        Character dying = Actor(false, new Vector2Int(22, 22)); Set(dying, "isDying", true);
        Character dead = Actor(false, new Vector2Int(25, 25)); dead.Stats.SetCurrentHP(0);
        var skill = new WaterPrisonSkill();
        Assert.That(skill.GetTargetType(user), Is.EqualTo(SkillTargetType.Self));
        Assert.That(skill.GetRangeDescription(user), Is.EqualTo("Todo el campo"));
        Assert.That(skill.CanUse(user, user.GridPosition, grid), Is.True);
        Assert.That(skill.CanUseFromPosition(user, new Vector2Int(1, 1), far, grid, 5), Is.True);
        Assert.That(skill.CanUseFromPosition(user, new Vector2Int(1, 1), far, grid, 4), Is.False);
        var expected = adjacent.GetOccupiedCells().Concat(far.GetOccupiedCells()).ToArray();
        CollectionAssert.AreEquivalent(expected, skill.GetPreviewCells(user, user.GridPosition, user.GridPosition, grid));
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(100));
        Assert.That(far.Stats.CurrentHP, Is.EqualTo(100));
        Drain(skill.Execute(user, user.GridPosition, grid), Vector2Int.zero);
        foreach (Character enemy in new[] { adjacent, far })
        {
            Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(79));
            Assert.That(enemy.GetStatusEffects().OfType<RootEffect>().Count(), Is.EqualTo(1));
            Assert.That(enemy.GetStatusEffects().OfType<RootEffect>().Single().RemainingTurns, Is.EqualTo(3));
        }
        Assert.That(ally.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(dying.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(ally.HasStatusEffect<RootEffect>(), Is.False);
        Assert.That(dying.HasStatusEffect<RootEffect>(), Is.False);
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(95));
        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(4));
        Assert.That(skill.CanUse(user, user.GridPosition, grid), Is.False);
        Drain(skill.Execute(user, user.GridPosition, grid), Vector2Int.zero);
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(95));
        Assert.That(far.Stats.CurrentHP, Is.EqualTo(79));
    }

    [Test]
    public void GlobalPrisonRechecksTheMapAtConfirmationAndDoesNotSpendWithoutEnemies()
    {
        Buy(UpgradeType.WaterPrisonGlobal);
        Character enemy = Actor(false, new Vector2Int(30, 30), 2);
        var skill = new WaterPrisonSkill();
        Assert.That(skill.CanUse(user, user.GridPosition, grid), Is.True);
        grid.UnregisterCharacter(enemy);
        Assert.That(skill.CanUse(user, user.GridPosition, grid), Is.False);
        Drain(skill.Execute(user, user.GridPosition, grid), Vector2Int.zero);
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(100));
        Assert.That(user.GetSkillCooldown(skill), Is.Zero);
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(100));
    }

    [Test]
    public void WaterPurchasesOnlyAffectThePlayersCurrentlyEquippedWaterHelmet()
    {
        var skill = new WaterPrisonSkill();
        Character far = Actor(false, new Vector2Int(30, 30));
        Assert.That(skill.GetTargetType(user), Is.EqualTo(SkillTargetType.TargetCell));
        Assert.That(skill.CanUse(user, far.GridPosition, grid), Is.False);
        Buy(UpgradeType.WaterWavePush); Buy(UpgradeType.WaterPrisonGlobal);
        user.AddPassive(new WaterUpgradePassive(false));
        Assert.That(user.GetPassiveEffects().OfType<WaterUpgradePassive>().Count(), Is.EqualTo(2));
        far.EquipHelmet(HelmetFactory.CreateWaterHelmet());
        Assert.That(skill.GetTargetType(far), Is.EqualTo(SkillTargetType.TargetCell));
        Assert.That(far.GetPassiveEffects().OfType<WaterUpgradePassive>(), Is.Empty);
        user.UnequipHelmet();
        Assert.That(skill.GetTargetType(user), Is.EqualTo(SkillTargetType.TargetCell));
        Assert.That(user.GetPassiveEffects().OfType<WaterUpgradePassive>(), Is.Empty);
        user.EquipHelmet(HelmetFactory.CreateWaterHelmet());
        Assert.That(skill.GetTargetType(user), Is.EqualTo(SkillTargetType.Self));
        session.AbandonRun();
        Assert.That(skill.GetTargetType(user), Is.EqualTo(SkillTargetType.TargetCell));
        Assert.That(user.GetPassiveEffects().OfType<WaterUpgradePassive>(), Is.Empty);
    }

    [Test]
    public void FirstPrisonSelectionOnlyHighlightsEnemiesAndConfirmationRunsTheGlobalCast()
    {
        Buy(UpgradeType.WaterPrisonGlobal);
        Character enemy = Actor(false, new Vector2Int(30, 30), 2);
        var input = Child("Input").AddComponent<GridInputHandler>();
        var hud = Child("HUD").AddComponent<BattleHUD>();
        Set(hud, "skillSlots", Array.Empty<SkillSlotUI>());
        Set(input, "battleHUD", hud); Set(input, "gridSystem", grid); Set(input, "selectedCharacter", user);
        Set(input, "playerCharacter", user); Set(input, "isPlayerTurn", true); Set(input, "isCharacterSelected", true);
        Set(input, "aoeTilePrefab", Child("Preview tile"));
        Call(input, "Awake");
        input.SelectSkillFromUI(3);
        var modeField = typeof(GridInputHandler).GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(modeField.GetValue(input).ToString(), Is.EqualTo("SelfSkillConfirm"));
        var tiles = (List<GameObject>)typeof(GridInputHandler).GetField("aoeTiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
        CollectionAssert.AreEquivalent(enemy.GetOccupiedCells().Select(cell => grid.GridToWorld(cell)), tiles.Select(tile => tile.transform.position));
        var rangeTiles = (List<GameObject>)typeof(GridInputHandler).GetField("rangeTiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
        Assert.That(rangeTiles, Is.Empty);
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(100));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(100));
        // The second R starts this same confirmation coroutine. Advance it
        // explicitly in EditMode rather than depending on a real keyboard/frame.
        Drain((IEnumerator)Call(input, "ExecuteSkill", user.GetSkill(3), user.GridPosition), Vector2Int.zero);
        Assert.That(modeField.GetValue(input).ToString(), Is.EqualTo("Move"));
        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(95));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(79));
        Assert.That(user.GetSkillCooldown(user.GetSkill(3)), Is.EqualTo(4));
    }

    [TestCase(1)] [TestCase(2)]
    public void WavePushStopsBeforeTheBoardEdgeForTheWholeEnemy(int userSize)
    {
        Buy(UpgradeType.WaterWavePush);
        Resize(user, userSize);
        grid.UnregisterCharacter(user); user.SetGridPosition(new Vector2Int(34, 12)); grid.RegisterCharacter(user);
        user.transform.position = grid.GridToWorld(user.GridPosition, user) + user.VisualOffset;
        Character enemy = Actor(false, new Vector2Int(37, 12), 2);
        Drain(new WaterWaveSkill().Execute(user, new Vector2Int(39, 12), grid), Vector2Int.right,
            new Dictionary<Character, Vector2Int> { [enemy] = new(38, 12) });
        Assert.That(enemy.GridPosition, Is.EqualTo(new Vector2Int(38, 12)));
        Assert.That(user.GridPosition, Is.EqualTo(new Vector2Int(38 - userSize, 12)));
        AssertOccupancy();
    }

    private void Configure(UpgradeType type)
    {
        bool prison = type == UpgradeType.WaterPrisonGlobal;
        Set(definition, "upgradeType", type);
        Set(definition, "upgradeId", prison ? UpgradeDefinition.WaterPrisonGlobalId : UpgradeDefinition.WaterWavePushId);
        Set(definition, "goldCost", prison ? 30 : 20);
    }

    private void Buy(UpgradeType type)
    {
        Configure(type);
        session.TryAddCurrency(RunCurrency.Gold, definition.Cost);
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
    }

    private static Vector2Int AtTrailing(Vector2Int origin, Vector2Int direction, int projection, int size)
    {
        Vector2Int result = origin + direction * projection;
        if (direction.x < 0) result.x -= size - 1;
        if (direction.y < 0) result.y -= size - 1;
        return result;
    }

    private void Resize(Character actor, int size)
    {
        grid.UnregisterCharacter(actor); Set(actor, "gridWidth", size); Set(actor, "gridHeight", size); grid.RegisterCharacter(actor);
    }

    private Character Actor(bool player, Vector2Int origin, int size = 1)
    {
        var go = Child("Actor"); var actor = go.AddComponent<Character>(); actor.enabled = false;
        Set(actor, "isPlayer", player); Set(actor, "_grid", grid); Set(actor, "startGridPosition", origin);
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size); Set(actor, "baseStats", new CharacterStatsData());
        var movement = go.AddComponent<CharacterMovement>(); Set(movement, "_grid", grid); Set(movement, "_character", actor);
        typeof(Character).GetProperty(nameof(Character.Movement)).SetValue(actor, movement);
        go.SetActive(true);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(20, 0, 100, 100, 0));
        actor.Stats.SetCurrentEnergy(100); actor.SetGridPosition(origin);
        actor.transform.position = grid.GridToWorld(origin, actor) + actor.VisualOffset;
        grid.RegisterCharacter(actor); actors.Add(actor); return actor;
    }

    private void AssertOccupancy()
    {
        var occupied = new HashSet<Vector2Int>();
        foreach (Character actor in actors)
            foreach (Vector2Int cell in actor.GetOccupiedCells())
            {
                Assert.That(occupied.Add(cell), Is.True, "Characters overlap at " + cell);
                Assert.That(grid.GetCharacterAt(cell), Is.SameAs(actor));
                Assert.That(grid.GetCell(cell).Walkable, Is.True);
            }
    }

    private void Drain(IEnumerator routine, Vector2Int direction, Dictionary<Character, Vector2Int> pushedDestinations = null)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        int steps = 0;
        while (stack.Count > 0)
        {
            Assert.That(++steps, Is.LessThan(2000), "Action did not finish");
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator child) { stack.Push(child); continue; }
            if (top.Current != null) continue;
            // Advance only presentation. Real coroutines still validate and
            // commit every logical movement/push and update the grid registry.
            if (top.GetType().DeclaringType == typeof(CharacterMovement))
                user.transform.position = grid.GridToWorld(user.GridPosition + direction, user) + user.VisualOffset;
            if (top.GetType().DeclaringType != typeof(PushSystem)) continue;
            foreach (Character actor in actors)
                if (grid.GetCharacterAt(actor.GridPosition) != actor)
                {
                    Assert.That(pushedDestinations != null && pushedDestinations.ContainsKey(actor), Is.True,
                        "Missing expected push destination");
                    actor.transform.position = grid.GridToWorld(pushedDestinations[actor], actor) + actor.VisualOffset;
                }
        }
    }

    private GameObject Child(string name)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(root.transform, false); return go;
    }
}
