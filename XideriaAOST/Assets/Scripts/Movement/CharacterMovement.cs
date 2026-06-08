using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class CharacterMovement : MonoBehaviour
{
    private Character _character;

    public float moveSpeed = 5f;

    private GridSystem _grid;

    private void Awake()
    {
        if (_grid == null)
        {
            _grid = FindObjectOfType<GridSystem>();
        }
        _character = GetComponent<Character>();
    }

    public IEnumerator MoveAlongRoutine(List<Vector2Int> path)
    {
        if (_character.HasStatusEffect<RootEffect>())
        {
            Debug.Log($"{_character.name} está inmovilizado.");
            yield break;
        }


        yield return FollowPath(path);
    }

    public IEnumerator MoveAlongRoutine(List<Vector2Int> path, float customSpeed)
    {
        float originalSpeed = moveSpeed;

        moveSpeed = customSpeed;

        yield return MoveAlongRoutine(path);

        moveSpeed = originalSpeed;
    }

    private IEnumerator FollowPath(List<Vector2Int> path)
    {
        if (_grid == null)
        {
            Debug.LogError("GridSystem es NULL en CharacterMovement");
            yield break;
        }

        foreach (var step in path)
        {
            _grid.UnregisterCharacter(_character.GridPosition);

            Vector3 targetPos = _grid.GridToWorld(step) + _character.VisualOffset;
            Vector3 targetFlat = new Vector3(targetPos.x, transform.position.y, targetPos.z);

            while (Vector3.Distance(transform.position, targetFlat) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetFlat,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = new Vector3(targetFlat.x, transform.position.y, targetFlat.z);

            _character.SetGridPosition(step);

            _grid.RegisterCharacter(step, _character);
            _character.ProcessCurrentTerrain();

            if (!_character.Stats.IsAlive)
                yield break;
        }
    }
}