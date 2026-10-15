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
        yield return FollowPath(path);
    }

    public IEnumerator MoveAlongRoutine(
        List<Vector2Int> path,
        System.Action onStepCompleted,
        System.Func<bool> canCompleteStep)
    {
        yield return FollowPath(path, onStepCompleted, canCompleteStep);
    }

    public IEnumerator MoveAlongRoutine(List<Vector2Int> path, float customSpeed)
    {
        float originalSpeed = moveSpeed;

        moveSpeed = customSpeed;

        try
        {
            yield return MoveAlongRoutine(path);
        }
        finally
        {
            moveSpeed = originalSpeed;
        }
    }

    private bool CanMove()
    {
        return _character != null && _character.Stats != null &&
            isActiveAndEnabled && _character.gameObject.activeInHierarchy &&
            _character.Stats.IsAlive && !_character.IsDying &&
            !_character.HasStatusEffect<RootEffect>();
    }

    private IEnumerator FollowPath(
        List<Vector2Int> path,
        System.Action onStepCompleted = null,
        System.Func<bool> canCompleteStep = null)
    {
        if (_grid == null || path == null || path.Count == 0 || !CanMove())
            yield break;

        CharacterAnimationController anim =
            _character.GetComponentInChildren<CharacterAnimationController>();

        anim?.SetWalking(true);

        try
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int step = path[i];

                if (!CanMove() || !_grid.CanCharacterOccupy(_character, step, ignoreSelf: true) ||
                    (canCompleteStep != null && !canCompleteStep()))
                    yield break;

                Vector2Int moveDirection = step - _character.GridPosition;
                _character.FaceDirection(moveDirection);

                Vector3 targetPos = _grid.GridToWorld(step, _character) + _character.VisualOffset;
                Vector3 targetFlat = new Vector3(targetPos.x, transform.position.y, targetPos.z);

                bool isLastStep = (i == path.Count - 1);
                bool walkStopped = false;

                // Keep the last completed footprint occupied throughout the animation.
                while (Vector3.Distance(transform.position, targetFlat) > 0.05f)
                {
                    if (!CanMove())
                        yield break;

                    if (isLastStep && !walkStopped &&
                        Vector3.Distance(transform.position, targetFlat) < 0.5f)
                    {
                        anim?.SetWalking(false);
                        walkStopped = true;
                    }

                    transform.position = Vector3.MoveTowards(
                        transform.position, targetFlat, moveSpeed * Time.deltaTime);
                    yield return null;
                }

                // The board or available energy may have changed during the step.
                if (!CanMove() || !_grid.CanCharacterOccupy(_character, step, ignoreSelf: true) ||
                    (canCompleteStep != null && !canCompleteStep()))
                    yield break;

                transform.position = targetFlat;
                _grid.UnregisterCharacter(_character);
                _character.SetGridPosition(step);
                _grid.RegisterCharacter(_character);

                // Charge only committed steps, before terrain effects or session saving.
                onStepCompleted?.Invoke();
                _character.ProcessCurrentTerrain();

                if (!CanMove())
                    yield break;

                if (_character.IsPlayer)
                    CheckSpecialCell(step);
            }
        }
        finally
        {
            if (anim != null)
                anim.SetWalking(false);

            if (_grid != null && _character != null &&
                _character.gameObject.activeInHierarchy &&
                _character.Stats.IsAlive && !_character.IsDying)
            {
                Vector3 committedPosition =
                    _grid.GridToWorld(_character.GridPosition, _character) + _character.VisualOffset;
                transform.position = new Vector3(
                    committedPosition.x, transform.position.y, committedPosition.z);
            }
        }
    }

    public IEnumerator MoveToCell(Vector2Int destination, float customSpeed)
    {
        float originalSpeed = moveSpeed;

        moveSpeed = customSpeed;

        List<Vector2Int> path = new()
    {
        destination
    };

        try
        {
            yield return FollowPath(path);
        }
        finally
        {
            moveSpeed = originalSpeed;
        }
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