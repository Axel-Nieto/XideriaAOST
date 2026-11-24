using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LarvaAI : MonoBehaviour, IEnemyAI
{
    protected Character character;
    protected GridSystem grid;
    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    protected virtual void Awake()
    {
        character = GetComponent<Character>();

        grid = FindObjectOfType<GridSystem>();
    }

    public virtual IEnumerator TakeTurn()
    {
        if (!character.Stats.IsAlive || character.IsDying)
            yield break;

        yield return new WaitForSeconds(0.5f);

        Character player = FindPlayer();

        if (player == null)
        {
            character.FinishTurn();
            yield break;
        }

        // 1. Intentar atacar antes de moverse
        if (TryUseAnySkill(player, out SkillBase skillBeforeMove))
        {
            yield return skillBeforeMove.Execute(character, player.GridPosition, grid);
            character.FinishTurn();
            yield break;
        }

        // 2. Si no puede atacar, moverse
        yield return MoveTowardsPlayer(player);

        if (!character.Stats.IsAlive || character.IsDying ||
            !character.gameObject.activeInHierarchy)
            yield break;

        // 3. Intentar atacar después de moverse
        if (TryUseAnySkill(player, out SkillBase skillAfterMove))
        {
            yield return skillAfterMove.Execute(character, player.GridPosition, grid);
        }

        character.FinishTurn();
    }

    private bool TryUseAnySkill(Character target, out SkillBase selectedSkill)
    {
        selectedSkill = null;

        foreach (var skill in character.GetActiveSkills())
        {
            if (skill.CanUse(character, target.GridPosition, grid))
            {
                selectedSkill = skill;
                return true;
            }
        }

        return false;
    }

    private Character FindPlayer()
    {
        Character[] allCharacters = FindObjectsOfType<Character>();

        foreach (var c in allCharacters)
        {
            if (c.IsPlayer)
                return c;
        }

        return null;
    }

    private IEnumerator MoveTowardsPlayer(Character player)
    {
        if (!character.Stats.IsAlive || character.IsDying ||
            !character.gameObject.activeInHierarchy ||
            character.HasStatusEffect<RootEffect>())
            yield break;

        List<Vector2Int> path =
            FindBestPathTowardsPlayer(player);

        if (path == null || path.Count <= 1)
            yield break;

        // A* incluye la posición inicial.
        path.RemoveAt(0);

        List<Vector2Int> movement = new();

        int energySpent = 0;
        int movementCost =
            character.GetMovementCostPerCell();

        foreach (var step in path)
        {
            if (!IsValidAIStep(step))
                break;

            int energyAfterMove =
                character.Stats.CurrentEnergy -
                ((energySpent + 1) * movementCost);

            if (energyAfterMove < 0)
                break;

            movement.Add(step);
            energySpent++;

            // Si desde esta posición ya puede usar
            // alguna skill contra el jugador, deja
            // de acercarse.
            if (CanUseAnySkillFromPosition(
                step,
                player,
                energyAfterMove))
            {
                break;
            }
        }

        if (movement.Count == 0)
            yield break;

        yield return character.Movement.MoveAlongRoutine(
            movement,
            () => character.Stats.ConsumeEnergy(movementCost),
            () => character.Stats.HasEnergy(movementCost)
        );
    }

    private List<Vector2Int> FindBestPathTowardsPlayer(Character player)
    {
        Vector2Int start = character.GridPosition;
        var frontier = new Queue<Vector2Int>();
        var steps = new Dictionary<Vector2Int, int> { [start] = 0 };
        var parents = new Dictionary<Vector2Int, Vector2Int>();
        Vector2Int? futureAttack = null;
        Vector2Int approach = start;
        int approachDistance = GetDistanceToTarget(character, start, player);
        int movementCost = character.GetMovementCostPerCell();
        frontier.Enqueue(start);

        // AI movement charges the same energy per committed cell. Search outward
        // once, so the first affordable attack needs the fewest actual steps.
        // Every origin is checked against the entire body, including safe terrain.
        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            int remainingEnergy = character.Stats.CurrentEnergy - steps[current] * movementCost;
            if (remainingEnergy >= 0 && CanUseAnySkillFromPosition(current, player, remainingEnergy))
                return BuildPath(current, parents);

            // If no attack is affordable this turn, approach the nearest reachable
            // firing position. Staying there saves energy for a subsequent turn.
            if (!futureAttack.HasValue &&
                CanUseAnySkillFromPosition(current, player, character.Stats.MaxEnergy))
                futureAttack = current;

            int distance = GetDistanceToTarget(character, current, player);
            if (distance < approachDistance)
            {
                approachDistance = distance;
                approach = current;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (steps.ContainsKey(next) || !IsValidAIStep(next))
                    continue;
                steps[next] = steps[current] + 1;
                parents[next] = current;
                frontier.Enqueue(next);
            }
        }

        // The start participates too: do not circle an already optimal position
        // when the target is surrounded, unreachable, or skills are on cooldown.
        return BuildPath(futureAttack ?? approach, parents);
    }

    private static List<Vector2Int> BuildPath(Vector2Int destination,
        Dictionary<Vector2Int, Vector2Int> parents)
    {
        var path = new List<Vector2Int> { destination };
        while (parents.TryGetValue(destination, out Vector2Int previous))
        {
            destination = previous;
            path.Add(destination);
        }
        path.Reverse();
        return path;
    }

    private int GetDistanceToTarget(
    Character mover,
    Vector2Int moverPosition,
    Character target)
    {
        int bestDistance = int.MaxValue;

        foreach (Vector2Int moverCell
                 in mover.GetOccupiedCells(moverPosition))
        {
            foreach (Vector2Int targetCell
                     in target.GetOccupiedCells())
            {
                int distance =
                    Mathf.Abs(moverCell.x - targetCell.x) +
                    Mathf.Abs(moverCell.y - targetCell.y);

                if (distance < bestDistance)
                    bestDistance = distance;
            }
        }

        return bestDistance;
    }

    private bool CanUseAnySkillFromPosition(
    Vector2Int simulatedPosition,
    Character target,
    int availableEnergy)
    {
        foreach (var skill in character.GetActiveSkills())
        {
            if (skill.CanUseFromPosition(
                character,
                simulatedPosition,
                target,
                grid,
                availableEnergy))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidAIStep(Vector2Int pos)
    {
        if (!grid.CanCharacterOccupy(
            character,
            pos,
            ignoreSelf: true))
        {
            return false;
        }

        foreach (Vector2Int occupiedCell
                 in character.GetOccupiedCells(pos))
        {
            if (!grid.IsSafeForAI(occupiedCell))
                return false;
        }

        return true;
    }
}
