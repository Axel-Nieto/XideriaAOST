using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class ElementalLarvaSkillTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character larva;
    private Character player;
    private SkillVFXLibrary previousEffects;

    [SetUp]
    public void CreateCombat()
    {
        previousEffects = SkillVFXLibrary.Instance;
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, null);
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 16); Set(grid, "height", 16); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        larva = CreateCharacter(false, new Vector2Int(2, 2));
        player = CreateCharacter(true, new Vector2Int(6, 2));
    }

    [TearDown]
    public void RestoreEffects() =>
        typeof(SkillVFXLibrary).GetProperty(nameof(SkillVFXLibrary.Instance)).SetValue(null, previousEffects);

    [TestCase(LarvaElement.Air, 4, 6, 1)]
    [TestCase(LarvaElement.Air, 4, 6, 2)]
    [TestCase(LarvaElement.Earth, 1, 2, 1)]
    [TestCase(LarvaElement.Earth, 1, 2, 2)]
    [TestCase(LarvaElement.Fire, 3, 4, 1)]
    [TestCase(LarvaElement.Fire, 3, 4, 2)]
    public void ActualAndSimulatedRangesUseTheWholeFootprint(LarvaElement element, int min, int max, int size)
    {
        grid.UnregisterCharacter(larva);
        Set(larva, "gridWidth", size); Set(larva, "gridHeight", size);
        grid.RegisterCharacter(larva);
        SkillBase skill = Skill(element);
        foreach (int distance in new[] { min - 1, min, max, max + 1 })
        {
            Move(player, new Vector2Int(2 + size - 1 + distance, 2));
            bool expected = distance >= min && distance <= max;
            Assert.That(skill.CanUse(larva, player.GridPosition, grid), Is.EqualTo(expected));
            Assert.That(skill.CanUseFromPosition(larva, larva.GridPosition, player, grid, 10), Is.EqualTo(expected));
        }
    }

    [Test]
    public void PullSimulationUsesSimulatedAlignmentAndEnergy()
    {
        Move(player, new Vector2Int(7, 3));
        larva.Stats.SetCurrentEnergy(0);
        var skill = new LarvaPullSkill();
        Assert.That(skill.CanUse(larva, player.GridPosition, grid), Is.False);
        Assert.That(skill.CanUseFromPosition(larva, new Vector2Int(2, 2), player, grid, 3), Is.False);
        Assert.That(skill.CanUseFromPosition(larva, new Vector2Int(2, 3), player, grid, 3), Is.True);
        Assert.That(skill.CanUseFromPosition(larva, new Vector2Int(2, 3), player, grid, 2), Is.False);
        larva.StartCooldown(skill);
        Assert.That(skill.CanUseFromPosition(larva, new Vector2Int(2, 3), player, grid, 3), Is.False);
    }

    [Test]
    public void LargeLarvaCanPullAlongANonOriginRow()
    {
        grid.UnregisterCharacter(larva);
        Set(larva, "gridWidth", 2); Set(larva, "gridHeight", 2);
        grid.RegisterCharacter(larva);
        Move(player, new Vector2Int(7, 3));
        var skill = new LarvaPullSkill();
        Assert.That(skill.CanUse(larva, player.GridPosition, grid), Is.True);
        Execute(skill, new Vector2Int(6, 3));
        Assert.That(player.GridPosition, Is.EqualTo(new Vector2Int(6, 3)));
        Assert.That(grid.GetCharacterAt(new Vector2Int(7, 3)), Is.Null);
    }

    [TestCase(LarvaElement.Air)]
    [TestCase(LarvaElement.Earth)]
    public void DisplacementMovesOneCellAndSpendsOnlyTheAttackersEnergy(LarvaElement element)
    {
        Move(player, new Vector2Int(element == LarvaElement.Air ? 6 : 4, 2));
        Vector2Int destination = player.GridPosition + (element == LarvaElement.Air ? Vector2Int.left : Vector2Int.right);
        SkillBase skill = Skill(element);
        Execute(skill, destination);
        Assert.That(player.GridPosition, Is.EqualTo(destination));
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(98));
        Assert.That(player.Stats.CurrentEnergy, Is.EqualTo(10));
        Assert.That(larva.Stats.CurrentEnergy, Is.EqualTo(7));
        Assert.That(larva.GetSkillCooldown(skill), Is.EqualTo(3));
        Assert.That(grid.GetCharacterAt(destination), Is.SameAs(player));
        Move(player, new Vector2Int(element == LarvaElement.Air ? 6 : 4, 2));
        Execute(skill, player.GridPosition);
        Assert.That(larva.Stats.CurrentEnergy, Is.EqualTo(7), "A cooldown attempt cannot spend again.");
    }

    [TestCase(LarvaElement.Air)]
    [TestCase(LarvaElement.Earth)]
    public void BlockedDestinationChecksEveryCellOfALargeTarget(LarvaElement element)
    {
        grid.UnregisterCharacter(player);
        Set(player, "gridWidth", 2); Set(player, "gridHeight", 2);
        Move(player, new Vector2Int(element == LarvaElement.Air ? 6 : 4, 2));
        Vector2Int origin = player.GridPosition;
        grid.SetCellType(new Vector2Int(element == LarvaElement.Air ? 5 : 6, 3), CellType.Blocked);
        Execute(Skill(element), origin);
        Assert.That(player.GridPosition, Is.EqualTo(origin));
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(98));
        foreach (Vector2Int cell in player.GetOccupiedCells())
            Assert.That(grid.GetCharacterAt(cell), Is.SameAs(player));
    }

    [Test]
    public void PullRespectsDisplacementImmunityAndChainUsesExistingReflection()
    {
        player.AddPassive(new ColossusPassive());
        Vector2Int original = player.GridPosition;
        Execute(new LarvaPullSkill(), original);
        Assert.That(player.GridPosition, Is.EqualTo(original));
        Move(player, new Vector2Int(4, 2));
        Execute(new LarvaChainSkill(), player.GridPosition, new Vector2Int(1, 2));
        Assert.That(player.GridPosition, Is.EqualTo(new Vector2Int(4, 2)));
        Assert.That(larva.GridPosition, Is.EqualTo(new Vector2Int(1, 2)));
    }

    [Test]
    public void FireDamagesOnlyItsTargetAndAppliesTheExistingBurnEffect()
    {
        Character neighbor = CreateCharacter(true, new Vector2Int(6, 3));
        Execute(new LarvaFireSpitSkill(), player.GridPosition);
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(95));
        Assert.That(neighbor.Stats.CurrentHP, Is.EqualTo(100));
        Assert.That(player.GetStatusEffects(), Has.Count.EqualTo(1));
        var burn = player.GetStatusEffects()[0] as BurnEffect;
        Assert.That(burn, Is.Not.Null);
        Assert.That(burn.RemainingTurns, Is.EqualTo(3));
        for (int turn = 0; turn < 3; turn++) burn.OnTurnStart(player);
        Assert.That(burn.IsFinished, Is.True);
        Assert.That(player.Stats.CurrentHP, Is.EqualTo(89));
    }

    [TestCase(LarvaElement.Air)]
    [TestCase(LarvaElement.Earth)]
    [TestCase(LarvaElement.Fire)]
    public void RejectedTargetsDoNotConsumeEnergyOrStartCooldown(LarvaElement element)
    {
        SkillBase skill = Skill(element);
        Move(player, new Vector2Int(2 + skill.MinRange, 2));
        Set(player, "isPlayer", false);
        Execute(skill, player.GridPosition);
        Assert.That(larva.Stats.CurrentEnergy, Is.EqualTo(10));
        Assert.That(larva.GetSkillCooldown(skill), Is.Zero);
        Set(player, "isPlayer", true);
        larva.Stats.SetCurrentEnergy(2);
        Execute(skill, player.GridPosition);
        Assert.That(larva.Stats.CurrentEnergy, Is.EqualTo(2));
        Assert.That(larva.GetSkillCooldown(skill), Is.Zero);
    }

    [TestCase(LarvaElement.Air)]
    [TestCase(LarvaElement.Earth)]
    [TestCase(LarvaElement.Fire)]
    public void SetupPrefersTheSpecialAndKeepsThePoisonLarvasBite(LarvaElement element)
    {
        ElementalLarvaSetup setup = larva.gameObject.AddComponent<ElementalLarvaSetup>();
        Set(setup, "element", element);
        Call(setup, "Start");
        Assert.That(larva.GetActiveSkills(), Has.Count.EqualTo(2));
        Assert.That(larva.GetSkill(0).GetType(), Is.EqualTo(Skill(element).GetType()));
        Assert.That(larva.GetSkill(1), Is.TypeOf<BiteSkill>());
    }

    private static SkillBase Skill(LarvaElement element) => element switch
    {
        LarvaElement.Air => new LarvaPullSkill(),
        LarvaElement.Earth => new LarvaChainSkill(),
        _ => new LarvaFireSpitSkill()
    };

    private GameObject Child(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(root.transform, false);
        return go;
    }

    private Character CreateCharacter(bool isPlayer, Vector2Int origin)
    {
        Character character = Child(isPlayer ? "Player" : "Larva").AddComponent<Character>();
        Set(character, "isPlayer", isPlayer);
        Set(character, "_grid", grid);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(character, new CharacterStats(4, 2, 100, 10, 5));
        character.Stats.SetCurrentEnergy(10);
        Move(character, origin);
        return character;
    }

    private void Move(Character character, Vector2Int origin)
    {
        grid.UnregisterCharacter(character);
        character.SetGridPosition(origin);
        character.transform.position = grid.GridToWorld(origin, character) + character.VisualOffset;
        grid.RegisterCharacter(character);
    }

    private void Execute(SkillBase skill, Vector2Int playerDestination, Vector2Int? larvaDestination = null)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(skill.Execute(larva, player.GridPosition, grid));
        int steps = 0;
        while (stack.Count > 0)
        {
            Assert.That(++steps, Is.LessThan(256), "Coroutine failed to complete.");
            IEnumerator routine = stack.Peek();
            if (!routine.MoveNext()) { stack.Pop(); continue; }
            if (routine.Current is IEnumerator child) { stack.Push(child); continue; }
            // Advance presentation immediately in EditMode; logical occupancy must
            // still be committed by the real PushSystem coroutine.
            player.transform.position = grid.GridToWorld(playerDestination, player) + player.VisualOffset;
            larva.transform.position = grid.GridToWorld(larvaDestination ?? larva.GridPosition, larva) + larva.VisualOffset;
        }
    }
}

