using System.Collections;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;

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

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            playerCharacter.FinishTurn();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            selectedCharacter = null;
            isCharacterSelected = false;

            ClearTiles();
            ClearRangeTiles();

            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }

        if (!isCharacterSelected || selectedCharacter == null)
        {
            ClearTiles();
            ClearRangeTiles();
            return;
        }

        if (isMoving)
            return;

        UpdateMovementRange();
        DrawRangeTiles();

        UpdatePathPreview();
        DrawPathTiles();
    }

    private void OnTurnStarted(Character character)
    {
        isPlayerTurn = character == playerCharacter;

        selectedCharacter = null;
        isCharacterSelected = false;

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

    private void HandleClick()
    {
        if (isMoving) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!isCharacterSelected)
        {
            if (Physics.Raycast(ray, out RaycastHit hitChar, 100f))
            {
                var character = hitChar.collider.GetComponentInParent<Character>();

                if (character != null && character == playerCharacter)
                {
                    selectedCharacter = character;
                    isCharacterSelected = true;
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
            return;

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

        var action = new MoveAction(path);
        StartCoroutine(selectedCharacter.ExecuteAction(action));
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
}