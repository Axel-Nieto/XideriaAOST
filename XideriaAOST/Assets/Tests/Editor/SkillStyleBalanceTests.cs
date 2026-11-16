using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SkillStyleBalanceTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private SkillVFXLibrary previousVfx;
    private GameObject rock;

    [SetUp]
    public void CreateCombat()
    {
        previousVfx = SkillVFXLibrary.Instance;
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null,
            Child("Empty effects").AddComponent<SkillVFXLibrary>());
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 32); Set(grid, "height", 32); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        rock = Child("Rock template");
    }

    [TearDown]
    public void RestoreEffects() => typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, previousVfx);

    [TestCase("Normal", 0, 2, 0)]
    [TestCase("FireQ", 1, 2, 0)] [TestCase("FireW", 5, 4, 2)]
    [TestCase("FireE", 3, 3, 2)] [TestCase("FireR", 6, 6, 3)]
    [TestCase("WaterQ", 0, 2, 0)] [TestCase("WaterE", 2, 5, 2)] [TestCase("WaterR", 1, 5, 4)]
    [TestCase("EarthQ", -1, 2, 0)] [TestCase("EarthR", 3, 6, 4)]
    public void DamageTooltipsAttackBonusesAndCostsAgreeForSmallAndLargeTargets(string name, int bonus, int cost, int cooldown)
    {
        foreach (int size in new[] { 1, 2 })
            foreach (int attack in new[] { 6, 8 })
                foreach (bool armored in new[] { false, true })
                {
                    Character user = Actor(true, new Vector2Int(5, 5), attack, 2, 1);
                    Character target = Actor(false, TargetCell(name), 4, 1, size);
                    Character ally = Actor(true, new Vector2Int(6, 4), 6, 1, 1);
                    if (armored) target.AddPassive(new LarvaArmorPassive());
                    SkillBase skill = Skill(name);
                    Vector2Int selected = name == "FireE" ? new Vector2Int(8, 5) : target.GridPosition;
                    if (skill.TargetType == SkillTargetType.Self) selected = user.GridPosition;
                    try
                    {
                        Assert.That(skill.CanUse(user, selected, grid), Is.True, name);
                        Assert.That(int.Parse(Regex.Match(skill.GetTooltipDetails(user), @"\d+").Value), Is.EqualTo(attack + bonus), name);
                        Vector2Int origin = user.GridPosition;
                        Vector2Int targetOrigin = target.GridPosition;
                        Drain(skill.Execute(user, selected, grid), name, user, target);
                        int direct = Mathf.Max(1, attack + bonus - target.Stats.Defense);
                        if (armored) direct = Mathf.CeilToInt(direct * 0.8f);
                        Assert.That(target.Stats.CurrentHP, Is.EqualTo(100 - direct), name + " must hit a large target only once.");
                        Assert.That(ally.Stats.CurrentHP, Is.EqualTo(100), name);
                        Assert.That(user.Stats.CurrentEnergy, Is.EqualTo(100 - cost), name);
                        Assert.That(user.GetSkillCooldown(skill), Is.EqualTo(cooldown), name);
                        int burnDamage = name == "FireQ" ? 1 : name == "FireR" ? 2 : 0;
                        int burnTurns = name == "FireQ" ? 3 : name == "FireR" ? 2 : 0;
                        if (burnTurns > 0)
                        {
                            BurnEffect burn = target.GetStatusEffects().OfType<BurnEffect>().Single();
                            Assert.That(burn.RemainingTurns, Is.EqualTo(burnTurns));
                            for (int tick = 0; tick < burnTurns; tick++) burn.OnTurnStart(target);
                            Assert.That(target.Stats.CurrentHP, Is.EqualTo(100 - direct - burnDamage * burnTurns));
                            Assert.That(skill.GetTooltipDetails(user), Does.Contain("Quemadura: " + burnDamage));
                        }
                        if (name == "FireE") Assert.That(user.GridPosition, Is.EqualTo(new Vector2Int(8, 5)));
                        else if (name == "WaterE") Assert.That(user.GridPosition, Is.EqualTo(new Vector2Int(7, 5)));
                        else Assert.That(user.GridPosition, Is.EqualTo(origin));
                        if (name == "WaterQ" || name == "EarthQ")
                            Assert.That(target.GridPosition, Is.EqualTo(targetOrigin + Vector2Int.right));
                        foreach (Vector2Int cell in target.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(target));
                    }
                    finally { Remove(user); Remove(target); Remove(ally); }
                }
    }

    [TestCase("WaterE")] [TestCase("WaterR")]
    public void ControlStillAffectsTheNextTwoActionTurns(string name)
    {
        Character user = Actor(true, new Vector2Int(5, 5), 6, 2, 1);
        Character target = Actor(false, TargetCell(name), 4, 1, 2);
        SkillBase skill = Skill(name);
        Drain(skill.Execute(user, target.GridPosition, grid), name, user, target);
        for (int turn = 1; turn <= 3; turn++)
        {
            if (name == "WaterR" && turn == 3)
                LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode!"));
            Call(target, "ProcessStatusEffects");
            if (name == "WaterR") Assert.That(target.HasStatusEffect<RootEffect>(), Is.EqualTo(turn < 3));
            else Assert.That(target.GetMovementCostPerCell(), Is.EqualTo(turn < 3 ? 2 : 1));
        }
        Assert.That(skill.GetTooltipDetails(user), Does.Contain("2 turnos"));
    }

    [Test]
    public void HighDefenseStillLeavesAtLeastOneDirectDamage()
    {
        foreach (string name in new[] { "Normal", "FireQ", "FireW", "FireE", "FireR", "WaterQ", "WaterE", "WaterR", "EarthQ", "EarthR" })
        {
            Character user = Actor(true, new Vector2Int(5, 5), 6, 2, 1);
            Character target = Actor(false, TargetCell(name), 4, 50, 2);
            SkillBase skill = Skill(name);
            Vector2Int selected = name == "FireE" ? new Vector2Int(8, 5) : target.GridPosition;
            if (skill.TargetType == SkillTargetType.Self) selected = user.GridPosition;
            try
            {
                Drain(skill.Execute(user, selected, grid), name, user, target);
                Assert.That(target.Stats.CurrentHP, Is.EqualTo(99), name);
            }
            finally { Remove(user); Remove(target); }
        }
    }

    private SkillBase Skill(string name) => name switch
    {
        "Normal" => new BasicAttackSkill(),
        "FireQ" => new FireBasicAttackSkill(), "FireW" => new FireballSkill(),
        "FireE" => new FlameDashSkill(), "FireR" => new FireExplosionSkill(),
        "WaterQ" => new WaterBasicSkill(), "WaterE" => new WaterWaveSkill(), "WaterR" => new WaterPrisonSkill(),
        "EarthQ" => new RockBasicSkill(), _ => new RockEruptionSkill(rock, rockCount: 0, pushDistance: 0)
    };

    private static Vector2Int TargetCell(string name) => name == "Normal" ? new Vector2Int(6, 5) :
        name == "EarthQ" ? new Vector2Int(7, 5) : name == "FireE" ? new Vector2Int(8, 6) : new Vector2Int(8, 5);

    private void Drain(IEnumerator routine, string name, Character user, Character target)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        Vector2Int targetDestination = target.GridPosition + Vector2Int.right;
        int userDestinationX = name == "FireE" ? 8 : name == "WaterE" ? 7 : user.GridPosition.x;
        int steps = 0;
        while (stack.Count > 0)
        {
            Assert.That(++steps, Is.LessThan(1000), name);
            IEnumerator current = stack.Peek();
            if (!current.MoveNext()) { stack.Pop(); continue; }
            if (current.Current is IEnumerator child) { stack.Push(child); continue; }
            if (current.Current != null) continue;
            if (user.GridPosition.x < userDestinationX)
                user.transform.position = grid.GridToWorld(user.GridPosition + Vector2Int.right, user) + user.VisualOffset;
            if ((name == "WaterQ" || name == "EarthQ") && target.GridPosition.x < targetDestination.x)
                target.transform.position = grid.GridToWorld(targetDestination, target) + target.VisualOffset;
        }
    }

    private Character Actor(bool player, Vector2Int origin, int attack, int defense, int size)
    {
        GameObject go = Child(player ? "Tete" : "Target");
        Character actor = go.AddComponent<Character>();
        actor.enabled = false;
        Set(actor, "isPlayer", player); Set(actor, "_grid", grid); Set(actor, "baseStats", new CharacterStatsData());
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size);
        CharacterMovement movement = go.AddComponent<CharacterMovement>();
        Set(movement, "_grid", grid); Set(movement, "_character", actor);
        typeof(Character).GetProperty(nameof(Character.Movement)).SetValue(actor, movement);
        go.SetActive(true);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(attack, defense, 100, 100, 0));
        actor.Stats.SetCurrentEnergy(100);
        actor.SetGridPosition(origin);
        actor.transform.position = grid.GridToWorld(origin, actor) + actor.VisualOffset;
        grid.RegisterCharacter(actor);
        return actor;
    }

    private void Remove(Character actor)
    {
        grid.UnregisterCharacter(actor);
        UnityEngine.Object.DestroyImmediate(actor.gameObject);
    }

    private GameObject Child(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false); go.transform.SetParent(root.transform, false);
        return go;
    }
}
