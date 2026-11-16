using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class LarvaPathfindingTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character enemy;
    private Character player;
    private LarvaAI ai;

    [SetUp]
    public void CreateBoard()
    {
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 24); Set(grid, "height", 14); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        enemy = Actor(false, new Vector2Int(2, 5));
        player = Actor(true, new Vector2Int(8, 5));
        ai = enemy.gameObject.AddComponent<LarvaAI>();
        Set(ai, "character", enemy); Set(ai, "grid", grid);
    }

    [TestCase(1, 1)] [TestCase(2, 1)] [TestCase(1, 2)] [TestCase(2, 2)]
    public void MeleeApproachUsesTheNearestSideOfTheWholeTarget(int enemySize, int playerSize)
    {
        Resize(enemy, enemySize); Resize(player, playerSize);
        enemy.AddSkill(new BiteSkill());
        List<Vector2Int> path = Plan();
        AssertValid(path);
        Assert.That(path.Count - 1, Is.EqualTo(6 - enemySize));
        Assert.That(path.Last(), Is.EqualTo(new Vector2Int(8 - enemySize, 5)));
    }

    [TestCase(1)] [TestCase(2)]
    public void RangedApproachStopsAtTheNearestFiringPosition(int size)
    {
        Resize(enemy, size);
        enemy.AddSkill(new PoisonSpitSkill());
        List<Vector2Int> path = Plan();
        AssertValid(path);
        Assert.That(path.Count - 1, Is.EqualTo(3 - size));
        Assert.That(new PoisonSpitSkill().CanUseFromPosition(enemy, path.Last(), player, grid,
            enemy.Stats.CurrentEnergy - (path.Count - 1)), Is.True);
    }

    [Test]
    public void PlannerReservesEnergyForTheAttackAfterMoving()
    {
        enemy.AddSkill(new LarvaFireSpitSkill());
        enemy.Stats.SetCurrentEnergy(5);
        List<Vector2Int> path = Plan();
        AssertValid(path);
        Assert.That(path.Last(), Is.EqualTo(new Vector2Int(4, 5)));
        Assert.That(path.Count - 1, Is.EqualTo(2));
        Assert.That(enemy.GetSkill(0).CanUseFromPosition(enemy, path.Last(), player, grid, 3), Is.True);
    }

    [Test]
    public void EnemyAlreadyInRangeSavesEnergyInsteadOfWalkingAway()
    {
        Move(enemy, new Vector2Int(4, 5));
        enemy.AddSkill(new LarvaFireSpitSkill());
        enemy.Stats.SetCurrentEnergy(2);
        Assert.That(Plan(), Is.EqualTo(new[] { enemy.GridPosition }));
    }

    [Test]
    public void EnemyAdjacentOnCooldownDoesNotCircleThePlayer()
    {
        Move(enemy, new Vector2Int(7, 5));
        SkillBase bite = new BiteSkill();
        enemy.AddSkill(bite);
        enemy.SetSkillCooldown(bite, 1);
        Assert.That(Plan(), Is.EqualTo(new[] { enemy.GridPosition }));
    }

    [Test]
    public void ApproachStillAdvancesWhenThePlayerIsAcrossAnUnreachableBarrier()
    {
        Move(enemy, new Vector2Int(20, 5));
        Move(player, new Vector2Int(1, 5));
        for (int y = 0; y < grid.Height; y++) grid.SetCellType(new Vector2Int(9, y), CellType.Blocked);
        enemy.AddSkill(new BiteSkill());
        List<Vector2Int> path = Plan();
        AssertValid(path);
        Assert.That(path.Last(), Is.EqualTo(new Vector2Int(10, 5)));
        Assert.That(path.Count - 1, Is.EqualTo(10));
    }

    [TestCase(1)] [TestCase(2)]
    public void NarrowCorridorOnlyAllowsBodiesThatFit(int size)
    {
        Resize(enemy, size);
        for (int y = 0; y < grid.Height; y++)
            if (y != 5) grid.SetCellType(new Vector2Int(6, y), CellType.Blocked);
        enemy.AddSkill(new BiteSkill());
        List<Vector2Int> path = Plan();
        AssertValid(path);
        Assert.That(path.Last(), Is.EqualTo(new Vector2Int(size == 1 ? 7 : 4, 5)));
    }

    [TestCase(11, 6)] [TestCase(9, 9)] [TestCase(8, 2)]
    public void Level04UsesAShortestAttackRouteAroundItsActualVoidCells(int x, int y)
    {
        string relative = "Assets/Scenes/Zone -1/Level04.unity";
        string source = File.Exists(relative) ? File.ReadAllText(relative) :
            File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", relative)));
        Set(grid, "width", 12); Set(grid, "height", 14);
        foreach (Match cell in Regex.Matches(source, @"- position: \{x: (\d+), y: (\d+)\}\r?\n\s+type: (\d+)"))
            grid.SetCellType(new Vector2Int(int.Parse(cell.Groups[1].Value), int.Parse(cell.Groups[2].Value)),
                (CellType)int.Parse(cell.Groups[3].Value));
        Move(player, new Vector2Int(1, 6));
        Move(enemy, new Vector2Int(x, y));
        enemy.AddSkill(new BiteSkill()); enemy.AddSkill(new PoisonSpitSkill());
        enemy.Stats.SetCurrentEnergy(7);
        List<Vector2Int> path = Plan();
        AssertValid(path);
        // Independent oracle: compare all viable attack destinations using the existing A*.
        int shortest = int.MaxValue;
        var astar = new AStarPathfinding(grid);
        for (int gx = 0; gx < grid.Width; gx++)
            for (int gy = 0; gy < grid.Height; gy++)
            {
                Vector2Int goal = new Vector2Int(gx, gy);
                if (!grid.CanCharacterOccupy(enemy, goal)) continue;
                if (!enemy.GetActiveSkills().Any(skill => skill.CanUseFromPosition(enemy, goal, player, grid, 10))) continue;
                List<Vector2Int> route = astar.FindPath(enemy.GridPosition, goal, enemy, true);
                if (route != null) shortest = Mathf.Min(shortest, route.Count - 1);
            }
        Assert.That(shortest, Is.LessThan(int.MaxValue));
        Assert.That(path.Count - 1, Is.EqualTo(shortest));
    }

    [TestCase(false, false)] [TestCase(true, false)]
    [TestCase(false, true)] [TestCase(true, true)]
    public void MovementPaysOnlyCompletedStepsAndStopsWhenRooted(bool rooted, bool slowed)
    {
        enemy.AddSkill(new LarvaFireSpitSkill());
        int initialEnergy = slowed ? 7 : 5;
        enemy.Stats.SetCurrentEnergy(initialEnergy);
        var statuses = (List<StatusEffect>)typeof(Character).GetField("statusEffects",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(enemy);
        if (rooted) statuses.Add(new RootEffect(1));
        if (slowed) statuses.Add(new SlowEffect(1));
        Vector2Int origin = enemy.GridPosition;
        DrainMovement((IEnumerator)Call(ai, "MoveTowardsPlayer", player));
        Assert.That(enemy.GridPosition, Is.EqualTo(rooted ? origin : new Vector2Int(4, 5)));
        Assert.That(enemy.Stats.CurrentEnergy, Is.EqualTo(rooted ? initialEnergy : 3));
        if (!rooted) Assert.That(enemy.GetSkill(0).CanUse(enemy, player.GridPosition, grid), Is.True);
        foreach (Vector2Int cell in enemy.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(enemy));
    }

    [Test]
    public void NoEnergyCannotMoveOrSpendEvenWhenARouteExists()
    {
        enemy.AddSkill(new BiteSkill());
        enemy.Stats.SetCurrentEnergy(0);
        Vector2Int origin = enemy.GridPosition;
        DrainMovement((IEnumerator)Call(ai, "MoveTowardsPlayer", player));
        Assert.That(enemy.GridPosition, Is.EqualTo(origin));
        Assert.That(enemy.Stats.CurrentEnergy, Is.Zero);
    }

    [TestCase(1)] [TestCase(2)]
    public void OtherBodiesBlockTheRouteAndMovingTeteChangesTheDestination(int size)
    {
        Resize(enemy, size);
        Character blocker = Actor(false, new Vector2Int(4, 5));
        Resize(blocker, 2);
        enemy.AddSkill(new BiteSkill());
        List<Vector2Int> first = Plan();
        AssertValid(first);
        Assert.That(first[1], Is.Not.EqualTo(enemy.GridPosition + Vector2Int.right));
        Move(player, new Vector2Int(2, 10));
        List<Vector2Int> second = Plan();
        AssertValid(second);
        Assert.That(second.Last(), Is.Not.EqualTo(first.Last()));
        Assert.That(second.Count - 1, Is.EqualTo(5 - size));
    }

    [Test]
    public void LargeEnemyAvoidsVoidUnderTheFarCornerOfItsBody()
    {
        Resize(enemy, 2);
        enemy.AddSkill(new BiteSkill());
        grid.SetCellType(new Vector2Int(4, 6), CellType.Void);
        List<Vector2Int> path = Plan();
        AssertValid(path);
        Assert.That(path[1], Is.EqualTo(enemy.GridPosition + Vector2Int.down));
        Assert.That(path, Has.No.Member(new Vector2Int(3, 5)));
    }

    private List<Vector2Int> Plan() => (List<Vector2Int>)Call(ai, "FindBestPathTowardsPlayer", player);

    private void AssertValid(List<Vector2Int> path)
    {
        Assert.That(path, Is.Not.Null.And.Not.Empty);
        Assert.That(path[0], Is.EqualTo(enemy.GridPosition));
        for (int i = 0; i < path.Count; i++)
        {
            Assert.That(grid.CanCharacterOccupy(enemy, path[i]), Is.True);
            foreach (Vector2Int cell in enemy.GetOccupiedCells(path[i])) Assert.That(grid.IsSafeForAI(cell), Is.True);
            if (i > 0) Assert.That(Mathf.Abs(path[i].x - path[i - 1].x) + Mathf.Abs(path[i].y - path[i - 1].y), Is.EqualTo(1));
        }
    }

    private GameObject Child(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(root.transform, false);
        return go;
    }

    private Character Actor(bool isPlayer, Vector2Int origin)
    {
        GameObject go = Child(isPlayer ? "Tete" : "Enemy");
        Character character = go.AddComponent<Character>();
        character.enabled = false;
        Set(character, "isPlayer", isPlayer); Set(character, "_grid", grid);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(character, new CharacterStats(4, 2, 100, 10, 5));
        character.Stats.SetCurrentEnergy(10);
        CharacterMovement movement = go.AddComponent<CharacterMovement>();
        Set(movement, "_grid", grid); Set(movement, "_character", character);
        typeof(Character).GetProperty(nameof(Character.Movement)).SetValue(character, movement);
        Move(character, origin);
        go.SetActive(true);
        return character;
    }

    private void Resize(Character actor, int size)
    {
        grid.UnregisterCharacter(actor);
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size);
        grid.RegisterCharacter(actor);
    }

    private void Move(Character actor, Vector2Int origin)
    {
        grid.UnregisterCharacter(actor);
        actor.SetGridPosition(origin);
        actor.transform.position = grid.GridToWorld(origin, actor) + actor.VisualOffset;
        grid.RegisterCharacter(actor);
    }

    private void DrainMovement(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int frames = 0;
        while (stack.Count > 0)
        {
            Assert.That(++frames, Is.LessThan(1000));
            IEnumerator current = stack.Peek();
            if (!current.MoveNext()) { stack.Pop(); continue; }
            if (current.Current is IEnumerator child) { stack.Push(child); continue; }
            // Advance only the visual interpolation; the real coroutine commits occupancy/energy.
            enemy.transform.position = grid.GridToWorld(enemy.GridPosition + Vector2Int.right, enemy) + enemy.VisualOffset;
        }
    }
}
