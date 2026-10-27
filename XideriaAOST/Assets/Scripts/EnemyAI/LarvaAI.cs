using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LarvaAI : MonoBehaviour, IEnemyAI
{
    private Character character;
    private GridSystem grid;
    private AStarPathfinding pathfinding;

    private void Awake()
    {
        character = GetComponent<Character>();

        grid = FindObjectOfType<GridSystem>();

        pathfinding = new AStarPathfinding(grid);
    }

    public IEnumerator TakeTurn()
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

    private List<Vector2Int> FindBestPathTowardsPlayer(
    Character player)
    {
        List<Vector2Int> attackCandidates = new();
        List<Vector2Int> approachCandidates = new();

        int searchRadius = 6;

        // PASO 1:
        // Buscar posiciones candidatas.
        // Esto es barato porque todavía NO ejecutamos A*.
        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            for (int y = -searchRadius; y <= searchRadius; y++)
            {
                Vector2Int candidate =
                    player.GridPosition +
                    new Vector2Int(x, y);

                if (!IsValidAIStep(candidate))
                    continue;

                int distance =
                    GetDistanceToTarget(
                        character,
                        candidate,
                        player
                    );

                if (CanUseAnySkillFromPosition(
                        candidate,
                        player,
                        character.Stats.CurrentEnergy))
                {
                    attackCandidates.Add(candidate);
                }
                else
                {
                    approachCandidates.Add(candidate);
                }
            }
        }

        // PASO 2:
        // Primero intentamos llegar a una posición
        // desde la que podamos atacar.
        attackCandidates.Sort(
            (a, b) =>
            {
                int distanceA =
                    GetDistanceToTarget(
                        character,
                        a,
                        player
                    );

                int distanceB =
                    GetDistanceToTarget(
                        character,
                        b,
                        player
                    );

                return distanceA.CompareTo(distanceB);
            }
        );

        foreach (Vector2Int candidate in attackCandidates)
        {
            List<Vector2Int> path =
                pathfinding.FindPath(
                    character.GridPosition,
                    candidate,
                    character,
                    avoidUnsafeCells: true
                );

            if (path != null && path.Count > 1)
                return path;
        }

        // PASO 3:
        // Si no existe una posición de ataque alcanzable,
        // buscamos las posiciones más cercanas al jugador.
        approachCandidates.Sort(
            (a, b) =>
            {
                int distanceA =
                    GetDistanceToTarget(
                        character,
                        a,
                        player
                    );

                int distanceB =
                    GetDistanceToTarget(
                        character,
                        b,
                        player
                    );

                return distanceA.CompareTo(distanceB);
            }
        );

        foreach (Vector2Int candidate in approachCandidates)
        {
            List<Vector2Int> path =
                pathfinding.FindPath(
                    character.GridPosition,
                    candidate,
                    character,
                    avoidUnsafeCells: true
                );

            if (path != null && path.Count > 1)
                return path;
        }

        return null;
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