public class ElementalLarvaPrefabTests
{
    [TestCase("Aire")]
    [TestCase("Tierra")]
    [TestCase("Fuego")]
    public void PrefabsHaveCombatComponentsOwnAnimationsAndCompletionEvents(string element)
    {
        string folder = "Assets/Prefabs/P_Enemigos/Zona_1";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/Larva_{element}.prefab");
        Assert.That(prefab, Is.Not.Null);
        Character character = prefab.GetComponent<Character>();
        Assert.That(character, Is.Not.Null);
        Assert.That(character.IsPlayer, Is.False);
        Assert.That(character.GridWidth, Is.EqualTo(1));
        Assert.That(character.GridHeight, Is.EqualTo(1));
        Assert.That(new SerializedObject(character).FindProperty("visualRoot").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.GetComponent<CharacterMovement>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<LarvaAI>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<ElementalLarvaSetup>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<LarvaSkillSetup>(), Is.Null);
        Assert.That(prefab.GetComponent<EnemyLootDropper>(), Is.Not.Null);
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.GetComponent<CharacterAnimationController>(), Is.Not.Null);
        var controller = animator.runtimeAnimatorController as AnimatorController;
        Assert.That(controller, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo($"{folder}/ElementalAnimations/Larva_{element}.controller"));
        var states = new Dictionary<string, AnimatorState>();
        foreach (ChildAnimatorState state in controller.layers[0].stateMachine.states)
            states.Add(state.state.name, state.state);
        Assert.That(states.ContainsKey("Death"), Is.True);
        Assert.That(AssetDatabase.GetAssetPath(states["Death"].motion),
            Is.EqualTo($"Assets/Models/Enemigos/Zona_1/Larva {element}/En_Dead_Larva{element}.anim"));
        foreach (string stateName in new[] { "Attack", "Skill" })
        {
            var clip = states[stateName].motion as AnimationClip;
            Assert.That(clip, Is.Not.Null);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            Assert.That(events.Length, Is.EqualTo(2));
            string prefix = stateName == "Attack" ? "Attack" : "Cast";
            Assert.That(events[0].functionName, Is.EqualTo($"On{prefix}HitFrame"));
            Assert.That(events[1].functionName, Is.EqualTo($"On{prefix}AnimationFinished"));
            Assert.That(events[1].time, Is.LessThan(clip.length));
            Assert.That(states[stateName].transitions[0].exitTime, Is.EqualTo(1f));
        }
    }
}
