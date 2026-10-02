using System.Collections;
using UnityEngine;

public static class PushSystem
{
    private const float DefaultPushSpeed = 10f;

    public static IEnumerator Push(
    Character source,
    Character target,
    GridSystem grid,
    int distance,
    float pushSpeed = DefaultPushSpeed)
    {
        if (source == null ||
            target == null ||
            grid == null ||
            distance <= 0)
        {
            yield break;
        }

        Vector2Int direction = GetPushDirection(
            source,
            target
        );

        if (direction == Vector2Int.zero)
            yield break;

        // El objetivo bloquea el desplazamiento.
        if (target.BlocksPush())
        {
            Vector2Int reflectedDirection = -direction;

            yield return PushInDirection(
                source,
                reflectedDirection,
                distance,
                grid,
                pushSpeed
            );

            yield break;
        }

        yield return PushInDirection(
            target,
            direction,
            distance,
            grid,
            pushSpeed
        );
    }

    public static IEnumerator PushInDirection(
        Character target,
        Vector2Int direction,
        int distance,
        GridSystem grid,
        float pushSpeed = DefaultPushSpeed)
    {
        if (target == null ||
            grid == null ||
            distance <= 0)
        {
            yield break;
        }

        direction = NormalizeCardinal(direction);

        if (direction == Vector2Int.zero)
            yield break;

        Vector2Int origin = target.GridPosition;
        Vector2Int destination = origin;

        // Buscamos hasta dónde puede desplazarse realmente.
        // Si pedimos 3 celdas pero la tercera está bloqueada,
        // se desplazará solamente 2.
        for (int i = 1; i <= distance; i++)
        {
            Vector2Int candidate =
                origin + direction * i;

            if (!grid.CanCharacterOccupy(
                    target,
                    candidate,
                    ignoreSelf: true))
            {
                break;
            }

            destination = candidate;
        }

        // No pudo desplazarse ni una celda.
        if (destination == origin)
            yield break;

        grid.UnregisterCharacter(target);

        Vector3 endPosition =
            grid.GridToWorld(destination, target)
            + target.VisualOffset;

        while (Vector3.Distance(
                   target.transform.position,
                   endPosition) > 0.05f)
        {
            target.transform.position =
                Vector3.MoveTowards(
                    target.transform.position,
                    endPosition,
                    pushSpeed * Time.deltaTime
                );

            yield return null;
        }

        target.transform.position = endPosition;

        target.SetGridPosition(destination);

        grid.RegisterCharacter(target);

        target.ProcessCurrentTerrain();
    }

    public static Vector2Int GetPushDirection(
        Character source,
        Character target)
    {
        if (source == null || target == null)
            return Vector2Int.zero;

        Vector2Int closestSourceCell =
            source.GridPosition;

        Vector2Int closestTargetCell =
            target.GridPosition;

        int bestDistance = int.MaxValue;

        foreach (Vector2Int sourceCell
                 in source.GetOccupiedCells())
        {
            foreach (Vector2Int targetCell
                     in target.GetOccupiedCells())
            {
                int distance =
                    Mathf.Abs(
                        targetCell.x - sourceCell.x)
                    +
                    Mathf.Abs(
                        targetCell.y - sourceCell.y);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closestSourceCell = sourceCell;
                    closestTargetCell = targetCell;
                }
            }
        }

        Vector2Int delta =
            closestTargetCell - closestSourceCell;

        return NormalizeCardinal(delta);
    }

    private static Vector2Int NormalizeCardinal(
        Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(direction.x) >
            Mathf.Abs(direction.y))
        {
            return new Vector2Int(
                direction.x > 0 ? 1 : -1,
                0
            );
        }

        return new Vector2Int(
            0,
            direction.y > 0 ? 1 : -1
        );
    }
}
