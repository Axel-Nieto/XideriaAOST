using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class CombatBalanceTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private SkillVFXLibrary previousVfx;

    [SetUp]
    public void CreateCombat()
    {
        previousVfx = SkillVFXLibrary.Instance;
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null,
            Child("Empty effects").AddComponent<SkillVFXLibrary>());
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 16); Set(grid, "height", 16); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
    }

    [TearDown]
    public void RestoreEffects() => typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, previousVfx);

    [TestCase("Larva", 4)] [TestCase("Larva Tierra", 4)]
    [TestCase("Larva_Aire", 3)] [TestCase("Larva_Tierra", 5)] [TestCase("Larva_Fuego", 3)]
    public void CommonEnemiesSurviveTwoToFourUnupgradedBasicAttacks(string prefab, int lethalHit)
    {
        Character player = Actor(true, new Vector2Int(3, 4), 6, 2, 36);
        string source = ReadSource("Assets/Prefabs/P_Enemigos/Zona_1/" + prefab + ".prefab");
        Character enemy = Actor(false, new Vector2Int(4, 4), Field(source, "attack"), Field(source, "defense"), Field(source, "maxHP"));
        for (int hit = 1; hit < lethalHit; hit++) Drain(new BasicAttackSkill().Execute(player, enemy.GridPosition, grid));
        Assert.That(enemy.Stats.CurrentHP, Is.GreaterThan(0));
        Assert.That(enemy.Stats.CurrentHP, Is.LessThanOrEqualTo(player.Stats.Attack - enemy.Stats.Defense));
    }

    [TestCase("Larva")] [TestCase("Larva_Aire")] [TestCase("Larva_Tierra")] [TestCase("Larva_Fuego")]
    public void OneCheapFireAttackAndItsFullBurnDoNotEraseAHealthyEnemy(string prefab)
    {
        Character player = Actor(true, new Vector2Int(2, 4), 6, 2, 36);
        string source = ReadSource("Assets/Prefabs/P_Enemigos/Zona_1/" + prefab + ".prefab");
        Character enemy = Actor(false, new Vector2Int(5, 4), Field(source, "attack"), Field(source, "defense"), Field(source, "maxHP"));
        Drain(new FireBasicAttackSkill().Execute(player, enemy.GridPosition, grid));
        StatusEffect burn = enemy.GetStatusEffects()[0];
        for (int turn = 0; turn < 3; turn++) burn.OnTurnStart(enemy);
        Assert.That(enemy.Stats.IsAlive, Is.True);
        Assert.That(enemy.Stats.CurrentHP, Is.LessThan(enemy.Stats.MaxHP));
    }

    [TestCase(false, 28)] [TestCase(true, 29)]
    public void EnemySpecialAndItsDamageOverTimeLeaveRoomToReact(bool fire, int remainingHP)
    {
        Character enemy = Actor(false, new Vector2Int(2, 4), 4, 1, 16);
        Character player = Actor(true, new Vector2Int(5, 4), 6, 2, 36);
        SkillBase skill = fire ? (SkillBase)new LarvaFireSpitSkill() : new PoisonSpitSkill();
        Drain(skill.Execute(enemy, player.GridPosition, grid));
        StatusEffect damageOverTime = player.GetStatusEffects()[0];
        for (int turn = 0; turn < 3; turn++) damageOverTime.OnTurnStart(player);
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(remainingHP));
        Assert.That(enemy.Stats.CurrentEnergy, Is.EqualTo(10));
    }

    [Test]
    public void EveryPlayerMapHasTheSameBaseCombatStats()
    {
        var maps = new List<string> { "Assets/Scenes/Tutorial.unity", "Assets/Scenes/Zone -1/Lobby.unity",
            "Assets/Scenes/Zone -1/LevelBossZ1.unity", "Assets/Scenes/New-Zone-1/NewTest.unity" };
        for (int i = 1; i <= 10; i++) maps.Add("Assets/Scenes/Zone -1/Level" + i.ToString("00") + ".unity");
        foreach (string path in maps)
        {
            string source = ReadSource(path);
            int players = 0;
            foreach (Match block in Regex.Matches(source, @"(?ms)^--- !u!114 &\d+\r?\n.*?(?=^--- !u!|\z)"))
            {
                if (!block.Value.Contains("guid: 1abd9ee50946c64439ed39a032d235a0") || !block.Value.Contains("isPlayer: 1")) continue;
                players++;
                Assert.That(Field(block.Value, "attack"), Is.EqualTo(6), path);
                Assert.That(Field(block.Value, "defense"), Is.EqualTo(2), path);
                Assert.That(Field(block.Value, "maxHP"), Is.EqualTo(36), path);
            }
            Assert.That(players, Is.EqualTo(1), path);
        }
    }

    [Test]
    public void BossArmorAndAttacksRequireSeveralDecisionsInsteadOfTwoHits()
    {
        string source = ReadSource("Assets/Prefabs/P_Enemigos/Zona_1/Jefe_Zona1/BossZone1Combat.prefab");
        int health = Field(source, "maxHP");
        int attack = Field(source, "attack");
        int defense = Field(source, "defense");
        var armor = new LarvaArmorPassive();
        int playerHit = armor.ModifyIncomingDamage(null, 6 - defense, DamageType.Physical);
        Assert.That(Mathf.CeilToInt((float)health / playerHit), Is.InRange(16, 20));
        Assert.That(Field(source, "hornDamage") + attack - 2, Is.EqualTo(7));
        Assert.That(Field(source, "chargeDamage") + attack - 2, Is.EqualTo(9));
        Assert.That(36 - 2 * (Field(source, "chargeDamage") + attack - 2), Is.GreaterThan(0));
    }

    private Character Actor(bool player, Vector2Int origin, int attack, int defense, int hp)
    {
        Character actor = Child(player ? "Tete" : "Enemy").AddComponent<Character>();
        Set(actor, "isPlayer", player); Set(actor, "_grid", grid);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(attack, defense, hp, 13, 7));
        actor.Stats.SetCurrentEnergy(13);
        actor.SetGridPosition(origin);
        actor.transform.position = grid.GridToWorld(origin, actor) + actor.VisualOffset;
        grid.RegisterCharacter(actor);
        return actor;
    }

    private GameObject Child(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false); go.transform.SetParent(root.transform, false);
        return go;
    }

    private static int Field(string source, string field) => int.Parse(Regex.Match(source, @"(?m)^\s+" + field + @": (\d+)").Groups[1].Value);
    private static string ReadSource(string path) => File.ReadAllText(File.Exists(path) ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", path)));
    private static void Drain(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int steps = 0;
        while (stack.Count > 0)
        {
            Assert.That(++steps, Is.LessThan(512));
            IEnumerator current = stack.Peek();
            if (!current.MoveNext()) { stack.Pop(); continue; }
            if (current.Current is IEnumerator child) stack.Push(child);
        }
    }
}
