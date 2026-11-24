using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BossCombatPresentationTests : UpgradeIntegrationFixture
{
    private GridSystem grid;
    private Character boss;
    private Animator animator;
    private CharacterAnimationController animation;
    private SkinnedMeshRenderer body;
    private CharacterVisualCentering centering;
    private readonly HashSet<string> deliveredEvents = new HashSet<string>();
    private int eventState;

    [SetUp]
    public void CreateBoss()
    {
        grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 20); Set(grid, "height", 20); Set(grid, "cellSize", 1f); Set(grid, "yOffset", 0f);
        Call(grid, "GenerateGrid");
        GameObject staging = Child("Boss staging");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/P_Enemigos/Zona_1/Jefe_Zona1/BossZone1Combat.prefab");
        Assert.That(prefab, Is.Not.Null);
        var instance = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
        boss = instance.GetComponent<Character>();
        boss.enabled = false;
        Set(boss, "_grid", grid);
        CharacterMovement movement = instance.GetComponent<CharacterMovement>();
        Set(movement, "_grid", grid); Set(movement, "_character", boss);
        typeof(Character).GetProperty(nameof(Character.Movement)).SetValue(boss, movement);
        instance.transform.SetParent(root.transform, false);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(boss, new CharacterStats(5, 2, 64, 8, 8));
        boss.Stats.SetCurrentEnergy(8);
        boss.SetGridPosition(new Vector2Int(6, 6));
        instance.transform.position = grid.GridToWorld(boss.GridPosition, boss) + boss.VisualOffset;
        grid.RegisterCharacter(boss);
        animator = instance.GetComponentInChildren<Animator>(true);
        animation = instance.GetComponentInChildren<CharacterAnimationController>(true);
        centering = instance.GetComponent<CharacterVisualCentering>();
        Assert.That(centering, Is.Not.Null);
        Assert.That(animation, Is.Not.Null, "The combat prefab must receive attack animation events.");
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.Update(0f);
        body = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Body");
    }

    [TestCase("Idle")] [TestCase("Walk")] [TestCase("Attack")]
    [TestCase("Charge")] [TestCase("Hit")] [TestCase("Death")]
    [TestCase("Boss_Fase2")] [TestCase("Boss_IdleF2")] [TestCase("Boss_Walkf2")]
    [TestCase("Boss_AttackF2")] [TestCase("Boss_SkillF2")] [TestCase("Boss_HitReactionF2")]
    public void EveryPoseAndHeadingKeepsTheBodyOverTheFourOccupiedCells(string state)
    {
        Vector2Int origin = boss.GridPosition;
        var mesh = new Mesh();
        try
        {
            foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                boss.FaceDirection(direction);
                foreach (float time in new[] { 0f, 0.13f, 0.37f, 0.62f, 0.87f, 0.999f })
                {
                    animator.Rebind();
                    animator.Play("Base Layer." + state, 0, time);
                    animator.Update(0f);
                    centering.Recenter();
                    body.BakeMesh(mesh);
                    Vector3 center = body.transform.TransformPoint(mesh.bounds.center);
                    Vector3 expected = grid.GridToWorld(origin, boss);
                    Assert.That(Vector2.Distance(new Vector2(center.x, center.z), new Vector2(expected.x, expected.z)),
                        Is.LessThan(0.025f), state + " direction=" + direction + " time=" + time + " center=" + center);
                    Assert.That(boss.GridPosition, Is.EqualTo(origin));
                    Assert.That(boss.GetOccupiedCells(), Has.Count.EqualTo(4));
                    foreach (Vector2Int cell in boss.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(boss));
                    Assert.That(Quaternion.Angle(boss.transform.rotation, Quaternion.identity), Is.LessThan(0.001f));
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void WalkingStartsAndStopsWithoutWaitingForAnIdleLoop()
    {
        animation.SetWalking(true);
        animator.Update(0.1f);
        animator.Update(0.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Walk"), Is.True);
        animation.SetWalking(false);
        animator.Update(0.1f);
        animator.Update(0.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
    }

    [Test]
    public void CombatPrefabHasThePhaseTwoAIAndBothEffectReferences()
    {
        BossAI ai = boss.GetComponent<BossAI>();
        Assert.That(ai, Is.Not.Null);
        var serialized = new SerializedObject(ai);
        Assert.That(serialized.FindProperty("phaseTwoImpactPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("temporaryRockPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(boss.GetComponents<IEnemyAI>(), Has.Length.EqualTo(1));
        var stats = new SerializedObject(boss).FindProperty("baseStats");
        Assert.That(stats.FindPropertyRelative("maxHP").intValue, Is.EqualTo(64));
        Assert.That(stats.FindPropertyRelative("attack").intValue, Is.EqualTo(5));
        Assert.That(stats.FindPropertyRelative("defense").intValue, Is.EqualTo(2));
    }

    [TestCase(false)] [TestCase(true)]
    public void PhaseTwoAttacksWaitForImpactThenReturnToTheirOwnIdle(bool ranged)
    {
        animation.PrepareBossPhaseTwo();
        float elapsed = 0f, effectAt = -1f;
        IEnumerator impact = animation.PlayBossAttackUntilImpact(ranged, () => effectAt = elapsed);
        while (impact.MoveNext())
        {
            Assert.That(elapsed, Is.LessThan(1.1f));
            Assert.That(animation.BossImpactReached, Is.False);
            animator.Update(0.01f); elapsed += 0.01f;
        }
        Assert.That(animation.BossImpactReached, Is.True);
        Assert.That(elapsed, Is.InRange(0.54f, 0.57f));
        Assert.That(effectAt, Is.InRange(ranged ? 0.34f : 0.54f, ranged ? 0.37f : 0.57f));
        IEnumerator finish = animation.WaitBossActionFinished();
        while (finish.MoveNext())
        {
            Assert.That(elapsed, Is.LessThan(1.2f));
            animator.Update(0.01f); elapsed += 0.01f;
        }
        Assert.That(elapsed, Is.GreaterThanOrEqualTo(0.99f));
        animation.FinishBossAction();
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Boss_IdleF2"), Is.True);
        animation.SetWalking(true);
        animator.Update(0.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Boss_IdleF2"), Is.True);
        animation.PlayHit(); animator.Update(0.01f); animator.Update(0.05f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Boss_HitReactionF2"), Is.True);
        for (int i = 0; i < 40; i++) animator.Update(0.01f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Boss_IdleF2"), Is.True);
    }

    [Test]
    public void TransformationInterruptsChargeAndCompletesBeforeCenterMovement()
    {
        animator.Play("Base Layer.Charge", 0, 0.4f); animator.Update(0f);
        var routine = new InterruptibleRoutine(animation.PlayBossTransformation());
        float elapsed = 0f;
        while (routine.MoveNext())
        {
            Assert.That(elapsed, Is.LessThan(3.1f));
            animation.PlayHit(); // Damage must not replace the transformation pose.
            animator.Update(0.01f); elapsed += 0.01f;
            if (elapsed < 2.8f)
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Boss_Fase2"), Is.True);
        }
        Assert.That(elapsed, Is.GreaterThan(2.8f));
        animation.FinishBossAction();
        animation.SetBossCenterMovement(true); animation.SetWalking(true);
        animator.Update(0.5f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Boss_Walkf2"), Is.True);
        animation.SetWalking(false); animation.SetBossCenterMovement(false);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Boss_IdleF2"), Is.True);
    }

    [Test]
    public void BlendingBetweenPosesKeepsTheBodyCentered()
    {
        var mesh = new Mesh();
        try
        {
            foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                boss.FaceDirection(direction);
                animator.Rebind(); animator.Play("Base Layer.Idle", 0, 0f); animator.Update(0f);
                foreach (string state in new[] { "Walk", "Attack", "Idle", "Charge", "Idle", "Hit", "Idle", "Death" })
                {
                    animator.CrossFadeInFixedTime("Base Layer." + state, 0.04f, 0, 0f);
                    for (int frame = 0; frame < 40; frame++)
                    {
                        animator.Update(0.01f);
                        centering.Recenter();
                        body.BakeMesh(mesh);
                        Vector3 center = body.transform.TransformPoint(mesh.bounds.center);
                        Vector3 expected = grid.GridToWorld(boss.GridPosition, boss);
                        Assert.That(Vector2.Distance(new Vector2(center.x, center.z), new Vector2(expected.x, expected.z)),
                            Is.LessThan(0.05f), "Transition to " + state + " frame=" + frame);
                    }
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void HornStrikeWaitsForTheConfiguredImpactAndFinishEvents()
    {
        Character target = Target(new Vector2Int(8, 7));
        IEnumerator attack = new HornStrikeSkill(3, 0).Execute(boss, target.GridPosition, grid);
        Assert.That(attack.MoveNext(), Is.True);
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(36));
        AdvanceAnimation((IEnumerator)attack.Current);
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(36));
        Assert.That(attack.MoveNext(), Is.True);
        Assert.That(target.Stats.CurrentHP, Is.EqualTo(30));
        Drain(attack, false);
        Assert.That(boss.Stats.CurrentEnergy, Is.EqualTo(5));
        animator.Update(0.1f); animator.Update(0.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void ChargeUsesItsOwnAnimationAndRestoresItOnCompletionOrCancellation(bool cancel)
    {
        Character target = Target(new Vector2Int(12, 7));
        IEnumerator action = new ChargeSkill(5, 0, 0).Execute(boss, target.GridPosition, grid);
        Assert.That(action.MoveNext(), Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Charge"), Is.True);
        animation.SetWalking(true);
        animator.Update(0.01f);
        centering.Recenter();
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Charge"), Is.True);
        if (cancel) ((IDisposable)action).Dispose();
        else
        {
            Drain((IEnumerator)action.Current, true);
            Drain(action, false);
            Assert.That(boss.GridPosition, Is.EqualTo(new Vector2Int(10, 6)));
            Assert.That(target.Stats.CurrentHP, Is.EqualTo(28));
        }
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
        Assert.That(animator.GetFloat("ChargeSpeed"), Is.EqualTo(1f));
        Assert.That(boss.Stats.CurrentEnergy, Is.EqualTo(3));
        foreach (Vector2Int cell in boss.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(boss));
    }

    [TestCase(false)] [TestCase(true)]
    public void RootOrInsufficientEnergyRejectsChargeBeforeItStarts(bool rooted)
    {
        Character target = Target(new Vector2Int(12, 7));
        if (rooted)
            ((List<StatusEffect>)typeof(Character).GetField("statusEffects", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(boss)).Add(new RootEffect(1));
        else boss.Stats.SetCurrentEnergy(4);
        int energy = boss.Stats.CurrentEnergy;
        var skill = new ChargeSkill();
        Assert.That(skill.CanUse(boss, target.GridPosition, grid), Is.False);
        Assert.That(skill.CanUseFromPosition(boss, boss.GridPosition, target, grid, energy), Is.False);
        Assert.That(skill.Execute(boss, target.GridPosition, grid).MoveNext(), Is.False);
        Assert.That(boss.Stats.CurrentEnergy, Is.EqualTo(energy));
        Assert.That(boss.GridPosition, Is.EqualTo(new Vector2Int(6, 6)));
        Assert.That(boss.GetSkillCooldown(skill), Is.Zero);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
    }

    [Test]
    public void DeathCompletesBeforeRemovingTheFullFootprint()
    {
        bool completed = false;
        IEnumerator dying = CharacterAnimationController.PlayDeathAndWait(animator, () => completed = true);
        Assert.That(dying.MoveNext(), Is.True);
        Assert.That(completed, Is.False);
        AdvanceAnimation(dying);
        Assert.That(completed, Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).loop, Is.False);
        foreach (Vector2Int cell in boss.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(boss));
    }

    private void AdvanceAnimation(IEnumerator routine)
    {
        int frames = 0;
        while (routine.MoveNext())
        {
            Assert.That(++frames, Is.LessThan(300), "Boss animation never signaled its completion. state=" +
                animator.GetCurrentAnimatorStateInfo(0).fullPathHash + " time=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime +
                " attack=" + animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Attack") + " fireEvents=" + animator.fireEvents);
            TickAnimation();
        }
    }

    private void Drain(IEnumerator routine, bool moving)
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
            TickAnimation();
            if (moving) boss.transform.position = grid.GridToWorld(boss.GridPosition + Vector2Int.right, boss) + boss.VisualOffset;
        }
    }

    private void TickAnimation()
    {
        animator.Update(0.01f);
        if (Application.isPlaying) return;
        // EditMode evaluates poses but does not send AnimationEvents. Deliver the
        // actual clip's configured messages to its real receiver as time crosses them.
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != eventState)
        {
            eventState = state.fullPathHash;
            deliveredEvents.Clear();
        }
        foreach (AnimatorClipInfo clip in animator.GetCurrentAnimatorClipInfo(0))
            foreach (AnimationEvent message in AnimationUtility.GetAnimationEvents(clip.clip))
                if (message.time <= state.normalizedTime * clip.clip.length && deliveredEvents.Add(message.functionName))
                {
                    // SendMessage itself asserts for non-ExecuteAlways behaviours in EditMode.
                    var receiver = animator.GetComponent<CharacterAnimationController>();
                    Assert.That(receiver, Is.SameAs(animation));
                    var callback = receiver.GetType().GetMethod(message.functionName);
                    Assert.That(callback, Is.Not.Null);
                    callback.Invoke(receiver, null);
                }
    }

    private GameObject Child(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false); go.transform.SetParent(root.transform, false);
        return go;
    }

    private Character Target(Vector2Int position)
    {
        Character target = Child("Tete").AddComponent<Character>();
        Set(target, "isPlayer", true); Set(target, "_grid", grid);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(target, new CharacterStats(6, 2, 36, 13, 7));
        target.SetGridPosition(position);
        target.transform.position = grid.GridToWorld(position, target) + target.VisualOffset;
        grid.RegisterCharacter(target);
        return target;
    }
}
