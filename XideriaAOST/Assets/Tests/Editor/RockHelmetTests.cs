using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;
using UnityEngine.TestTools;

public class RockCombatTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character player;
    private GameObject rock;
    private readonly List<Character> characters = new();
    private UnityEngine.Random.State randomState;

    [SetUp]
    public void CreateCombat()
    {
        randomState = UnityEngine.Random.state;
        characters.Clear();
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 16); Set(grid, "height", 16); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        rock = Child("Rock visual");
        player = Actor(true, new Vector2Int(5, 5));
        player.EquipHelmet(HelmetFactory.CreateRockHelmet(rock));
    }

    [TearDown]
    public void RestoreRandom() => UnityEngine.Random.state = randomState;

    [Test]
    public void HelmetHasTheRequestedTypesCostsAndCooldowns()
    {
        Assert.That((int)HelmetType.Fire, Is.EqualTo(1));
        Assert.That((int)HelmetType.Water, Is.EqualTo(2));
        Assert.That((int)HelmetType.Rock, Is.EqualTo(3));
        Assert.That(player.GetActiveSkills().Select(s => s.GetType()), Is.EqualTo(new[] {
            typeof(RockBasicSkill), typeof(RockShieldSkill), typeof(RockExcavateSkill), typeof(RockEruptionSkill)
        }));
        CollectionAssert.AreEqual(new[] { 2, 5, 1, 6 }, player.GetActiveSkills().Select(s => s.EnergyCost));
        CollectionAssert.AreEqual(new[] { 0, 3, 0, 4 }, player.GetActiveSkills().Select(s => s.Cooldown));
        Assert.That(player.BlocksPush(), Is.True);
    }

    [Test]
    public void SpinHitsAndPushesEachEnemyOnceIncludingTwoByTwoAndGainsOneStackPerEnemy()
    {
        Character a = Actor(false, new Vector2Int(7, 5));
        Character b = Actor(false, new Vector2Int(5, 7), 2);
        Character far = Actor(false, new Vector2Int(8, 4));
        Character ally = Actor(true, new Vector2Int(4, 5));
        var skill = new RockBasicSkill();
        Drain(skill.Execute(player, player.GridPosition, grid), new Dictionary<Character, Vector2Int> {
            [a] = new(8, 5), [b] = new(5, 8)
        });
        Assert.That(a.Stats.CurrentHP, Is.EqualTo(81));
        Assert.That(b.Stats.CurrentHP, Is.EqualTo(81));
        Assert.That(far.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(ally.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(a.GridPosition, Is.EqualTo(new Vector2Int(8, 5)));
        Assert.That(b.GridPosition, Is.EqualTo(new Vector2Int(5, 8)));
        foreach (Character target in new[] { a, b })
            foreach (Vector2Int cell in target.GetOccupiedCells())
                Assert.That(grid.GetCharacterAt(cell), Is.SameAs(target));
        Assert.That(grid.GetCharacterAt(new Vector2Int(7, 5)), Is.Null);
        Assert.That(grid.GetCharacterAt(new Vector2Int(5, 7)), Is.Null);
        Assert.That(Guard.ArmorStacks, Is.EqualTo(2));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(98));
        Assert.That(player.GridPosition, Is.EqualTo(new Vector2Int(5, 5)));
        Assert.That(skill.GetPreviewCells(player, player.GridPosition, player.GridPosition, grid), Does.Contain(new Vector2Int(5, 7)));
    }

    [TestCase(1)] [TestCase(2)]
    public void SpinPushBlockedAtAnyFootprintCellTriggersUnstoppableBonusOnce(int size)
    {
        Character target = Actor(false, new Vector2Int(7, 5), size);
        grid.SetCellType(new Vector2Int(7 + size, 5 + size - 1), CellType.Blocked);
        Drain(new RockBasicSkill().Execute(player, player.GridPosition, grid));
        Assert.That(target.GridPosition, Is.EqualTo(new Vector2Int(7, 5)));
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(79));
        Assert.That(Guard.ArmorStacks, Is.EqualTo(1));
        foreach (Vector2Int cell in target.GetOccupiedCells())
            Assert.That(grid.GetCharacterAt(cell), Is.SameAs(target));
    }

    [Test]
    public void SpinDamagesBossWithoutMovingEitherImmuneActorOrAddingCollisionDamage()
    {
        Character boss = Actor(false, new Vector2Int(7, 5), 2);
        boss.AddPassive(new ColossusPassive());
        Drain(new RockBasicSkill().Execute(player, player.GridPosition, grid));
        Assert.That(boss.Stats.CurrentHP, Is.EqualTo(81));
        Assert.That(boss.GridPosition, Is.EqualTo(new Vector2Int(7, 5)));
        Assert.That(player.GridPosition, Is.EqualTo(new Vector2Int(5, 5)));
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(Guard.ArmorStacks, Is.EqualTo(1));
    }

    [Test]
    public void ArmorStacksExpireIndependentlyAndShieldUsesOnlyOneChargePerPositiveHit()
    {
        Guard.AddArmor(2);
        Assert.That(Guard.ModifyIncomingDamage(player, 100, DamageType.Physical), Is.EqualTo(80));
        player.StartTurn();
        Guard.AddArmor(1);
        player.StartTurn();
        Assert.That(Guard.ArmorStacks, Is.EqualTo(1));
        Drain(player.GetSkill(1).Execute(player, player.GridPosition, grid));
        Assert.That(Guard.ShieldCharges, Is.EqualTo(3));
        Assert.That(Guard.ModifyIncomingDamage(player, 0, DamageType.Fire), Is.Zero);
        Assert.That(Guard.ShieldCharges, Is.EqualTo(3));
        Assert.That(Guard.ModifyIncomingDamage(player, 100, DamageType.Fire), Is.EqualTo(80));
        Assert.That(Guard.ShieldCharges, Is.EqualTo(2));
        Assert.That(Guard.ModifyIncomingDamage(player, 100, DamageType.Water), Is.EqualTo(80));
        Assert.That(Guard.ModifyIncomingDamage(player, 100, DamageType.Physical), Is.EqualTo(80));
        Assert.That(Guard.ModifyIncomingDamage(player, 100, DamageType.Physical), Is.EqualTo(90));
        player.StartTurn();
        Assert.That(Guard.ArmorStacks, Is.Zero);
        Assert.That(Guard.ShieldCharges, Is.Zero);
    }

    [Test]
    public void ShieldExpiresAtNextOwnTurnAndCooldownPreventsAnotherCost()
    {
        SkillBase shield = player.GetSkill(1);
        Drain(shield.Execute(player, player.GridPosition, grid));
        Assert.That(Guard.ShieldCharges, Is.EqualTo(3));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(95));
        Drain(shield.Execute(player, player.GridPosition, grid));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(95));
        player.StartTurn();
        Assert.That(Guard.ShieldCharges, Is.Zero);
        Assert.That(player.GetSkillCooldown(shield), Is.EqualTo(3));
        player.EquipHelmet(HelmetFactory.CreateDefaultHelmet());
        Assert.That(player.GetPassiveEffects().OfType<RockGuardPassive>(), Is.Empty);
        Assert.That(player.BlocksPush(), Is.False);
    }

    [TestCase(1, 0, 1)] [TestCase(-1, 0, 1)] [TestCase(0, 1, 1)] [TestCase(0, -1, 1)]
    [TestCase(1, 0, 2)] [TestCase(-1, 0, 2)] [TestCase(0, 1, 2)] [TestCase(0, -1, 2)]
    public void ExcavateCrossesOneObstacleAndMovesTheWholeFootprint(int dx, int dy, int size)
    {
        Resize(player, size);
        Vector2Int origin = player.GridPosition;
        var direction = new Vector2Int(dx, dy);
        Vector2Int obstacle = origin + new Vector2Int(dx > 0 ? size : dx, dy > 0 ? size : dy);
        grid.SetCellType(obstacle, CellType.Blocked);
        Vector2Int destination = origin + direction * (size + 1);
        var skill = new RockExcavateSkill();
        Assert.That(skill.CanUseFromCell(player, origin, obstacle, grid, 1), Is.True);
        Assert.That(skill.CanUseFromCell(player, origin, obstacle, grid, 0), Is.False);
        Assert.That(skill.GetPreviewCells(player, origin, obstacle, grid), Is.EquivalentTo(player.GetOccupiedCells(destination).Concat(new[] { obstacle })));
        Drain(skill.Execute(player, obstacle, grid));
        Assert.That(player.GridPosition, Is.EqualTo(destination));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(99));
        Assert.That(grid.GetCell(obstacle).Type, Is.EqualTo(CellType.Blocked));
        foreach (Vector2Int cell in player.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
        foreach (Vector2Int cell in player.GetOccupiedCells(origin)) Assert.That(grid.GetCharacterAt(cell), Is.Null);
    }

    [TestCase("diagonal")] [TestCase("no obstacle")] [TestCase("wall thickness")]
    [TestCase("void")] [TestCase("occupied")] [TestCase("rooted")] [TestCase("large blocked corner")]
    public void InvalidExcavationsNeverSpendEnergyOrChangeOccupancy(string reason)
    {
        Vector2Int origin = player.GridPosition;
        Vector2Int obstacle = origin + Vector2Int.right;
        grid.SetCellType(obstacle, CellType.Blocked);
        if (reason == "diagonal") obstacle += Vector2Int.up;
        if (reason == "no obstacle") grid.SetCellType(obstacle, CellType.Ground);
        if (reason == "wall thickness") grid.SetCellType(origin + Vector2Int.right * 2, CellType.Blocked);
        if (reason == "void") grid.SetCellType(origin + Vector2Int.right * 2, CellType.Void);
        if (reason == "occupied") Actor(false, origin + Vector2Int.right * 2);
        if (reason == "rooted") player.GetStatusEffects().Add(new RootEffect(2));
        if (reason == "large blocked corner")
        {
            Resize(player, 2);
            obstacle = origin + Vector2Int.right * 2;
            grid.SetCellType(origin + Vector2Int.right, CellType.Ground);
            grid.SetCellType(obstacle, CellType.Blocked);
            grid.SetCellType(origin + new Vector2Int(4, 1), CellType.Blocked);
        }
        var skill = new RockExcavateSkill();
        Assert.That(skill.CanUse(player, obstacle, grid), Is.False);
        Drain(skill.Execute(player, obstacle, grid));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(100));
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        foreach (Vector2Int cell in player.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
    }

    [TestCase(1, 0, 1)] [TestCase(-1, 0, 1)] [TestCase(0, 1, 1)] [TestCase(0, -1, 1)]
    [TestCase(1, 0, 2)] [TestCase(-1, 0, 2)] [TestCase(0, 1, 2)] [TestCase(0, -1, 2)]
    public void ExcavationEffectsHideEachBlockedBandWithoutHidingTheOthers(int dx, int dy, int size)
    {
        Resize(player, size);
        Vector2Int origin = player.GridPosition;
        var direction = new Vector2Int(dx, dy);
        Vector2Int side = dx != 0 ? Vector2Int.up : Vector2Int.right;
        int depth = dx < 0 || dy < 0 ? 1 : size;
        Vector2Int first = origin + direction * depth;
        Vector2Int third = first + direction * 2;
        Assert.That(RockSkillVFX.CanShowExcavateRock(player, grid, origin, direction, 1), Is.True);
        // A blocked far corner must also suppress the effect for a 2x2 character.
        grid.SetCellType(first + side * (size - 1), CellType.Blocked);
        grid.SetCellType(third + side * (size - 1), CellType.Void);
        Assert.That(RockSkillVFX.CanShowExcavateRock(player, grid, origin, direction, 1), Is.False);
        Assert.That(RockSkillVFX.CanShowExcavateRock(player, grid, origin, direction, 2), Is.True);
        Assert.That(RockSkillVFX.CanShowExcavateRock(player, grid, origin, direction, 3), Is.False);
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        foreach (Vector2Int cell in player.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
    }

    [TestCase(1)] [TestCase(2)]
    public void ExcavationEffectsRejectCharactersAndTheEdgeOfTheBoard(int size)
    {
        Resize(player, size);
        Vector2Int origin = player.GridPosition;
        Vector2Int first = origin + Vector2Int.right * size;
        Actor(false, first + Vector2Int.up * (size - 1), 2);
        Assert.That(RockSkillVFX.CanShowExcavateRock(player, grid, origin, Vector2Int.right, 1), Is.False);
        Assert.That(RockSkillVFX.CanShowExcavateRock(player, grid, new Vector2Int(0, 0), Vector2Int.left, 1), Is.False);
    }

    [Test]
    public void PlayerAndBossCannotPushEachOtherOrReflectTheirImmunity()
    {
        Character boss = Actor(false, new Vector2Int(7, 5), 2);
        boss.AddPassive(new ColossusPassive());
        Drain(PushSystem.Push(player, boss, grid, 3));
        Drain(PushSystem.Push(boss, player, grid, 3));
        Drain(PushSystem.PushInDirection(player, Vector2Int.left, 2, grid));
        Assert.That(player.GridPosition, Is.EqualTo(new Vector2Int(5, 5)));
        Assert.That(boss.GridPosition, Is.EqualTo(new Vector2Int(7, 5)));
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(boss.Stats.CurrentHP, Is.EqualTo(100));
        foreach (Vector2Int cell in boss.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(boss));
    }

    [TestCase(1)] [TestCase(2)]
    public void CollisionUsesEveryTargetCellAndDealsExactlyTwoExtraDamage(int size)
    {
        Character target = Actor(false, new Vector2Int(7, 5), size);
        grid.SetCellType(new Vector2Int(7 + size, 5 + size - 1), CellType.Blocked);
        Drain(PushSystem.Push(player, target, grid, 1));
        Assert.That(target.GridPosition, Is.EqualTo(new Vector2Int(7, 5)));
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(98));
    }

    [Test]
    public void PartialPushDealsCollisionBonusOnceAndOrdinaryReflectionStillWorks()
    {
        Character target = Actor(false, new Vector2Int(7, 5));
        grid.SetCellType(new Vector2Int(10, 5), CellType.Blocked);
        Drain(PushSystem.Push(player, target, grid, 3), new Dictionary<Character, Vector2Int> { [target] = new(9, 5) });
        Assert.That(target.GridPosition, Is.EqualTo(new Vector2Int(9, 5)));
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(98));
        player.EquipHelmet(HelmetFactory.CreateDefaultHelmet());
        target.AddPassive(new ColossusPassive());
        Drain(PushSystem.Push(player, target, grid, 1), new Dictionary<Character, Vector2Int> { [player] = new(4, 5) });
        Assert.That(player.GridPosition, Is.EqualTo(new Vector2Int(4, 5)));
    }

    [TestCase(CellType.Ground)] [TestCase(CellType.Water)] [TestCase(CellType.Lava)]
    public void TemporaryRocksBlockLargeFootprintsAndRestoreExactTerrainAfterTwoOwnTurns(CellType original)
    {
        var field = TemporaryRockField.For(grid);
        var cell = new Vector2Int(9, 9);
        grid.SetCellType(cell, original);
        Assert.That(field.TryPlace(player, cell, rock), Is.True);
        Character large = Actor(false, new Vector2Int(1, 1), 2);
        Assert.That(grid.CanCharacterOccupy(large, new Vector2Int(8, 8)), Is.False);
        Assert.That(field.TryPlace(player, cell, rock), Is.False);
        Call(field, "Tick", large);
        Call(field, "Tick", player);
        Assert.That(grid.GetCell(cell).Type, Is.EqualTo(CellType.Blocked));
        Call(field, "Tick", large);
        Assert.That(grid.GetCell(cell).Type, Is.EqualTo(CellType.Blocked));
        Call(field, "Tick", player);
        Assert.That(field.Count, Is.Zero);
        Assert.That(grid.GetCell(cell).Type, Is.EqualTo(original));
        Assert.That(grid.CanCharacterOccupy(large, new Vector2Int(8, 8)), Is.True);
    }

    [TestCase(CellType.Blocked)] [TestCase(CellType.Void)] [TestCase(CellType.ExitRun)]
    [TestCase(CellType.ExitTutorial)] [TestCase(CellType.Shop)]
    public void RocksCannotOverwriteOriginalObstaclesVoidOrDoors(CellType original)
    {
        var cell = new Vector2Int(9, 9);
        grid.SetCellType(cell, original);
        Assert.That(TemporaryRockField.For(grid).TryPlace(player, cell, rock), Is.False);
        Assert.That(grid.GetCell(cell).Type, Is.EqualTo(original));
    }

    [Test]
    public void RockCleanupWorksOnOwnerDeathAndDoesNotOverwriteAnExternalTerrainChange()
    {
        var field = TemporaryRockField.For(grid);
        var a = new Vector2Int(9, 9); var b = new Vector2Int(10, 9);
        grid.SetCellType(a, CellType.Water);
        field.TryPlace(player, a, rock); field.TryPlace(player, b, rock);
        grid.SetCellType(b, CellType.Lava);
        Call(field, "ClearOwner", player);
        Assert.That(field.Count, Is.Zero);
        Assert.That(grid.GetCell(a).Type, Is.EqualTo(CellType.Water));
        Assert.That(grid.GetCell(b).Type, Is.EqualTo(CellType.Lava));
    }

    [Test]
    public void EruptionHitsPushesCreatesUpToThreeRocksAndRespectsCooldown()
    {
        Character target = Actor(false, new Vector2Int(7, 5));
        SkillBase eruption = player.GetSkill(3);
        Drain(eruption.Execute(player, player.GridPosition, grid), new Dictionary<Character, Vector2Int> { [target] = new(8, 5) });
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(77));
        Assert.That(target.GridPosition, Is.EqualTo(new Vector2Int(8, 5)));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(94));
        var field = grid.GetComponent<TemporaryRockField>();
        Assert.That(field.Count, Is.EqualTo(3));
        Assert.That(grid.GetCell(player.GridPosition).Walkable, Is.True);
        Assert.That(grid.GetCell(target.GridPosition).Walkable, Is.True);
        Drain(eruption.Execute(player, player.GridPosition, grid));
        Assert.That(field.Count, Is.EqualTo(3));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(94));
        field.ClearAll();
    }

    [TestCase(1)] [TestCase(2)]
    public void EruptionRadiusFiveMatchesDamageSimulationAndPreviewForTheWholeFootprint(int size)
    {
        Resize(player, size);
        Vector2Int edge = player.GridPosition + Vector2Int.right * (size + 4);
        Vector2Int outside = player.GridPosition + Vector2Int.up * (size + 5);
        Character insideTarget = Actor(false, edge, size);
        Character outsideTarget = Actor(false, outside, size);
        var skill = new RockEruptionSkill(rock, rockCount: 0, pushDistance: 0);
        Assert.That(skill.AreaRadius, Is.EqualTo(5));
        Assert.That(skill.CanUseFromPosition(player, player.GridPosition, insideTarget, grid, 100), Is.True);
        Assert.That(skill.CanUseFromPosition(player, player.GridPosition, outsideTarget, grid, 100), Is.False);
        var preview = skill.GetPreviewCells(player, player.GridPosition, player.GridPosition, grid).ToList();
        Assert.That(preview, Does.Contain(edge));
        Assert.That(preview.Contains(outside), Is.False);
        Drain(skill.Execute(player, player.GridPosition, grid));
        Assert.That(insideTarget.Stats.CurrentHP, Is.EqualTo(77));
        Assert.That(outsideTarget.Stats.CurrentHP, Is.EqualTo(100));
    }

    [Test]
    public void EruptionUsesVisibleCellSizedRockModelsAboveTheDecorativeFloor()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/HelmetModels/Rock.fbx");
        Assert.That(model, Is.Not.Null);
        var skill = new RockEruptionSkill(model);
        Drain(skill.Execute(player, player.GridPosition, grid));
        var field = grid.GetComponent<TemporaryRockField>();
        Assert.That(field.Count, Is.EqualTo(3));
        foreach (Transform visual in grid.transform)
        {
            if (!visual.name.StartsWith("Temporary Rock ")) continue;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.GreaterThan(0));
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Assert.That(renderers.All(r => r.enabled), Is.True);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.min.y, Is.EqualTo(grid.GridToWorld(Vector2Int.zero).y + 0.3f).Within(0.001f));
            Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(bounds.size.y, Is.GreaterThan(0.5f));
        }
        field.ClearAll();
    }

    [TestCase(1)] [TestCase(2)]
    public void TemporaryRockBaseMatchesTheMapTileWithoutMovingItsBlockedCell(int casterSize)
    {
        Set(player, "gridWidth", casterSize);
        Set(player, "gridHeight", casterSize);
        // Character model offsets/rotations are unrelated to the map's tile offset.
        Set(player, "visualSpawnOffset", new Vector3(4f, 3f, -2f));
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        grid.transform.position = new Vector3(-0.6f, 0f, -0.6f);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/HelmetModels/Rock.fbx");
        var field = TemporaryRockField.For(grid);
        var cell = new Vector2Int(10, 9);
        Assert.That(field.TryPlace(player, cell, model), Is.True);
        Transform visual = grid.transform.Cast<Transform>().Single(t => t.name == "Temporary Rock " + cell);
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        Vector3 surface = grid.GridToWorld(cell) + new Vector3(-0.3f, 0.3f, -0.3f);
        Assert.That(bounds.center.x, Is.EqualTo(surface.x).Within(0.001f));
        Assert.That(bounds.center.z, Is.EqualTo(surface.z).Within(0.001f));
        Assert.That(bounds.min.y, Is.EqualTo(surface.y).Within(0.001f));
        Assert.That(grid.GetCell(cell).Type, Is.EqualTo(CellType.Blocked));
        Assert.That(grid.GetCell(cell + Vector2Int.right).Type, Is.EqualTo(CellType.Ground));
        Assert.That(grid.GetCell(cell + Vector2Int.up).Type, Is.EqualTo(CellType.Ground));
        field.ClearAll();
        Assert.That(grid.GetCell(cell).Type, Is.EqualTo(CellType.Ground));
    }

    [TestCase(0.5f, 1f)] [TestCase(1f, 1f)] [TestCase(2f, 3f)]
    public void EruptionEffectMeshBaseRestsOnItsTileThroughoutGridAndCellSizeChanges(float cellSize, float parentScale)
    {
        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath("Assets/Prefabs/P_Skills/Ground_Helmet_Skill/RockSkill_R.fbx")
            .OfType<Mesh>().Single();
        var presentation = player.gameObject.AddComponent<RockSkillVFX>();
        Transform effect = Child("R effect transform").transform;
        grid.transform.SetPositionAndRotation(new Vector3(-0.6f, 2f, -0.6f), Quaternion.Euler(0f, 90f, 0f));
        grid.transform.localScale = Vector3.one * parentScale;
        Set(grid, "cellSize", cellSize);
        Vector3 surface = grid.GridToWorld(new Vector2Int(10, 9)) + new Vector3(-0.3f, 0.3f, -0.3f);
        Call(presentation, "PositionEruptionRock", effect, surface, cellSize, grid.transform);

        // Use the imported mesh and the graph's size/scale/pivot, as applied by
        // GetElementToVFXMatrix in the installed VFX package. Bounds from the VFX
        // renderer also include debris and are not the permanent obstacle base.
        Vector3 meshBase = new(mesh.bounds.center.x, mesh.bounds.min.y, mesh.bounds.center.z);
        Vector3 graphBase = Vector3.Scale(meshBase - new Vector3(0f, 1.7f, 0f), new Vector3(0.3f, 0.45f, 0.3f));
        Vector3 restingBase = graphBase + Vector3.up * 1.5f;
        Assert.That(Vector3.Distance(effect.TransformPoint(restingBase), surface), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(effect.lossyScale, Vector3.one * (cellSize * 0.8f)), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(effect.rotation, Quaternion.identity), Is.LessThan(0.001f));
        // At the start of emergence (and end of sinking), the mesh is below the floor.
        Assert.That(effect.TransformPoint(graphBase).y, Is.LessThan(surface.y));
    }

    [TestCase(1, false)] [TestCase(2, false)]
    [TestCase(1, true)] [TestCase(2, true)]
    public void ExcavationThroughTheInputFlowAttemptsAdjacentPickupAndRespectsInventoryCapacity(int size, bool full)
    {
        HelmetCatalog oldCatalog = HelmetCatalog.Instance;
        HelmetInventoryData oldInventory = HelmetInventoryData.Instance;
        var previousScene = root.scene;
        var pickupScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, pickupScene);
        try
        {
            HelmetCatalog catalog = Child("Catalog").AddComponent<HelmetCatalog>();
            Set(catalog, "helmets", new List<HelmetCatalogEntry> { new() { Type = HelmetType.Rock, MaxCharges = 3, MaxStored = 3 } });
            typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, catalog);
            HelmetInventoryData inventory = Child("Inventory Data").AddComponent<HelmetInventoryData>();
            typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, inventory);
            Call(inventory, "InitializeInventory"); inventory.UnlockHelmet(HelmetType.Rock);
            if (full) inventory.AddHelmet(HelmetType.Rock, 3);
            Resize(player, size);
            player.enabled = false; player.gameObject.SetActive(true);
            Vector2Int obstacle = player.GridPosition + Vector2Int.right * size;
            Vector2Int destination = player.GridPosition + Vector2Int.right * (size + 1);
            grid.SetCellType(obstacle, CellType.Blocked);
            HelmetPickup pickup = Child("Adjacent pickup").AddComponent<HelmetPickup>();
            pickup.gameObject.AddComponent<HelmetPickupAnimator>();
            Set(pickup, "_helmetData", HelmetFactory.CreateRockHelmet(rock));
            pickup.SetGridPosition(destination + new Vector2Int(size, size - 1));
            pickup.gameObject.SetActive(true);
            BattleHUD hud = Child("HUD").AddComponent<BattleHUD>();
            Set(hud, "skillSlots", Array.Empty<SkillSlotUI>());
            GridInputHandler input = Child("Input").AddComponent<GridInputHandler>();
            Set(input, "selectedCharacter", player); Set(input, "gridSystem", grid); Set(input, "battleHUD", hud);
            var routine = (IEnumerator)Call(input, "ExecuteSkill", new RockExcavateSkill(), obstacle);
            // Stop at the pickup presentation boundary: EditMode cannot run delayed Destroy.
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            int iterations = 0;
            while (stack.Count > 0 && !pickup.IsCollected)
            {
                Assert.That(++iterations, Is.LessThan(100));
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) stack.Push(nested);
            }
            foreach (IEnumerator pending in stack) (pending as IDisposable)?.Dispose();
            Assert.That(player.GridPosition, Is.EqualTo(destination));
            Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(99));
            Assert.That(inventory.GetAmount(HelmetType.Rock), Is.EqualTo(full ? 3 : 1));
            Assert.That(pickup.IsCollected, Is.EqualTo(!full));
            Assert.That(input.IsExecutingAction, Is.False);
        }
        finally
        {
            typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, oldCatalog);
            typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, oldInventory);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previousScene);
        }
    }

    [TestCase(1200f)] [TestCase(500f)]
    public void LongTooltipsWrapAndGrowVerticallyWithinTheCanvas(float canvasWidth)
    {
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvas.transform.SetParent(root.transform, false);
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(canvasWidth, 900f);
        var panel = new GameObject("Tooltip", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvas.transform, false);
        panel.SetActive(false);
        var group = panel.GetComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(80, 80, 36, 36);
        group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandHeight = false;
        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        Assert.That(font, Is.Not.Null);
        TMPro.TextMeshProUGUI Label(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(panel.transform, false);
            var label = go.GetComponent<TMPro.TextMeshProUGUI>();
            label.font = font; label.fontSize = 20;
            return label;
        }
        var title = Label("Title"); var description = Label("Description"); var stats = Label("Stats");
        BattleHUD hud = Child("HUD").AddComponent<BattleHUD>();
        Set(hud, "tooltipPanel", panel); Set(hud, "tooltipTitleText", title);
        Set(hud, "tooltipDescriptionText", description); Set(hud, "tooltipStatsText", stats);
        hud.ShowGenericTooltip("Imparable", "No te pueden empujar.");
        var rect = (RectTransform)panel.transform;
        float shortHeight = rect.rect.height;
        hud.ShowGenericTooltip(Guard.Name, Guard.Description);
        description.ForceMeshUpdate();
        Assert.That(rect.rect.width, Is.EqualTo(Mathf.Min(600f, canvasWidth * 0.9f)).Within(0.01f));
        Assert.That(rect.rect.height, Is.GreaterThan(shortHeight));
        Assert.That(description.textInfo.lineCount, Is.GreaterThan(1));
        Assert.That(description.rectTransform.rect.height, Is.GreaterThanOrEqualTo(description.preferredHeight - 0.1f));
        hud.ShowSkillTooltip(new RockExcavateSkill(), 2);
        Assert.That(rect.rect.width, Is.LessThanOrEqualTo(600f));
        Assert.That(rect.rect.width, Is.LessThan(canvasWidth));
    }

    [Test]
    public void InventoryCreatesOnlyOneRockSlotAndKeepsItsOwnButtonTarget()
    {
        GameObject panel = Child("Inventory");
        GameObject container = Child("Slots"); container.transform.SetParent(panel.transform);
        container.AddComponent<GridLayoutGroup>();
        GameObject template = new("Water Slot", typeof(RectTransform), typeof(Image), typeof(Button));
        template.transform.SetParent(container.transform, false);
        var slot = template.AddComponent<HelmetSlotUI>(); slot.ConfigureType(HelmetType.Water);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(template.GetComponent<Button>().onClick, slot.SelectHelmet);
        HelmetInventoryUI inventory = Child("Inventory UI").AddComponent<HelmetInventoryUI>();
        Set(inventory, "inventoryPanel", panel);
        Call(inventory, "EnsureRockSlot"); Call(inventory, "EnsureRockSlot");
        HelmetSlotUI[] slots = panel.GetComponentsInChildren<HelmetSlotUI>(true);
        Assert.That(slots.Length, Is.EqualTo(2));
        HelmetSlotUI rockSlot = slots.Single(s => s.HelmetType == HelmetType.Rock);
        Assert.That(rockSlot.GetComponent<Button>().onClick.GetPersistentTarget(0), Is.SameAs(rockSlot));
    }

    [Test]
    public void ExcavationHoldsTheBuriedPoseUntilTheExitAndRestoresTheModel()
    {
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        Vector3 origin = player.transform.position;
        AdvanceAnimation(animation.PlayExcavateEnter(), animator);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(animator.transform.localPosition.y, Is.EqualTo(-4.04f).Within(0.01f));
        animator.Update(1.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(player.transform.position, Is.EqualTo(origin));
        AdvanceAnimation(animation.PlayExcavateExit(), animator);
        animation.FinishExcavate();
        Assert.That(animator.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(animator.GetFloat("ExcavateSpeed"), Is.EqualTo(1f));
    }

    [TestCase(1, false)] [TestCase(2, false)]
    [TestCase(1, true)] [TestCase(2, true)]
    public void AnimatedExcavationMovesOnlyAfterBurialAndRevalidatesTheLanding(int size, bool blockLanding)
    {
        Resize(player, size);
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        Vector2Int origin = player.GridPosition;
        Vector2Int obstacle = origin + Vector2Int.right * size;
        Vector2Int destination = origin + Vector2Int.right * (size + 1);
        grid.SetCellType(obstacle, CellType.Blocked);
        IEnumerator skill = new RockExcavateSkill().Execute(player, obstacle, grid);
        Assert.That(skill.MoveNext(), Is.True);
        AdvanceAnimation((IEnumerator)skill.Current, animator);
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(100));
        Assert.That(animator.transform.localPosition.y, Is.LessThan(-4f));
        if (blockLanding) grid.SetCellType(destination + Vector2Int.one * (size - 1), CellType.Blocked);
        if (skill.MoveNext())
        {
            Assert.That(blockLanding, Is.False);
            Assert.That(player.GridPosition, Is.EqualTo(destination));
            Assert.That(animator.transform.localPosition.y, Is.LessThan(-4f));
            AdvanceAnimation((IEnumerator)skill.Current, animator);
            Assert.That(skill.MoveNext(), Is.False);
        }
        Assert.That(player.GridPosition, Is.EqualTo(blockLanding ? origin : destination));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(blockLanding ? 100 : 99));
        Assert.That(animator.transform.localPosition, Is.EqualTo(Vector3.zero));
        foreach (Vector2Int cell in player.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
    }

    [TestCase(0.7f, 0.5f)] [TestCase(0.7f, 2f)]
    [TestCase(1.1f, 0.5f)] [TestCase(1.1f, 2f)]
    public void ExcavationMatchesBothVfxPhasesAtDifferentAnimatorSpeeds(float travelDelay, float animatorSpeed)
    {
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        animator.speed = animatorSpeed;
        RockSkillVFX vfx = player.gameObject.AddComponent<RockSkillVFX>();
        Set(vfx, "excavateTravelDelay", travelDelay);
        float duration = vfx.ExcavateEnterDuration;
        float elapsed = AdvanceTimedAnimation(animation.PlayExcavateEnter(duration), animator);
        Assert.That(elapsed, Is.EqualTo(travelDelay * 0.2f / 0.7f).Within(0.02f));
        Assert.That(animator.transform.localPosition.y, Is.EqualTo(-4.04f).Within(0.01f));
        animator.Update(0.5f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0.5f).Within(0.001f));
        elapsed = AdvanceTimedAnimation(animation.PlayExcavateExit(duration), animator);
        Assert.That(elapsed, Is.EqualTo(duration).Within(0.02f));
        animation.FinishExcavate();
        Assert.That(animator.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(animator.GetFloat("ExcavateSpeed"), Is.EqualTo(1f));
    }

    [TestCase(1, 0.7f, false)] [TestCase(2, 1.1f, false)]
    [TestCase(1, 1.1f, true)] [TestCase(2, 0.7f, true)]
    public void ExcavationStartsVfxBeforeBurialAndWaitsBeforeMoving(int size, float travelDelay, bool blockLanding)
    {
        Resize(player, size);
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        var template = Child("Excavation template").AddComponent<VisualEffect>();
        template.gameObject.SetActive(false);
        RockSkillVFX vfx = player.gameObject.AddComponent<RockSkillVFX>();
        Set(vfx, "excavate", template);
        Set(vfx, "excavateTravelDelay", travelDelay);
        Vector2Int origin = player.GridPosition;
        Vector2Int obstacle = origin + Vector2Int.right * size;
        Vector2Int destination = origin + Vector2Int.right * (size + 1);
        grid.SetCellType(obstacle, CellType.Blocked);
        IEnumerator skill = new RockExcavateSkill().Execute(player, obstacle, grid);
        VisualEffect effect = null;
        try
        {
            Assert.That(skill.MoveNext(), Is.True);
            IEnumerator presentation = (IEnumerator)skill.Current;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode!"));
            Assert.That(presentation.MoveNext(), Is.True);
            effect = UnityEngine.Object.FindObjectsByType<VisualEffect>(FindObjectsSortMode.None)
                .Single(instance => instance != template);
            Assert.That(effect.playRate, Is.EqualTo(0.7f / travelDelay).Within(0.001f));
            Assert.That(player.GridPosition, Is.EqualTo(origin));
            Assert.That(animator.transform.localPosition, Is.EqualTo(Vector3.zero));
            float elapsed = AdvanceTimedAnimation((IEnumerator)presentation.Current, animator);
            Assert.That(elapsed, Is.EqualTo(vfx.ExcavateEnterDuration).Within(0.02f));
            Assert.That(presentation.MoveNext(), Is.True);
            Assert.That(presentation.Current, Is.TypeOf<WaitForSeconds>());
            Assert.That(player.GridPosition, Is.EqualTo(origin));
            Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(100));
            if (blockLanding) grid.SetCellType(destination + Vector2Int.one * (size - 1), CellType.Blocked);
            Assert.That(presentation.MoveNext(), Is.False);
            if (skill.MoveNext())
            {
                Assert.That(blockLanding, Is.False);
                Assert.That(player.GridPosition, Is.EqualTo(destination));
                elapsed = AdvanceTimedAnimation((IEnumerator)skill.Current, animator);
                Assert.That(elapsed, Is.EqualTo(vfx.ExcavateEnterDuration).Within(0.02f));
                Assert.That(skill.MoveNext(), Is.False);
            }
            Assert.That(player.GridPosition, Is.EqualTo(blockLanding ? origin : destination));
            Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(blockLanding ? 100 : 99));
            Assert.That(animator.transform.localPosition, Is.EqualTo(Vector3.zero));
            foreach (Vector2Int cell in player.GetOccupiedCells())
                Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
        }
        finally
        {
            ((IDisposable)skill).Dispose();
            if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
        }
    }

    [TestCase(0.5f)] [TestCase(1f)] [TestCase(2f)]
    public void TwirlSynchronizesSlashAndImpactAtDifferentSpeedsAndDoesNotRepeatFromIdle(float speed)
    {
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        animator.speed = speed;
        animator.Update(0.2f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.metarig|IDL"), Is.True);
        float slashTime = -1f;
        float slashRate = 0f;
        float impactTime = AdvanceTimedAnimation(animation.PlayTwirlUntilHitFrame(rate =>
        {
            slashTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime * (61f / 60f) / speed;
            slashRate = rate;
        }), animator);
        Assert.That(slashTime, Is.EqualTo(0.25f / speed).Within(0.02f));
        Assert.That(slashRate, Is.EqualTo(speed).Within(0.001f));
        Assert.That(impactTime, Is.EqualTo(0.4f / speed).Within(0.03f));
        Assert.That(animation.TwirlHitFrameReached, Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Twirl_Tete"), Is.True);
        float recoveryTime = AdvanceTimedAnimation(animation.WaitTwirlFinished(), animator);
        Assert.That(impactTime + recoveryTime, Is.GreaterThanOrEqualTo((61f / 60f) / speed));
        animation.FinishTwirl();
        animator.Update(3f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.metarig|IDL"), Is.True);
        Assert.That(animator.IsInTransition(0), Is.False);
        // A second Q must restart the same timeline without waiting for attack events.
        Assert.That(AdvanceTimedAnimation(animation.PlayTwirlUntilHitFrame(null), animator),
            Is.EqualTo(impactTime).Within(0.02f));
        animation.FinishTwirl();
    }

    [TestCase(1, 1)] [TestCase(1, 2)] [TestCase(2, 1)] [TestCase(2, 2)]
    public void AnimatedSpinWaitsForImpactAndPreservesTheCastersWholeFootprint(int casterSize, int targetSize)
    {
        Resize(player, casterSize);
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        Character target = Actor(false, player.GridPosition + Vector2Int.right * (casterSize + 1), targetSize);
        target.AddPassive(new ColossusPassive());
        Vector2Int origin = player.GridPosition;
        Quaternion rotation = animator.transform.localRotation;
        var routine = new RockBasicSkill().Execute(player, origin, grid);
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            var windup = (IEnumerator)routine.Current;
            Assert.That(windup.MoveNext(), Is.True);
            animator.Update(0.2f);
            Assert.That(windup.MoveNext(), Is.True);
            Assert.That(target.Stats.CurrentHP, Is.EqualTo(100));
            Assert.That(Guard.ArmorStacks, Is.Zero);
            AdvanceTimedAnimation(windup, animator);
            Assert.That(target.Stats.CurrentHP, Is.EqualTo(100));
            Assert.That(routine.MoveNext(), Is.True); // Damage, then the push coroutine.
            Drain((IEnumerator)routine.Current);
            Assert.That(target.Stats.CurrentHP, Is.EqualTo(81));
            Assert.That(Guard.ArmorStacks, Is.EqualTo(1));
            Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(98));
            Assert.That(routine.MoveNext(), Is.True);
            AdvanceTimedAnimation((IEnumerator)routine.Current, animator);
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(player.GridPosition, Is.EqualTo(origin));
            Assert.That(animator.transform.localRotation, Is.EqualTo(rotation));
            foreach (Vector2Int cell in player.GetOccupiedCells())
                Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
        }
        finally { ((IDisposable)routine).Dispose(); }
    }

    [Test]
    public void InterruptedSpinBeforeImpactDoesNotDamageAndClearsItsTrigger()
    {
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        Character target = Actor(false, new Vector2Int(7, 5));
        var routine = new RockBasicSkill().Execute(player, player.GridPosition, grid);
        Assert.That(routine.MoveNext(), Is.True);
        var windup = (IEnumerator)routine.Current;
        Assert.That(windup.MoveNext(), Is.True);
        animator.Update(0.2f);
        Assert.That(windup.MoveNext(), Is.True);
        ((IDisposable)routine).Dispose();
        Assert.That(windup.MoveNext(), Is.False);
        Assert.That(animation.TwirlHitFrameReached, Is.False);
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(Guard.ArmorStacks, Is.Zero);
        animator.Update(3f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.metarig|IDL"), Is.True);
        Assert.That(animator.IsInTransition(0), Is.False);
    }

    [TestCase(UpgradeType.RockExcavationStrike, UpgradeDefinition.RockExcavationStrikeId, 30)]
    [TestCase(UpgradeType.RockShieldDuration, UpgradeDefinition.RockShieldDurationId, 20)]
    public void EarthUpgradePurchasesUseGoldPersistAndRejectDuplicates(UpgradeType type, string id, int cost)
    {
        Set(definition, "upgradeType", type); Set(definition, "upgradeId", id); Set(definition, "goldCost", cost);
        session.TryAddCurrency(RunCurrency.XideriaFragments, 50);
        session.TryAddCurrency(RunCurrency.Gold, cost - 1);
        Assert.That(session.TryPurchaseUpgrade(definition, out var result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.InsufficientGold));
        Assert.That(session.HasUpgrade(id), Is.False);
        session.TryAddCurrency(RunCurrency.Gold, 1);
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.True);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.Success));
        Assert.That(session.Gold, Is.Zero);
        Assert.That(session.XideriaFragments, Is.EqualTo(50));
        Assert.That(session.TryPurchaseUpgrade(definition, out result), Is.False);
        Assert.That(result, Is.EqualTo(UpgradePurchaseResult.AlreadyPurchased));
        session.ResetSession();
        var restored = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(session.RunState));
        Assert.That(restored.HasUpgrade(id), Is.True);
        session.AbandonRun();
        Assert.That(session.HasUpgrade(id), Is.False);
        Assert.That(player.GetPassiveEffects().OfType<RockExcavationStrikePassive>(), Is.Empty);
        Assert.That(RockGuardPassive.ShieldDuration(player), Is.EqualTo(1));
    }

    [Test]
    public void ExcavationPassiveAppearsOnceOnlyForThePlayersEquippedEarthHelmet()
    {
        Assert.That(player.GetPassiveEffects().OfType<RockExcavationStrikePassive>(), Is.Empty);
        BuyEarth(UpgradeType.RockExcavationStrike);
        player.AddPassive(new RockExcavationStrikePassive());
        Assert.That(player.GetPassiveEffects().OfType<RockExcavationStrikePassive>().Count(), Is.EqualTo(1));
        Character enemy = Actor(false, new Vector2Int(10, 10));
        enemy.EquipHelmet(HelmetFactory.CreateRockHelmet());
        Assert.That(enemy.GetPassiveEffects().OfType<RockExcavationStrikePassive>(), Is.Empty);
        player.UnequipHelmet();
        Assert.That(player.GetPassiveEffects().OfType<RockExcavationStrikePassive>(), Is.Empty);
        player.EquipHelmet(HelmetFactory.CreateRockHelmet());
        Assert.That(player.GetPassiveEffects().OfType<RockExcavationStrikePassive>().Count(), Is.EqualTo(1));
        session.AbandonRun();
        Assert.That(player.GetPassiveEffects().OfType<RockExcavationStrikePassive>(), Is.Empty);
    }

    [TestCase(1, 1)] [TestCase(1, 2)] [TestCase(2, 1)] [TestCase(2, 2)]
    public void ExcavationArrivalPerformsExactlyOneFullFreeSpinEvenAtZeroEnergy(int casterSize, int enemySize)
    {
        BuyEarth(UpgradeType.RockExcavationStrike);
        Resize(player, casterSize);
        Vector2Int obstacle = player.GridPosition + Vector2Int.right * casterSize;
        Vector2Int destination = player.GridPosition + Vector2Int.right * (casterSize + 1);
        grid.SetCellType(obstacle, CellType.Blocked);
        Character first = Actor(false, destination + Vector2Int.right * casterSize, enemySize);
        Character second = Actor(false, destination + Vector2Int.up * casterSize);
        Character inBasicArea = Actor(false, destination + Vector2Int.down * 2);
        foreach (Character enemy in new[] { first, second, inBasicArea }) enemy.AddPassive(new ColossusPassive());
        player.Stats.SetCurrentEnergy(1);
        var skill = new RockExcavateSkill();
        Drain(skill.Execute(player, obstacle, grid));
        Assert.That(player.GridPosition, Is.EqualTo(destination));
        Assert.That(player.Stats.CurrentEnergy, Is.Zero);
        Assert.That(Guard.ArmorStacks, Is.EqualTo(3));
        foreach (Character enemy in new[] { first, second, inBasicArea })
            Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(81));
        Assert.That(new RockBasicSkill().CanUse(player, destination, grid), Is.False);
        Assert.That(player.GetSkillCooldown(new RockBasicSkill()), Is.Zero);
        foreach (Vector2Int cell in player.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
    }

    [TestCase(false, 0)] [TestCase(true, 1)] [TestCase(true, 2)] [TestCase(true, 3)] [TestCase(true, 4)]
    public void ExcavationDoesNotSpinWithoutPurchaseOrWithoutALivingAdjacentEnemy(bool purchased, int targetKind)
    {
        if (purchased) BuyEarth(UpgradeType.RockExcavationStrike);
        Vector2Int obstacle = player.GridPosition + Vector2Int.right;
        Vector2Int destination = player.GridPosition + Vector2Int.right * 2;
        grid.SetCellType(obstacle, CellType.Blocked);
        Vector2Int offset = targetKind == 2 ? Vector2Int.one : Vector2Int.right * (targetKind == 3 ? 2 : 1);
        Character target = Actor(targetKind == 1, destination + offset);
        if (targetKind == 4) Set(target, "isDying", true);
        player.Stats.SetCurrentEnergy(1);
        Drain(new RockExcavateSkill().Execute(player, obstacle, grid));
        Assert.That(player.GridPosition, Is.EqualTo(destination));
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(Guard.ArmorStacks, Is.Zero);
        Assert.That(player.Stats.CurrentEnergy, Is.Zero);
    }

    [Test]
    public void EachExcavationGrantsOneNewSpinWithoutRecursingOrChargingForTheBasic()
    {
        BuyEarth(UpgradeType.RockExcavationStrike);
        Vector2Int origin = player.GridPosition;
        Vector2Int obstacle = origin + Vector2Int.right;
        Vector2Int destination = origin + Vector2Int.right * 2;
        grid.SetCellType(obstacle, CellType.Blocked);
        Character atOrigin = Actor(false, origin + Vector2Int.up);
        Character atDestination = Actor(false, destination + Vector2Int.up);
        atOrigin.AddPassive(new ColossusPassive()); atDestination.AddPassive(new ColossusPassive());
        player.Stats.SetCurrentEnergy(2);
        var skill = new RockExcavateSkill();
        Drain(skill.Execute(player, obstacle, grid));
        Assert.That(atDestination.Stats.CurrentHP, Is.EqualTo(81));
        Assert.That(atOrigin.Stats.CurrentHP, Is.EqualTo(100));
        Drain(skill.Execute(player, obstacle, grid));
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(atDestination.Stats.CurrentHP, Is.EqualTo(81));
        Assert.That(atOrigin.Stats.CurrentHP, Is.EqualTo(81));
        Assert.That(Guard.ArmorStacks, Is.EqualTo(2));
        Assert.That(player.Stats.CurrentEnergy, Is.Zero);
    }

    [Test]
    public void FailedExcavationLandingDoesNotGrantAnArrivalAttackOrSpendEnergy()
    {
        BuyEarth(UpgradeType.RockExcavationStrike);
        Vector2Int origin = player.GridPosition;
        Vector2Int obstacle = origin + Vector2Int.right;
        Vector2Int destination = origin + Vector2Int.right * 2;
        grid.SetCellType(obstacle, CellType.Blocked);
        Character enemy = Actor(false, destination + Vector2Int.up);
        player.Stats.SetCurrentEnergy(1);
        var routine = new RockExcavateSkill().Execute(player, obstacle, grid);
        Assert.That(routine.MoveNext(), Is.True);
        grid.SetCellType(destination, CellType.Blocked);
        Drain(routine);
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(1));
        Assert.That(enemy.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(Guard.ArmorStacks, Is.Zero);
    }

    [TestCase(false)] [TestCase(true)]
    public void ShieldDurationUpgradeKeepsOnlyUnspentChargesForExactlyOneExtraOwnTurn(bool purchased)
    {
        if (purchased) BuyEarth(UpgradeType.RockShieldDuration);
        Guard.AddArmor(2);
        var shield = new RockShieldSkill();
        Drain(shield.Execute(player, player.GridPosition, grid));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(95));
        Assert.That(player.GetSkillCooldown(shield), Is.EqualTo(3));
        Assert.That(Guard.ShieldCharges, Is.EqualTo(3));
        Assert.That(Guard.ShieldTurnsRemaining, Is.EqualTo(purchased ? 2 : 1));
        Assert.That(Guard.ModifyIncomingDamage(player, 100, DamageType.Physical), Is.EqualTo(70));
        Assert.That(Guard.ShieldCharges, Is.EqualTo(2));
        player.StartTurn();
        Assert.That(Guard.ShieldCharges, Is.EqualTo(purchased ? 2 : 0));
        Assert.That(Guard.ArmorStacks, Is.EqualTo(2));
        Assert.That(Guard.ShieldTurnsRemaining, Is.EqualTo(purchased ? 1 : 0));
        player.StartTurn();
        Assert.That(Guard.ShieldCharges, Is.Zero);
        Assert.That(Guard.ArmorStacks, Is.Zero);
        Assert.That(Guard.ShieldTurnsRemaining, Is.Zero);
    }

    [Test]
    public void ShieldUpgradeDoesNotAddChargesOrAffectEnemiesAndIsLostWithTheHelmet()
    {
        BuyEarth(UpgradeType.RockShieldDuration);
        Guard.ActivateShield(2);
        for (int i = 0; i < 3; i++) Guard.ModifyIncomingDamage(player, 10, DamageType.Physical);
        Assert.That(Guard.ShieldCharges, Is.Zero);
        player.StartTurn();
        Assert.That(Guard.ShieldCharges, Is.Zero);
        Character enemy = Actor(false, new Vector2Int(10, 10));
        enemy.EquipHelmet(HelmetFactory.CreateRockHelmet());
        Assert.That(RockGuardPassive.ShieldDuration(enemy), Is.EqualTo(1));
        player.UnequipHelmet();
        Assert.That(RockGuardPassive.ShieldDuration(player), Is.EqualTo(1));
        player.EquipHelmet(HelmetFactory.CreateRockHelmet());
        Assert.That(RockGuardPassive.ShieldDuration(player), Is.EqualTo(2));
    }

    private void BuyEarth(UpgradeType type)
    {
        bool strike = type == UpgradeType.RockExcavationStrike;
        int cost = strike ? 30 : 20;
        Set(definition, "upgradeType", type);
        Set(definition, "upgradeId", strike ? UpgradeDefinition.RockExcavationStrikeId : UpgradeDefinition.RockShieldDurationId);
        Set(definition, "goldCost", cost);
        session.TryAddCurrency(RunCurrency.Gold, cost);
        Assert.That(session.TryPurchaseUpgrade(definition, out _), Is.True);
    }

    private static float AdvanceTimedAnimation(IEnumerator routine, Animator animator)
    {
        int frames = 0;
        while (routine.MoveNext())
        {
            Assert.That(++frames, Is.LessThan(200), "Animation did not complete");
            animator.Update(0.01f);
        }
        return frames * 0.01f;
    }

    [TestCase(false)] [TestCase(true)]
    public void InterruptedExcavationRestoresTheModelAndAnimationSpeedWithoutSpendingEnergy(bool withVfx)
    {
        Animator animator = ExcavationModel(out CharacterAnimationController animation);
        VisualEffect template = null;
        if (withVfx)
        {
            template = Child("Excavation template").AddComponent<VisualEffect>();
            template.gameObject.SetActive(false);
            RockSkillVFX vfx = player.gameObject.AddComponent<RockSkillVFX>();
            Set(vfx, "excavate", template);
        }
        Vector2Int origin = player.GridPosition;
        Vector2Int obstacle = origin + Vector2Int.right;
        grid.SetCellType(obstacle, CellType.Blocked);
        IEnumerator skill = new RockExcavateSkill().Execute(player, obstacle, grid);
        Assert.That(skill.MoveNext(), Is.True);
        IEnumerator presentation = (IEnumerator)skill.Current;
        VisualEffect effect = null;
        if (withVfx)
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode!"));
            Assert.That(presentation.MoveNext(), Is.True);
            effect = UnityEngine.Object.FindObjectsByType<VisualEffect>(FindObjectsSortMode.None)
                .Single(instance => instance != template);
            AdvanceAnimation((IEnumerator)presentation.Current, animator);
        }
        else AdvanceAnimation(presentation, animator);
        ((IDisposable)skill).Dispose();
        if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
        Assert.That(animator.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(animator.GetFloat("ExcavateSpeed"), Is.EqualTo(1f));
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(100));
    }

    private Animator ExcavationModel(out CharacterAnimationController animation)
    {
        GameObject model = Child("Excavating Tete");
        model.transform.SetParent(player.transform, false);
        Animator animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Prefabs/TeteNew/TeteAnimado.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animation = model.AddComponent<CharacterAnimationController>();
        Set(animation, "animator", animator);
        model.SetActive(true);
        player.gameObject.SetActive(true);
        animator.Rebind();
        animator.Update(0f);
        return animator;
    }

    private static void AdvanceAnimation(IEnumerator routine, Animator animator)
    {
        int frames = 0;
        while (routine.MoveNext())
        {
            Assert.That(++frames, Is.LessThan(100), "Animation did not complete");
            animator.Update(0.05f);
        }
    }


    private RockGuardPassive Guard => player.GetPassiveEffects().OfType<RockGuardPassive>().Single();

    [Test]
    public void RockPickupCatalogAndInventoryEquipTheRequestedHelmetWithCharges()
    {
        HelmetCatalog oldCatalog = HelmetCatalog.Instance;
        HelmetInventoryData oldInventory = HelmetInventoryData.Instance;
        try
        {
            HelmetCatalog catalog = Child("Catalog").AddComponent<HelmetCatalog>();
            Set(catalog, "helmets", new List<HelmetCatalogEntry> { new() { Type = HelmetType.Rock, MaxCharges = 3, MaxStored = 3 } });
            Set(catalog, "rockObstaclePrefab", rock);
            typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, catalog);
            HelmetInventoryData inventory = Child("Inventory Data").AddComponent<HelmetInventoryData>();
            typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, inventory);
            Call(inventory, "InitializeInventory");
            inventory.UnlockHelmet(HelmetType.Rock);
            HelmetPickup pickup = Child("Pickup").AddComponent<HelmetPickup>();
            Set(pickup, "helmetType", HelmetType.Rock);
            Call(pickup, "Awake");
            Assert.That(pickup.GetHelmet().Type, Is.EqualTo(HelmetType.Rock));
            Assert.That(inventory.AddHelmet(pickup.GetHelmet().Type), Is.True);
            Assert.That(player.EquipHelmetFromInventory(HelmetType.Rock), Is.True);
            Assert.That(player.EquippedHelmet.Type, Is.EqualTo(HelmetType.Rock));
            Assert.That(player.HelmetCharges, Is.EqualTo(3));
            Assert.That(inventory.GetAmount(HelmetType.Rock), Is.Zero);
            Assert.That(player.GetSkill(3).CanUse(player, player.GridPosition, grid), Is.True);
        }
        finally
        {
            typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, oldCatalog);
            typeof(HelmetInventoryData).GetProperty(nameof(HelmetInventoryData.Instance)).SetValue(null, oldInventory);
        }
    }

    [Test]
    public void BlankPassiveIconKeepsAVisibleHoverTarget()
    {
        StatusIconUI icon = Child("Status").AddComponent<StatusIconUI>();
        Image image = Child("Icon").AddComponent<Image>();
        var text = Child("Turns").AddComponent<TMPro.TextMeshProUGUI>();
        Set(icon, "iconImage", image); Set(icon, "turnText", text);
        icon.Setup(null, "Imparable", "", 0, null, true);
        Assert.That(image.enabled, Is.True);
        Assert.That(image.sprite, Is.Null);
        Assert.That(text.gameObject.activeSelf, Is.False);
    }
    private GameObject Child(string name)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(root.transform, false); return go;
    }
    private Character Actor(bool isPlayer, Vector2Int cell, int size = 1)
    {
        Character result = Child("Actor").AddComponent<Character>();
        Set(result, "isPlayer", isPlayer); Set(result, "_grid", grid);
        Set(result, "gridWidth", size); Set(result, "gridHeight", size);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(result, new CharacterStats(20, 0, 100, 100, 0));
        result.Stats.SetCurrentEnergy(100); result.SetGridPosition(cell);
        result.transform.position = grid.GridToWorld(cell, result) + result.VisualOffset;
        grid.RegisterCharacter(result); characters.Add(result); return result;
    }
    private void Resize(Character actor, int size)
    {
        grid.UnregisterCharacter(actor); Set(actor, "gridWidth", size); Set(actor, "gridHeight", size); grid.RegisterCharacter(actor);
    }
    private void Drain(IEnumerator routine, Dictionary<Character, Vector2Int> destinations = null)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        int iterations = 0;
        while (stack.Count > 0)
        {
            Assert.That(++iterations, Is.LessThan(1000));
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator child) { stack.Push(child); continue; }
            if (destinations != null)
                foreach (var destination in destinations)
                    destination.Key.transform.position = grid.GridToWorld(destination.Value, destination.Key) + destination.Key.VisualOffset;
        }
    }
}

