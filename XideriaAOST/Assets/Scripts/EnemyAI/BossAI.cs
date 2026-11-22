using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossAI : LarvaAI
{
    [SerializeField] private GameObject phaseTwoImpactPrefab;
    [SerializeField] private GameObject temporaryRockPrefab;
    [SerializeField] private int phaseTwoMeleeDamage = 3;
    [SerializeField] private int phaseTwoRangedDamage = 2;
    public bool IsPhaseTwo { get; private set; }
    internal Character Owner => character;
    public bool IsTransforming => transformationPending;
    private bool transformationPending;
    private CharacterAnimationController animation;
    private static readonly Vector2Int[] adjacent = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    protected override void Awake()
    {
        base.Awake();
        animation = GetComponentInChildren<CharacterAnimationController>();
    }

    private void OnEnable()
    {
        if (character == null) character = GetComponent<Character>();
        character.Damaged -= CheckPhase;
        character.Damaged += CheckPhase;
    }

    private void OnDisable()
    {
        if (character != null) character.Damaged -= CheckPhase;
    }

    public void CheckPhase(Character damaged)
    {
        if (IsPhaseTwo || damaged != character || character.Stats == null || !character.Stats.IsAlive || character.IsDying ||
            (long)character.Stats.CurrentHP * 2 > character.Stats.MaxHP) return;
        IsPhaseTwo = true;
        transformationPending = true;
        if (animation == null) animation = GetComponentInChildren<CharacterAnimationController>();
        animation?.PrepareBossPhaseTwo();
        character.ReplaceSkills(new SkillBase[] { new BossPhaseTwoSkill(this, false), new BossPhaseTwoSkill(this, true) });
        foreach (TurnManager manager in FindObjectsOfType<TurnManager>())
            if (manager.gameObject.scene == gameObject.scene) { manager.RequestImmediateTurn(character); break; }
    }

    public override IEnumerator TakeTurn()
    {
        CheckPhase(character);
        if (!IsPhaseTwo) { yield return base.TakeTurn(); yield break; }
        if (!Alive(character)) yield break;
        if (transformationPending)
        {
            try
            {
                if (animation != null)
                {
                    yield return animation.PlayBossTransformation();
                    animation.FinishBossAction();
                }
                if (!Alive(character)) yield break;
                if (TryFindCenterPath(out List<Vector2Int> path) && path.Count > 0 && !character.HasStatusEffect<RootEffect>())
                {
                    animation?.SetBossCenterMovement(true);
                    yield return character.Movement.MoveAlongRoutine(path);
                }
                transformationPending = false;
                character.Stats.SetCurrentEnergy(character.Stats.MaxEnergy);
            }
            finally
            {
                animation?.FinishBossAction();
                animation?.SetBossCenterMovement(false);
            }
        }
        Character player = null;
        foreach (Character candidate in FindObjectsOfType<Character>())
            if (candidate.IsPlayer && candidate.gameObject.scene == gameObject.scene && Alive(candidate)) { player = candidate; break; }
        if (player != null && Alive(character))
        {
            bool ranged = !AreAdjacent(character, player);
            var skill = new BossPhaseTwoSkill(this, ranged);
            if (skill.CanUse(character, player.GridPosition, grid)) yield return skill.Execute(character, player.GridPosition, grid);
        }
        character.FinishTurn();
    }

    public bool TryFindCenterPath(out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();
        if (grid == null || character == null || character.Movement == null) return false;
        Vector2 center = new((grid.Width - character.GridWidth) * 0.5f, (grid.Height - character.GridHeight) * 0.5f);
        Vector2Int start = character.GridPosition, best = start;
        float bestDistance = ((Vector2)start - center).sqrMagnitude;
        var frontier = new Queue<Vector2Int>();
        var parents = new Dictionary<Vector2Int, Vector2Int>();
        var seen = new HashSet<Vector2Int> { start };
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            float distance = ((Vector2)current - center).sqrMagnitude;
            if (distance < bestDistance) { best = current; bestDistance = distance; }
            foreach (Vector2Int direction in adjacent)
            {
                Vector2Int next = current + direction;
                if (seen.Contains(next) || !grid.CanCharacterOccupy(character, next, true)) continue;
                bool safe = true;
                foreach (Vector2Int cell in character.GetOccupiedCells(next)) if (!grid.IsSafeForAI(cell)) { safe = false; break; }
                if (!safe) continue;
                seen.Add(next); parents[next] = current; frontier.Enqueue(next);
            }
        }
        while (best != start) { path.Add(best); best = parents[best]; }
        path.Reverse();
        return true;
    }

    public static bool AreAdjacent(Character first, Character second)
        => first != null && AreAdjacentAt(first, first.GridPosition, second);

    internal static bool AreAdjacentAt(Character first, Vector2Int origin, Character second)
    {
        if (first == null || second == null) return false;
        foreach (Vector2Int a in first.GetOccupiedCells(origin))
            foreach (Vector2Int b in second.GetOccupiedCells())
                if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1) return true;
        return false;
    }

    internal IEnumerator ExecutePhaseTwoAttack(Character target, bool ranged)
    {
        character.FaceCell(target.GridPosition);
        Vector2Int impactOrigin = target.GridPosition;
        List<Vector2Int> impactCells = target.GetOccupiedCells();
        try
        {
            if (animation != null)
            {
                yield return animation.PlayBossAttackUntilImpact(ranged, ranged ? (System.Action)(() =>
                    SkillVFXSpawner.SpawnAtWorld(phaseTwoImpactPrefab, grid.GridToWorld(impactOrigin, target), Quaternion.identity, 3.5f)) : null);
                if (!animation.BossImpactReached) yield break;
            }
            if (!Alive(character) || !Alive(target)) yield break;
            target.TakeDamage(Mathf.Max(1, character.Stats.Attack + (ranged ? phaseTwoRangedDamage : phaseTwoMeleeDamage) - target.Stats.Defense),
                DamageType.Physical, character);
            if (Alive(target))
            {
                if (!ranged) yield return PushSystem.Push(character, target, grid, 2);
                else
                {
                    var directions = new List<Vector2Int>();
                    foreach (Vector2Int direction in adjacent)
                        if (grid.CanCharacterOccupy(target, target.GridPosition + direction, true)) directions.Add(direction);
                    if (directions.Count > 0 && !target.BlocksPush())
                        yield return PushSystem.PushInDirection(target, directions[Random.Range(0, directions.Count)], 1, grid);
                }
            }
            if (ranged && temporaryRockPrefab != null)
            {
                TemporaryRockField field = TemporaryRockField.For(grid);
                RockSkillVFX presentation = target.GetComponent<RockSkillVFX>();
                foreach (Vector2Int cell in impactCells)
                    if (field.TryPlace(character, cell, temporaryRockPrefab, 2, presentation)) break;
            }
            if (animation != null) yield return animation.WaitBossActionFinished();
        }
        finally { animation?.FinishBossAction(); }
    }

    internal int AttackBonus(bool ranged) => ranged ? phaseTwoRangedDamage : phaseTwoMeleeDamage;
    private static bool Alive(Character actor) => actor != null && actor.Stats != null && actor.Stats.IsAlive && !actor.IsDying;
}
