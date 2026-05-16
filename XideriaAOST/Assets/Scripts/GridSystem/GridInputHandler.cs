using System;
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
    [SerializeField] private GameObject aoeTilePrefab;
    [SerializeField] private BattleHUD battleHUD;
    [SerializeField] private CharacterInfoHUD characterInfoHUD;

    private List<GameObject> aoeTiles = new();
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
    private int selectedSkillIndex = 0;
    private HelmetPickup hoveredHelmet;
    [SerializeField] private HelmetHoverUI helmetHoverUI;

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
        if (GameSession.IsGameOver)
            return;

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
            if (Keyboard.current.qKey.wasPressedThisFrame)
                SelectSkill(0);

            if (Keyboard.current.wKey.wasPressedThisFrame)
                SelectSkill(1);

            if (Keyboard.current.eKey.wasPressedThisFrame)
                SelectSkill(2);

            if (Keyboard.current.rKey.wasPressedThisFrame)
                SelectSkill(3);
        }

        UpdateHelmetHover();

        if (Keyboard.current.fKey.wasPressedThisFrame && isCharacterSelected && !isMoving)
        {
            if (hoveredHelmet != null)
            {
                selectedCharacter.EquipHelmet(
                    hoveredHelmet.GetHelmet());

                Destroy(hoveredHelmet.gameObject);

                hoveredHelmet = null;

                helmetHoverUI.Hide();

                battleHUD.Refresh();
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
                ClearAoeTiles();

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

        if (Keyboard.current.pKey.wasPressedThisFrame)
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
            ClearAoeTiles();

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

            DrawAoePreview();
        }
    }

    private void OnTurnStarted(Character character)
    {
        StartCoroutine(RefreshHudNextFrame(character));
    }

    private void UpdateMovementRange()
    {
        if (selectedCharacter == null) return;

        int energy = selectedCharacter.Stats.CurrentEnergy / selectedCharacter.GetMovementCostPerCell();

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

        var skill = selectedCharacter.GetSkill(selectedSkillIndex);
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

                if (character != null)
                {
                    battleHUD.ShowCharacter(character);

                    if (character.IsPlayer && isPlayerTurn)
                    {
                        ClearTiles();
                        ClearRangeTiles();
                        ClearAoeTiles();

                        selectedCharacter = character;
                        isCharacterSelected = true;
                        currentMode = ActionMode.Move;
                    }

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

            Vector2Int finalDestination = GetLastValidDestination(path);

            if (finalDestination == selectedCharacter.GridPosition)
                return;

            path = _pathfinding.FindPath(
                selectedCharacter.GridPosition,
                finalDestination
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
            var skill = selectedCharacter.GetSkill(selectedSkillIndex);

            if (skill == null)
            {
                CancelSelection();
                return;
            }

            if (!skill.CanUse(selectedCharacter, targetPos, gridSystem))
            {
                CancelSelection();
                return;
            }

            StartCoroutine(ExecuteSkill(skill, targetPos));
            return;
        }
    }

    private IEnumerator ExecuteSkill(SkillBase skill, Vector2Int targetCell)
    {
        if (isMoving)
            yield break;

        if (skill == null || selectedCharacter == null)
            yield break;

        if (!skill.CanUse(selectedCharacter, targetCell, gridSystem))
            yield break;

        isMoving = true;

        yield return skill.Execute(selectedCharacter, targetCell, gridSystem);

        isMoving = false;
        currentMode = ActionMode.Move;

        battleHUD.Refresh();
    }

    private void SelectSkill(int index)
    {
        if (isMoving) return;

        var skill = selectedCharacter.GetSkill(index);

        if (skill == null)
            return;

        selectedSkillIndex = index;
        battleHUD.SetSelectedSkill(index);

        Debug.Log($"Skill seleccionada: {skill.Name}");

        if (skill.TargetType == SkillTargetType.Self)
        {
            StartCoroutine(ExecuteSkill(skill, selectedCharacter.GridPosition));
            return;
        }

        currentMode = ActionMode.BasicAttack;

        ClearTiles();
        ClearRangeTiles();
        ClearAoeTiles();
    }

    private IEnumerator ExecuteSelectedAction(IAction action)
    {
        isMoving = true;

        yield return selectedCharacter.ExecuteAction(action);

        isMoving = false;

        battleHUD.Refresh();

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
        selectedSkillIndex = -1;
        hoveredHelmet = null;
        helmetHoverUI.Hide();

        battleHUD.SetSelectedSkill(-1);

        ClearTiles();
        ClearRangeTiles();
        ClearAoeTiles();
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

    private IEnumerator RefreshHudNextFrame(Character character)
    {
        isPlayerTurn = character.IsPlayer;

        selectedCharacter = null;
        isCharacterSelected = false;
        currentMode = ActionMode.Move;
        selectedSkillIndex = -1;

        ClearTiles();
        ClearRangeTiles();
        ClearAoeTiles();

        battleHUD.SetPlayerTurn(isPlayerTurn);

        yield return null;

        battleHUD.ShowCharacter(character);
        battleHUD.SetSelectedSkill(-1);
    }

    public void SelectSkillFromUI(int index)
    {
        if (!isPlayerTurn) return;
        if (!isCharacterSelected) return;
        if (selectedCharacter == null) return;
        if (isMoving) return;

        SelectSkill(index);
    }

    public void EndTurnFromUI()
    {
        if (!isPlayerTurn) return;
        if (isMoving) return;

        playerCharacter.FinishTurn();
    }

    private bool IsHelmetAt(Vector2Int pos)
    {
        HelmetPickup[] helmets = FindObjectsOfType<HelmetPickup>();

        foreach (var helmet in helmets)
        {
            if (helmet.GridPosition == pos)
                return true;
        }

        return false;
    }

    private void UpdateHelmetHover()
    {
        hoveredHelmet = null;

        helmetHoverUI.Hide();

        if (!isCharacterSelected || selectedCharacter == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
            return;

        HelmetPickup helmet =
            hit.collider.GetComponentInParent<HelmetPickup>();

        if (helmet == null)
            return;

        int distance =
            Mathf.Abs(
                helmet.GridPosition.x - selectedCharacter.GridPosition.x)
            +
            Mathf.Abs(
                helmet.GridPosition.y - selectedCharacter.GridPosition.y);

        if (distance != 1)
            return;

        hoveredHelmet = helmet;

        Vector3 screenPos =
            mainCamera.WorldToScreenPoint(
                helmet.transform.position + Vector3.up * 2f);

        helmetHoverUI.Show(
            $"[F] Recoger {helmet.GetHelmet().Name}",
            screenPos);
    }

    private Vector2Int GetLastValidDestination(List<Vector2Int> path)
    {
        // path viene con origen incluido
        for (int i = path.Count - 1; i >= 1; i--)
        {
            Vector2Int pos = path[i];

            if (!gridSystem.IsWalkable(pos))
                continue;

            if (gridSystem.IsOccupied(pos))
                continue;

            if (IsHelmetAt(pos))
                continue;

            return pos;
        }

        return path[0];
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

    private void ClearAoeTiles()
    {
        foreach (var tile in aoeTiles)
            Destroy(tile);

        aoeTiles.Clear();
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

    private void DrawAoePreview()
    {
        ClearAoeTiles();

        if (selectedCharacter == null)
            return;

        var skill = selectedCharacter.GetSkill(selectedSkillIndex);

        if (skill == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, gridLayer))
            return;

        Vector2Int targetPos = gridSystem.WorldToGrid(hit.point);

        foreach (var cell in skill.GetPreviewCells(selectedCharacter.GridPosition, targetPos))
        {
            if (!gridSystem.IsValidCell(cell))
                continue;

            Vector3 worldPos = gridSystem.GridToWorld(cell);
            worldPos += pathTileOffset;

            var tile = Instantiate(
                aoeTilePrefab,
                worldPos,
                Quaternion.Euler(90, 0, 0)
            );

            aoeTiles.Add(tile);
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