public class RockHelmetAssetTests
{
    [TestCase("Helmet_Rock", true)] [TestCase("Helmet_Rock_Equipped", false)]
    public void RockPrefabsHaveTheirOwnControllerAndCorrectPickupComponents(string name, bool pickup)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HelmetPrefabs/" + name + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        Animator animator = prefab.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.applyRootMotion, Is.False);
        var controller = animator.runtimeAnimatorController as AnimatorController;
        Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo("Assets/Animation/Helmet_Animation/Rock/Helmet_Rock.controller"));
        Assert.That(controller.parameters.Any(p => p.name == "IsDisol" && p.type == AnimatorControllerParameterType.Trigger), Is.True);
        Assert.That(prefab.GetComponent<HelmetPickup>() != null, Is.EqualTo(pickup));
        Assert.That(prefab.GetComponent<HelmetPickupAnimator>() != null, Is.EqualTo(pickup));
        Assert.That(prefab.GetComponent<SphereCollider>() != null, Is.EqualTo(pickup));
        if (pickup)
        {
            var serialized = new SerializedObject(prefab.GetComponent<HelmetPickup>());
            Assert.That(serialized.FindProperty("helmetType").intValue, Is.EqualTo((int)HelmetType.Rock));
            Assert.That(serialized.FindProperty("rockObstaclePrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(new SerializedObject(prefab.GetComponent<HelmetPickupAnimator>()).FindProperty("animator").objectReferenceValue, Is.SameAs(animator));
        }
    }
}
