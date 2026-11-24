using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

public class BossPhaseTwoTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character boss;
    private Character player;
    private BossAI ai;
    private UnityEngine.Random.State previousRandom;
    private Action<Character> previousStarted, previousEnded, previousInterrupted;
    private bool previousGameOver;

    [SetUp]
    public void CreateCombat()
    {
        // FindObjectsOfType intentionally excludes editor preview scenes.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        previousRandom = UnityEngine.Random.state;
        previousStarted = GameEvents.OnTurnStarted; previousEnded = GameEvents.OnTurnEnded;
        previousInterrupted = GameEvents.OnTurnInterrupted; previousGameOver = GameSession.IsGameOver;
        GameEvents.OnTurnStarted = null; GameEvents.OnTurnEnded = null; GameEvents.OnTurnInterrupted = null;
        GameSession.IsGameOver = false;
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 12); Set(grid, "height", 12); Set(grid, "cellSize", 1f); Call(grid, "GenerateGrid");
        grid.gameObject.SetActive(true);
        boss = Actor(false, new Vector2Int(2, 2), 2);
        boss.AddPassive(new ColossusPassive());
        ai = boss.gameObject.AddComponent<BossAI>();
        Call(ai, "Awake"); Call(ai, "OnEnable");
        player = Actor(true, new Vector2Int(9, 9));
    }

    [TearDown]
    public void RestoreGlobals()
    {
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Random.state = previousRandom;
        GameEvents.OnTurnStarted = previousStarted; GameEvents.OnTurnEnded = previousEnded;
        GameEvents.OnTurnInterrupted = previousInterrupted; GameSession.IsGameOver = previousGameOver;
    }

    [TestCase(false)] [TestCase(true)]
    public void RangedBasicsCanHitAFartherPartOfAnAdjacentLargeEnemy(bool water)
    {
        Move(player, new Vector2Int(1, 2));
        SkillBase skill = water ? (SkillBase)new WaterBasicSkill() : new FireBasicAttackSkill();
        player.Stats.SetCurrentEnergy(8);
        Assert.That(skill.CanUse(player, boss.GridPosition, grid), Is.True);
        Assert.That(skill.CanUseFromPosition(player, player.GridPosition, boss, grid, 8), Is.True);
        grid.UnregisterCharacter(boss); Set(boss, "gridWidth", 1); Set(boss, "gridHeight", 1); grid.RegisterCharacter(boss);
        Assert.That(skill.CanUse(player, boss.GridPosition, grid), Is.False, "A 1x1 target still respects the minimum range");
        Move(boss, new Vector2Int(10, 2));
        Assert.That(skill.CanUse(player, boss.GridPosition, grid), Is.False);
    }

    [Test]
    public void HalfHealthRequestsAnImmediateTurnExactlyOnceAndChangesSkills()
    {
        var manager = Child("Turns").AddComponent<TurnManager>(); manager.gameObject.SetActive(true);
        boss.TakeDamage(31, DamageType.Physical);
        Assert.That(ai.IsPhaseTwo, Is.False);
        boss.TakeDamage(1, DamageType.Physical);
        Assert.That(ai.IsPhaseTwo, Is.True);
        Assert.That(ai.IsTransforming, Is.True);
        Assert.That(manager.HasPendingImmediateTurn, Is.True);
        Assert.That(boss.GetActiveSkills().Count, Is.EqualTo(2));
        Assert.That(boss.GetActiveSkills().All(skill => skill is BossPhaseTwoSkill), Is.True);
        var skills = boss.GetActiveSkills().ToArray();
        boss.TakeDamage(1, DamageType.Physical);
        CollectionAssert.AreEqual(skills, boss.GetActiveSkills());
    }

    [Test]
    public void DeadBossDoesNotRequestATransformation()
    {
        boss.Stats.SetCurrentHP(0);
        ai.CheckPhase(boss);
        Assert.That(ai.IsPhaseTwo, Is.False);
    }

    [Test]
    public void CenterSearchUsesTheWholeBodyAndChoosesTheClosestReachableOrigin()
    {
        Assert.That(ai.TryFindCenterPath(out var path), Is.True);
        Assert.That(path.Last(), Is.EqualTo(new Vector2Int(5, 5)));
        grid.SetCellType(new Vector2Int(6, 6), CellType.Blocked);
        Assert.That(ai.TryFindCenterPath(out path), Is.True);
        Assert.That(path.Last(), Is.Not.EqualTo(new Vector2Int(5, 5)));
        foreach (Vector2Int step in path) Assert.That(grid.CanCharacterOccupy(boss, step, true), Is.True);
        float chosen = ((Vector2)path.Last() - new Vector2(5, 5)).sqrMagnitude;
        Assert.That(chosen, Is.EqualTo(1f));
        grid.SetCellType(new Vector2Int(4, 5), CellType.Void);
        ai.TryFindCenterPath(out path);
        foreach (Vector2Int step in path)
            foreach (Vector2Int cell in boss.GetOccupiedCells(step)) Assert.That(grid.GetCell(cell).Type, Is.Not.EqualTo(CellType.Void));
    }

    [TestCase(1)] [TestCase(2)]
    public void MeleeHitsOncePushesTwoCellsAndKeepsBossStationary(int playerSize)
    {
        PhaseTwo();
        grid.UnregisterCharacter(player); Set(player, "gridWidth", playerSize); Set(player, "gridHeight", playerSize);
        Move(player, new Vector2Int(4, 2));
        var skill = new BossPhaseTwoSkill(ai, false);
        Vector2Int bossOrigin = boss.GridPosition, targetOrigin = player.GridPosition;
        Assert.That(skill.CanUse(boss, player.GridPosition, grid), Is.True);
        Drain(skill.Execute(boss, player.GridPosition, grid), player, targetOrigin + Vector2Int.right * 2);
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(94));
        Assert.That(player.GridPosition, Is.EqualTo(targetOrigin + Vector2Int.right * 2));
        Assert.That(boss.GridPosition, Is.EqualTo(bossOrigin));
        Assert.That(boss.Stats.CurrentEnergy, Is.EqualTo(5));
        AssertOccupancy(boss); AssertOccupancy(player);
    }

    [TestCase(1)] [TestCase(2)]
    public void RangedImpactUsesAnAdjacentFreeOriginAndLeavesOneRockForTwoBossTurns(int playerSize)
    {
        PhaseTwo();
        grid.UnregisterCharacter(player); Set(player, "gridWidth", playerSize); Set(player, "gridHeight", playerSize);
        Move(player, new Vector2Int(8, 6));
        Set(ai, "temporaryRockPrefab", Child("Rock prototype"));
        Vector2Int origin = player.GridPosition, bossOrigin = boss.GridPosition;
        foreach (Vector2Int cell in player.GetOccupiedCells(origin + Vector2Int.up))
            if (!player.GetOccupiedCells().Contains(cell)) grid.SetCellType(cell, CellType.Blocked);
        foreach (Vector2Int cell in player.GetOccupiedCells(origin + Vector2Int.down))
            if (!player.GetOccupiedCells().Contains(cell)) grid.SetCellType(cell, CellType.Blocked);
        foreach (Vector2Int cell in player.GetOccupiedCells(origin + Vector2Int.right))
            if (!player.GetOccupiedCells().Contains(cell)) grid.SetCellType(cell, CellType.Blocked);
        var skill = new BossPhaseTwoSkill(ai, true);
        Assert.That(skill.CanUse(boss, player.GridPosition, grid), Is.True);
        Drain(skill.Execute(boss, player.GridPosition, grid), player, origin + Vector2Int.left);
        Assert.That(player.GridPosition, Is.EqualTo(origin + Vector2Int.left));
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(95));
        Assert.That(boss.GridPosition, Is.EqualTo(bossOrigin));
        var field = grid.GetComponent<TemporaryRockField>();
        Assert.That(field, Is.Not.Null); Assert.That(field.Count, Is.EqualTo(1));
        Call(field, "Tick", player); Assert.That(field.Count, Is.EqualTo(1));
        Call(field, "Tick", boss); Assert.That(field.Count, Is.EqualTo(1));
        Call(field, "Tick", boss); Assert.That(field.Count, Is.Zero);
        AssertOccupancy(boss); AssertOccupancy(player);
    }

    [Test]
    public void RandomExpulsionCanChooseAVoidCell()
    {
        PhaseTwo(); Move(player, new Vector2Int(8, 6));
        grid.SetCellType(new Vector2Int(8, 7), CellType.Void);
        foreach (Vector2Int cell in new[] { new Vector2Int(7, 6), new Vector2Int(9, 6), new Vector2Int(8, 5) }) grid.SetCellType(cell, CellType.Blocked);
        Drain(new BossPhaseTwoSkill(ai, true).Execute(boss, player.GridPosition, grid), player, new Vector2Int(8, 7));
        Assert.That(player.IsDying, Is.True);
        Assert.That(boss.GridPosition, Is.EqualTo(new Vector2Int(2, 2)));
    }

    [Test]
    public void PushImmunityPreventsDisplacementAndRocksCannotOverwriteThePlayer()
    {
        PhaseTwo(); player.AddPassive(new UnstoppablePassive());
        Set(ai, "temporaryRockPrefab", Child("Rock prototype"));
        Vector2Int origin = player.GridPosition;
        Drain(new BossPhaseTwoSkill(ai, true).Execute(boss, origin, grid));
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(grid.GetComponent<TemporaryRockField>().Count, Is.Zero);
        Assert.That(grid.GetCell(origin).Type, Is.EqualTo(CellType.Ground));
    }

    [Test]
    public void ImmediateBossTurnEndsThePlayerTurnAndStartsTheBoss()
    {
        var manager = Child("Turns").AddComponent<TurnManager>(); manager.enabled = false; manager.gameObject.SetActive(true);
        Set(manager, "turnOrder", new List<Character> { player, boss });
        var starts = new List<Character>(); var ends = new List<Character>(); var interrupted = new List<Character>();
        GameEvents.OnTurnStarted += actor => { starts.Add(actor); actor.StartTurn(); };
        GameEvents.OnTurnEnded += ends.Add; GameEvents.OnTurnInterrupted += interrupted.Add;
        IEnumerator loop = (IEnumerator)Call(manager, "TurnLoop");
        Assert.That(loop.MoveNext(), Is.True);
        Assert.That(manager.CurrentCharacter, Is.SameAs(player));
        boss.TakeDamage(32, DamageType.Physical);
        Assert.That(loop.MoveNext(), Is.True);
        Assert.That(manager.CurrentCharacter, Is.SameAs(boss));
        Assert.That(ends, Does.Contain(player)); Assert.That(interrupted, Is.EqualTo(new[] { player }));
        Assert.That(starts.Take(2), Is.EqualTo(new[] { player, boss }));
        Call(manager, "OnDisable"); ((IDisposable)loop).Dispose();
    }

    [Test]
    public void InterruptibleRoutineDisposesNestedActionsEvenDuringADelay()
    {
        bool innerCleaned = false, outerCleaned = false;
        IEnumerator Inner() { try { yield return new WaitForSeconds(999f); Assert.Fail("Cancelled action continued"); } finally { innerCleaned = true; } }
        IEnumerator Outer() { try { yield return Inner(); Assert.Fail("Cancelled parent continued"); } finally { outerCleaned = true; } }
        var routine = new InterruptibleRoutine(Outer());
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.Current, Is.TypeOf<WaitForSeconds>());
        routine.Cancel();
        Assert.That(innerCleaned && outerCleaned, Is.True);
        Assert.That(routine.MoveNext(), Is.False);
    }

    private void PhaseTwo() { boss.Stats.SetCurrentHP(32); ai.CheckPhase(boss); }

    [TestCase(1)] [TestCase(2)]
    public void BossUsesThePlayersActualEruptionRockEffectButKeepsItsOwnLifetime(int playerSize)
    {
        PhaseTwo();
        grid.UnregisterCharacter(player); Set(player, "gridWidth", playerSize); Set(player, "gridHeight", playerSize);
        Move(player, new Vector2Int(8, 6));
        GameObject fallback = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/HelmetModels/Rock.fbx");
        Set(ai, "temporaryRockPrefab", fallback);
        var graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Prefabs/P_Skills/Ground_Helmet_Skill/Vfx_RockAttack_R.vfx");
        Assert.That(graph, Is.Not.Null);
        VisualEffect template = Child("Player R rock template").AddComponent<VisualEffect>();
        template.visualEffectAsset = graph;
        Assert.That(template.HasFloat("RockProgress"), Is.True);
        var presentation = player.gameObject.AddComponent<RockSkillVFX>(); Set(presentation, "eruptionRock", template);
        Vector2Int origin = player.GridPosition;
        foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.right })
            foreach (Vector2Int cell in player.GetOccupiedCells(origin + direction))
                if (!player.GetOccupiedCells().Contains(cell)) grid.SetCellType(cell, CellType.Blocked);
        Drain(new BossPhaseTwoSkill(ai, true).Execute(boss, origin, grid), player, origin + Vector2Int.left);
        var field = TemporaryRockField.For(grid);
        Assert.That(field.Count, Is.EqualTo(1));
        VisualEffect effect = grid.GetComponentsInChildren<VisualEffect>().Single();
        Assert.That(effect.visualEffectAsset, Is.SameAs(graph));
        Assert.That(effect.GetFloat("RockProgress"), Is.EqualTo(1.5f));
        Assert.That(grid.transform.Cast<Transform>().Single(t => t.name.StartsWith("Temporary Rock ")).gameObject.activeSelf, Is.False,
            "The helmet fallback must stay hidden when the R effect is available");
        Assert.That(field.TryPlace(player, new Vector2Int(4, 8), fallback, 2), Is.True);
        Assert.That(grid.GetComponentsInChildren<VisualEffect>().All(e => e.visualEffectAsset == graph), Is.True);
        Call(field, "Tick", player); Call(field, "Tick", player);
        Assert.That(field.Count, Is.EqualTo(1), "Player turns cannot expire the boss's rock");
        Call(field, "Tick", boss); Assert.That(field.Count, Is.EqualTo(1));
        Call(field, "Tick", boss); Assert.That(field.Count, Is.Zero);
        AssertOccupancy(boss); AssertOccupancy(player);
    }

    [TestCase(1, 0, true, false)] [TestCase(1, 1, true, false)] [TestCase(1, 2, true, false)] [TestCase(1, 3, true, false)]
    [TestCase(2, 0, true, false)] [TestCase(2, 1, true, false)] [TestCase(2, 2, true, false)] [TestCase(2, 3, true, false)]
    [TestCase(1, 0, false, false)] [TestCase(2, 0, false, false)]
    [TestCase(1, 0, true, true)] [TestCase(2, 0, true, true)]
    [TestCase(1, 0, false, true)] [TestCase(2, 0, false, true)]
    public void SummonedRockKeepsItsPlacementOrCleansUpWhenInterrupted(int size, int heading, bool useEffect, bool interrupt)
    {
        PhaseTwo();
        grid.UnregisterCharacter(player); Set(player, "gridWidth", size); Set(player, "gridHeight", size);
        Vector2Int origin = new Vector2Int(7, 7);
        Move(player, origin);
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int direction = directions[heading];
        foreach (Vector2Int other in directions.Where(d => d != direction))
            foreach (Vector2Int cell in player.GetOccupiedCells(origin + other))
                if (!player.GetOccupiedCells().Contains(cell)) grid.SetCellType(cell, CellType.Blocked);
        Set(ai, "temporaryRockPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/HelmetModels/Rock.fbx"));
        if (useEffect)
        {
            var template = Child("Rock template").AddComponent<VisualEffect>();
            template.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                "Assets/Prefabs/P_Skills/Ground_Helmet_Skill/Vfx_RockAttack_R.vfx");
            Set(player.gameObject.AddComponent<RockSkillVFX>(), "eruptionRock", template);
        }
        GameObject model = Child("Boss animation");
        Animator animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animation/Jefe/Combat/BossCombat.controller");
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var animation = model.AddComponent<CharacterAnimationController>();
        Set(animation, "animator", animator); Set(ai, "animation", animation);
        model.SetActive(true); animator.Rebind(); animator.Update(0f);
        var field = TemporaryRockField.For(grid);
        Transform summoned = null;
        Vector3 position = default, scale = default;
        using (var routine = new InterruptibleRoutine(new BossPhaseTwoSkill(ai, true).Execute(boss, origin, grid)))
        {
            int frames = 0;
            while (routine.MoveNext())
            {
                Assert.That(++frames, Is.LessThan(500));
                if (summoned == null)
                {
                    summoned = useEffect ? grid.GetComponentInChildren<VisualEffect>()?.transform :
                        grid.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Temporary Rock "));
                    if (summoned != null)
                    {
                        Assert.That(player.Stats.CurrentHP, Is.EqualTo(100), "The rock must emerge before the damage/push.");
                        Assert.That(field.Count, Is.Zero, "The visual alone must not block the target's cell.");
                        position = summoned.position; scale = summoned.lossyScale;
                        if (interrupt) { routine.Cancel(); break; }
                    }
                }
                animator.Update(0.01f);
                if (player.Stats.CurrentHP < 100)
                    player.transform.position = grid.GridToWorld(origin + direction, player) + player.VisualOffset;
            }
        }
        if (interrupt)
        {
            Assert.That(summoned == null, Is.True, "An interrupted cast must remove its uncommitted rock.");
            Assert.That(field.Count, Is.Zero);
            Assert.That(player.GridPosition, Is.EqualTo(origin));
            Assert.That(player.Stats.CurrentHP, Is.EqualTo(100));
            AssertOccupancy(boss); AssertOccupancy(player);
            return;
        }
        Assert.That(summoned, Is.Not.Null);
        Assert.That(field.Count, Is.EqualTo(1));
        Transform retained = useEffect ? grid.GetComponentInChildren<VisualEffect>().transform :
            grid.transform.Cast<Transform>().Single(t => t.name.StartsWith("Temporary Rock "));
        Assert.That(retained, Is.SameAs(summoned));
        Assert.That(Vector3.Distance(retained.position, position), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(retained.lossyScale, scale), Is.LessThan(0.0001f));
        Assert.That(player.GridPosition, Is.EqualTo(origin + direction));
        AssertOccupancy(boss); AssertOccupancy(player);
    }

    [TestCase(1)] [TestCase(2)]
    public void InterruptingAPushRestoresTheWholeCommittedFootprint(int size)
    {
        grid.UnregisterCharacter(player); Set(player, "gridWidth", size); Set(player, "gridHeight", size);
        Move(player, new Vector2Int(6, 5));
        Vector2Int origin = player.GridPosition;
        var push = new InterruptibleRoutine(PushSystem.PushInDirection(player, Vector2Int.right, 2, grid));
        Assert.That(push.MoveNext(), Is.True);
        player.transform.position += Vector3.right * 0.4f;
        push.Cancel();
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(player.transform.position, Is.EqualTo(grid.GridToWorld(origin, player) + player.VisualOffset));
        AssertOccupancy(player); AssertOccupancy(boss);
    }

    [TestCase(1)] [TestCase(2)]
    public void InterruptingMovementDoesNotCommitOrChargeAnUnfinishedStep(int size)
    {
        grid.UnregisterCharacter(player); Set(player, "gridWidth", size); Set(player, "gridHeight", size);
        Move(player, new Vector2Int(6, 5));
        Vector2Int origin = player.GridPosition;
        var movement = new InterruptibleRoutine(new MoveAction(new List<Vector2Int> { origin + Vector2Int.right })
            .Execute(player));
        Assert.That(movement.MoveNext(), Is.True);
        player.transform.position += Vector3.right * 0.4f;
        movement.Cancel();
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(8));
        Assert.That(player.transform.position, Is.EqualTo(grid.GridToWorld(origin, player) + player.VisualOffset));
        AssertOccupancy(player);
    }

    [Test]
    public void CompletedTransformationNeverRelocatesAgainAndSelectsTheAppropriateAttack()
    {
        PhaseTwo(); Move(boss, new Vector2Int(5, 5)); player.AddPassive(new UnstoppablePassive());
        boss.Stats.SetCurrentEnergy(0);
        Drain(ai.TakeTurn());
        Assert.That(ai.IsTransforming, Is.False);
        Assert.That(boss.GridPosition, Is.EqualTo(new Vector2Int(5, 5)));
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(95));
        Assert.That(boss.Stats.CurrentEnergy, Is.EqualTo(3));
        Assert.That(boss.IsTurnFinished, Is.True);
        Move(player, new Vector2Int(7, 5)); boss.StartTurn();
        Drain(ai.TakeTurn());
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(89));
        Assert.That(boss.GridPosition, Is.EqualTo(new Vector2Int(5, 5)));
    }

    [Test]
    public void InterruptingTheCurrentBossTurnDoesNotTickItsTurnStartTwice()
    {
        var manager = Child("Turns").AddComponent<TurnManager>(); manager.enabled = false; manager.gameObject.SetActive(true);
        Set(manager, "turnOrder", new List<Character> { boss, player });
        int starts = 0, ends = 0;
        GameEvents.OnTurnStarted += actor => { if (actor == boss) { starts++; actor.StartTurn(); } };
        GameEvents.OnTurnEnded += actor => { if (actor == boss) ends++; };
        IEnumerator loop = (IEnumerator)Call(manager, "TurnLoop");
        try
        {
            Assert.That(loop.MoveNext(), Is.True);
            boss.TakeDamage(32, DamageType.Physical);
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(manager.CurrentCharacter, Is.SameAs(boss));
            Assert.That(starts, Is.EqualTo(1)); Assert.That(ends, Is.Zero);
            Assert.That(manager.HasPendingImmediateTurn, Is.False);
        }
        finally { Call(manager, "OnDisable"); ((IDisposable)loop).Dispose(); }
    }

    [TestCase(false)] [TestCase(true)]
    public void InputCannotResumeAnInterruptedMovementEvenBeforeItsFirstFrame(bool started)
    {
        var input = Child("Input").AddComponent<GridInputHandler>();
        Set(input, "gridSystem", grid); Set(input, "selectedCharacter", player);
        Set(input, "playerCharacter", player); Set(input, "isPlayerTurn", true);
        Vector2Int origin = player.GridPosition;
        IEnumerator command = (IEnumerator)Call(input, "ExecuteSelectedAction",
            new MoveAction(new List<Vector2Int> { origin + Vector2Int.left }));
        Assert.That(command.MoveNext(), Is.True);
        var wrapper = (IEnumerator)command.Current;
        if (started)
        {
            Assert.That(wrapper.MoveNext(), Is.True);
            Assert.That(((IEnumerator)wrapper.Current).MoveNext(), Is.True);
            player.transform.position += Vector3.left * 0.4f;
        }
        Call(input, "OnTurnInterrupted", player);
        Drain(wrapper); Drain(command);
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(8));
        Assert.That(player.transform.position, Is.EqualTo(grid.GridToWorld(origin, player) + player.VisualOffset));
        Assert.That(typeof(GridInputHandler).GetField("isPlayerTurn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input), Is.False);
        Assert.That(typeof(GridInputHandler).GetField("isMoving", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input), Is.False);
        AssertOccupancy(player);
    }

    [Test]
    public void PhaseTwoSkillSimulationUsesTheSameWholeBodyAdjacencyAsExecution()
    {
        PhaseTwo(); Move(player, new Vector2Int(4, 2));
        var melee = new BossPhaseTwoSkill(ai, false); var ranged = new BossPhaseTwoSkill(ai, true);
        Assert.That(melee.CanUseFromPosition(boss, boss.GridPosition, player, grid, 8), Is.True);
        Assert.That(ranged.CanUseFromPosition(boss, boss.GridPosition, player, grid, 8), Is.False);
        Assert.That(melee.CanUseFromPosition(boss, new Vector2Int(1, 2), player, grid, 8), Is.False);
        Assert.That(ranged.CanUseFromPosition(boss, new Vector2Int(1, 2), player, grid, 8), Is.True);
        Assert.That(ranged.CanUseFromPosition(boss, new Vector2Int(1, 2), player, grid, 4), Is.False);
    }
    private Character Actor(bool isPlayer, Vector2Int origin, int size = 1)
    {
        var go = Child("Actor"); var actor = go.AddComponent<Character>(); actor.enabled = false;
        Set(actor, "isPlayer", isPlayer); Set(actor, "_grid", grid); Set(actor, "startGridPosition", origin);
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size); Set(actor, "baseStats", new CharacterStatsData());
        var movement = go.AddComponent<CharacterMovement>(); Set(movement, "_grid", grid); Set(movement, "_character", actor);
        typeof(Character).GetProperty(nameof(Character.Movement)).SetValue(actor, movement); go.SetActive(true);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(5, 2, isPlayer ? 100 : 64, 8, 8));
        actor.Stats.SetCurrentEnergy(8); Move(actor, origin); return actor;
    }
    private void Move(Character actor, Vector2Int origin)
    {
        grid.UnregisterCharacter(actor); actor.SetGridPosition(origin); grid.RegisterCharacter(actor);
        actor.transform.position = grid.GridToWorld(origin, actor) + actor.VisualOffset;
    }
    private void AssertOccupancy(Character actor) { foreach (Vector2Int cell in actor.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(actor)); }
    private void Drain(IEnumerator routine, Character pushed = null, Vector2Int destination = default)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine); int frames = 0;
        while (stack.Count > 0)
        {
            Assert.That(++frames, Is.LessThan(1000));
            IEnumerator top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator child) { stack.Push(child); continue; }
            if (pushed != null && top.Current == null) pushed.transform.position = grid.GridToWorld(destination, pushed) + pushed.VisualOffset;
        }
    }
    private GameObject Child(string name) { var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(root.transform, false); return go; }
}
