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
            yield break;
        }

        CharacterAnimationController anim =
            _character.GetComponentInChildren<CharacterAnimationController>();

        anim?.SetWalking(true);

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int step = path[i];

            _character.FaceCell(step);

            _grid.UnregisterCharacter(_character.GridPosition);

            Vector3 targetPos = _grid.GridToWorld(step) + _character.VisualOffset;
            Vector3 targetFlat = new Vector3(targetPos.x, transform.position.y, targetPos.z);

            bool isLastStep = (i == path.Count - 1);
            bool walkStopped = false;

            while (Vector3.Distance(transform.position, targetFlat) > 0.05f)
            {
                if (isLastStep &&
                    !walkStopped &&
                    Vector3.Distance(transform.position, targetFlat) < 0.5f)
                {
                    anim?.SetWalking(false);
                    walkStopped = true;
                }

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetFlat,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = new Vector3(
                targetFlat.x,
                transform.position.y,
                targetFlat.z
            );

            _character.SetGridPosition(step);

            _grid.RegisterCharacter(step, _character);
            _character.ProcessCurrentTerrain();

            if (_character.IsPlayer)
            {
                CheckSpecialCell(step);
            }

            if (!_character.Stats.IsAlive)
            {
                anim?.SetWalking(false);
                yield break;
            }

            if (_character.HasStatusEffect<RootEffect>())
            {
                anim?.SetWalking(false);
                yield break;
            }
        }

        anim?.SetWalking(false);
    }

    public IEnumerator MoveToCell(Vector2Int destination, float customSpeed)
    {
        float originalSpeed = moveSpeed;

        moveSpeed = customSpeed;

        List<Vector2Int> path = new()
    {
        destination
    };

        yield return FollowPath(path);

        moveSpeed = originalSpeed;
    }

    private void CheckSpecialCell(Vector2Int position)
    {
        GameStateManager gameStateManager =
            FindFirstObjectByType<GameStateManager>();

        if (gameStateManager == null ||
            !gameStateManager.IsExplorationMode)
        {
            return;
        }

        GridCell cell = _grid.GetCell(position);
        CellType type = cell.Type;

        switch (type)
        {
            case CellType.ExitTutorial:
                LoadSpecialCellScene(position);
                break;

            case CellType.ExitRun:
                LoadSpecialCellScene(position);
                break;

            case CellType.Shop:
                Debug.Log("Tienda todavía no implementada.");
                break;
        }
    }

    private void LoadSpecialCellScene(Vector2Int position)
    {
        string targetScene = _grid.GetTargetScene(position);

        if (string.IsNullOrWhiteSpace(targetScene))
        {
            return;
        }

        bool isReturningToLobby =
            string.Equals(
                targetScene,
                "Lobby",
                System.StringComparison.OrdinalIgnoreCase
            );

        if (isReturningToLobby)
        {
            PlayerSessionData.Instance?.ResetSession();
        }
        else
        {
            _character.SavePlayerSession();
        }

        SceneTransitionManager.Instance.LoadScene(targetScene);
    }
}