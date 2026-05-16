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
        List<Vector2Int> path = pathfinding.FindPath(
            character.GridPosition,
            player.GridPosition
        );

        if (path == null || path.Count <= 1)
            yield break;

        path.RemoveAt(0);

        List<Vector2Int> movement = new();

        int energySpent = 0;
        int movementCost = character.GetMovementCostPerCell();

        foreach (var step in path)
        {
            // no pisar al player ni objetos bloqueados
            if (grid.IsBlockedForMovement(step))
                break;

            int energyAfterMove = character.Stats.CurrentEnergy - ((energySpent + 1) * movementCost);

            // no caminar si después no queda energía para ninguna skill
            if (!CanUseAnySkillFromPosition(step, player, energyAfterMove))
            {
                // si todavía no estamos en rango, permitimos caminar un poco
                // pero dejando al menos 2 de energía para mordida
                if (energyAfterMove < 2)
                    break;
            }

            movement.Add(step);
            energySpent++;

            // si desde esta nueva posición ya puede atacar, se detiene
            if (CanUseAnySkillFromPosition(step, player, energyAfterMove))
                break;
        }

        if (movement.Count == 0)
            yield break;

        yield return character.Movement.MoveAlongRoutine(movement);

        character.Stats.ConsumeEnergy(movement.Count * movementCost);
    }

    private bool CanUseAnySkillFromPosition(
    Vector2Int simulatedPosition,
    Character target,
    int availableEnergy)
    {
        foreach (var skill in character.GetActiveSkills())
        {
            if (availableEnergy < skill.EnergyCost)
                continue;

            if (character.IsSkillOnCooldown(skill))
                continue;

            if (!skill.IsInRange(simulatedPosition, target.GridPosition))
                continue;

            return true;
        }

        return false;
    }
}
