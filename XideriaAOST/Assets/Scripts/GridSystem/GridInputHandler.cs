using System.Collections;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GridInputHandler : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private LayerMask gridLayer;
    [SerializeField] private GameObject pathTilePrefab;
    [SerializeField] private Vector3 pathTileOffset;
    [SerializeField] private GameObject rangeTilePrefab;
    [SerializeField] private Character playerCharacter;

    private Character selectedCharacter;
    private AStarPathfinding _pathfinding;
    private List<Vector2Int> currentPath = new();
    private List<GameObject> activeTiles = new();
    private MovementRangeCalculator _rangeCalculator;
    private HashSet<Vector2Int> reachableCells = new();
    private List<GameObject> rangeTiles = new();
    private bool isMoving = false;
    private bool isCharacterSelected = false;
    private bool isPlayerTurn = false;
    private ActionMode currentMode = ActionMode.Move;

    private void Start()
    {
        selectedCharacter = null;
        _pathfinding = new AStarPathfinding(gridSystem);
        _rangeCalculator = new MovementRangeCalculator(gridSystem);
    }

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= OnTurnStarted;
    }

    private void Update()
    {
        if (!isPlayerTurn) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isMoving)
        {
            if (isPlayerTurn)
            {
                playerCharacter.FinishTurn();
            }
            return;
        }

        if (isCharacterSelected)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                currentMode = ActionMode.BasicAttack;
                currentPath.Clear();
                ClearTiles();
            }
        }

        if (Keyboard.current.eKey.wasPressedThisFrame && isCharacterSelected && !isMoving)
        {
            var helmet = GetAdjacentHelmet();

            if (helmet != null)
            {
                selectedCharacter.EquipHelmet(helmet.GetHelmet());
                Destroy(helmet.gameObject);
            }
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (!isCharacterSelected)
                return;

            if (currentMode == ActionMode.BasicAttack)
            {
                currentMode = ActionMode.Move;
                ClearTiles();
                return;
            }

            if (currentMode == ActionMode.Move)
            {
                selectedCharacter = null;
                isCharacterSelected = false;
                currentMode = ActionMode.None;

                ClearTiles();
                ClearRangeTiles();
                return;
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }

        //QUITAR REINICIO Y EXIT CUANDO TENGAMOS TIEMPO

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
        }

        if (!isCharacterSelected || selectedCharacter == null)
        {
            ClearTiles();
            ClearRangeTiles();
            return;
        }

        if (isMoving)
            return;

        if (currentMode == ActionMode.Move)
        {
            UpdateMovementRange();
            DrawRangeTiles();

            UpdatePathPreview();
            DrawPathTiles();
        }
        else if (currentMode == ActionMode.BasicAttack)
        {
            UpdateBasicAttackRange();
            DrawRangeTiles();

            currentPath.Clear();
            ClearTiles();
        }
    }

    private void OnTurnStarted(Character character)
    {
        isPlayerTurn = character.IsPlayer;

        selectedCharacter = null;
        isCharacterSelected = false;
        currentMode = ActionMode.Move;

        ClearTiles();
        ClearRangeTiles();
    }

    private void UpdateMovementRange()
    {
        if (selectedCharacter == null) return;

        int energy = selectedCharacter.Stats.CurrentEnergy;

        reachableCells = _rangeCalculator.GetReachableCells(
            selectedCharacter.GridPosition,
            energy
        );
    }

    private void UpdateBasicAttackRange()
    {
        reachableCells.Clear();

        if (selectedCharacter == null)
            return;

        var skill = selectedCharacter.GetBasicSkill();
        var origin = selectedCharacter.GridPosition;

        for (int x = -skill.MaxRange; x <= skill.MaxRange; x++)
        {
            for (int y = -skill.MaxRange; y <= skill.MaxRange; y++)
            {
                int distance = Mathf.Abs(x) + Mathf.Abs(y);

                if (distance < skill.MinRange || distance > skill.MaxRange)
                    continue;

                Vector2Int pos = origin + new Vector2Int(x, y);

                if (gridSystem.IsValidCell(pos))
                {
                    reachableCells.Add(pos);
                }
            }
        }
    }

    private void HandleClick()
    {
        if (isMoving) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!isCharacterSelected)
        {
            if (Physics.Raycast(ray, out RaycastHit hitChar, 100f))
            {
                var character = hitChar.collider.GetComponentInParent<Character>();

                if (character != null && character.IsPlayer && isPlayerTurn)
                {
                    ClearTiles();
                    ClearRangeTiles();

                    selectedCharacter = character;
                    isCharacterSelected = true;
                    currentMode = ActionMode.Move;
                    return;
                }
            }
        }

        if (!isCharacterSelected || selectedCharacter == null)
            return;

        if (!Physics.Raycast(ray, out RaycastHit hitGrid, 100f, gridLayer))
            return;

        Vector2Int targetPos = gridSystem.WorldToGrid(hitGrid.point);

        if (!reachableCells.Contains(targetPos))
        {
            CancelSelection();
            return;
        }

        if (currentMode == ActionMode.Move)
        {
            var path = _pathfinding.FindPath(
                selectedCharacter.GridPosition,
                targetPos
            );

            if (path == null || path.Count <= 1)
                return;

            path.RemoveAt(0);

            int energy = selectedCharacter.Stats.CurrentEnergy;

            if (path.Count > energy)
                path = path.GetRange(0, energy);

            IAction action = new MoveAction(path);
            StartCoroutine(ExecuteSelectedAction(action));
            return;
        }

        if (currentMode == ActionMode.BasicAttack)
        {
            var skill = selectedCharacter.GetBasicSkill();

            Character target = gridSystem.GetCharacterAt(targetPos);

            if (target == null || target.IsPlayer)
            {
                CancelSelection();
                return;
            }

            StartCoroutine(ExecuteSkill(skill, target));
        }
    }

    private IEnumerator ExecuteSkill(SkillBase skill, Character target)
    {
        isMoving = true;

        if (!skill.CanUse(selectedCharacter, target))
        {
            isMoving = false;
            yield break;
        }

        yield return skill.Execute(selectedCharacter, target);

        isMoving = false;

        currentMode = ActionMode.Move;
    }

    private IEnumerator ExecuteSelectedAction(IAction action)
    {
        isMoving = true;

        yield return selectedCharacter.ExecuteAction(action);

        isMoving = false;

        if (currentMode == ActionMode.BasicAttack)
        {
            currentMode = ActionMode.Move;
        }
    }

    private void CancelSelection()
    {
        selectedCharacter = null;
        isCharacterSelected = false;
        currentMode = ActionMode.None;

        ClearTiles();
        ClearRangeTiles();
    }

    private HelmetPickup GetAdjacentHelmet()
    {
        if (selectedCharacter == null)
            return null;

        Vector2Int origin = selectedCharacter.GridPosition;

        Vector2Int[] directions =
        {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

        HelmetPickup[] allHelmets = FindObjectsOfType<HelmetPickup>();

        foreach (var dir in directions)
        {
            Vector2Int adjacentPos = origin + dir;

            foreach (var helmet in allHelmets)
            {
                if (helmet.GridPosition == adjacentPos)
                    return helmet;
            }
        }

        return null;
    }

    private void UpdatePathPreview()
    {
        if (selectedCharacter == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, gridLayer))
            return;

        Vector2Int targetGridPos = gridSystem.WorldToGrid(hit.point);

        if (!gridSystem.IsWalkable(targetGridPos))
        {
            currentPath.Clear();
            return;
        }

        currentPath = _pathfinding.FindPath(
            selectedCharacter.GridPosition,
            targetGridPos
        );
    }

    private void ClearTiles()
    {
        foreach (var tile in activeTiles)
        {
            Destroy(tile);
        }

        activeTiles.Clear();
    }

    private void ClearRangeTiles()
    {
        foreach (var tile in rangeTiles)
            Destroy(tile);

        rangeTiles.Clear();
    }

    private void DrawPathTiles()
    {
        ClearTiles();

        if (currentPath == null || currentPath.Count == 0)
            return;

        foreach (var step in currentPath)
        {
            Vector3 worldPos = gridSystem.GridToWorld(step);
            worldPos += pathTileOffset;

            GameObject tile = Instantiate(pathTilePrefab, worldPos, Quaternion.Euler(90, 0, 0));

            activeTiles.Add(tile);
        }
    }

    private void DrawRangeTiles()
    {
        ClearRangeTiles();

        foreach (var cell in reachableCells)
        {
            Vector3 worldPos = gridSystem.GridToWorld(cell);
            worldPos += pathTileOffset;

            var tile = Instantiate(rangeTilePrefab, worldPos, Quaternion.Euler(90, 0, 0));
            rangeTiles.Add(tile);
        }
    }

    private void OnDrawGizmos()
    {
        if (currentPath == null || currentPath.Count == 0 || gridSystem == null)
            return;

        Gizmos.color = Color.green;

        for (int i = 0; i < currentPath.Count; i++)
        {
            Vector3 worldPos = gridSystem.GridToWorld(currentPath[i]);

            Gizmos.DrawSphere(worldPos + Vector3.up * 0.2f, 0.1f);

            if (i < currentPath.Count - 1)
            {
                Vector3 nextWorld = gridSystem.GridToWorld(currentPath[i + 1]);
                Gizmos.DrawLine(worldPos + Vector3.up * 0.2f, nextWorld + Vector3.up * 0.2f);
            }
        }
    }

    private enum ActionMode
    {
        None,
        Move,
        BasicAttack
    }
}