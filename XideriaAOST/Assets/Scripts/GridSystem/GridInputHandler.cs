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
    [SerializeField] private GameObject movementRangeTilePrefab;
    [SerializeField] private GameObject skillRangeTilePrefab;
    [SerializeField] private Vector3 pathTileOffset;
    [SerializeField] private GameObject rangeTilePrefab;
    [SerializeField] private Character playerCharacter;
    [SerializeField] private GameObject aoeTilePrefab;
    [SerializeField] private BattleHUD battleHUD;
    [SerializeField] private CharacterInfoHUD characterInfoHUD;
    [SerializeField] private GameObject dangerRangeTilePrefab;
    [SerializeField] private EnemyHoverUI enemyHoverUI;
    [SerializeField] private TileObjectPool tilePool;

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
    private bool isHelmetInventoryOpen = false;
    private ActionMode currentMode = ActionMode.Move;
    private int selectedSkillIndex = 0;
    private CharacterPositionIndicator selectedIndicator;
    private CharacterPositionIndicator turnIndicator;
    private List<GameObject> dangerRangeTiles = new();
    private HashSet<Vector2Int> dangerousPreviewCells = new();
    private GameStateManager gameStateManager;

    private const float SkillRangeStateCheckInterval = 0.1f;
    private bool skillRangeDirty = true;
    private Character cachedSkillCharacter;
    private SkillBase cachedSkill;
    private GridSystem cachedSkillGrid;
    private Vector2Int cachedSkillOrigin;
    private int cachedSkillWidth;
    private int cachedSkillHeight;
    private int cachedSkillEnergy;
    private int cachedSkillCooldown;
    private float nextSkillRangeStateCheck;
    private readonly List<int> skillRangeWorldState = new();
    private readonly List<int> pendingSkillRangeWorldState = new();

    private bool movementRangeDirty = true;
    private Character cachedMovementCharacter;
    private GridSystem cachedMovementGrid;
    private Vector2Int cachedMovementOrigin;
    private int cachedMovementWidth;
    private int cachedMovementHeight;
    private int cachedMovementMaxSteps;
    private bool cachedMovementExplorationMode;
    private float nextMovementRangeStateCheck;
    private readonly List<int> movementRangeWorldState = new();
    private readonly Dictionary<Vector2Int, List<Vector2Int>> movementPaths = new();

    private void Awake()
    {
        gameStateManager = FindFirstObjectByType<GameStateManager>();
    }

    private void Start()
    {
        selectedCharacter = playerCharacter;
        isCharacterSelected = playerCharacter != null;
        currentMode = ActionMode.Move;

        _pathfinding = new AStarPathfinding(gridSystem);
        _rangeCalculator = new MovementRangeCalculator(gridSystem);

        if (playerCharacter != null)
        {
            selectedIndicator =
                playerCharacter.GetComponentInChildren<CharacterPositionIndicator>();

            selectedIndicator?.SetSelected(true);

            battleHUD.ShowCharacter(playerCharacter);
        }
    }

    private void OnEnable()
    {
        skillRangeDirty = true;
        movementRangeDirty = true;
        GameEvents.OnTurnStarted += OnTurnStarted;
        GameEvents.OnEnergyRecovered += OnEnergyRecovered;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= OnTurnStarted;
        GameEvents.OnEnergyRecovered -= OnEnergyRecovered;
    }

    private void OnEnergyRecovered(Character character, int amount)
    {
        if (character != playerCharacter || amount <= 0)
            return;

        skillRangeDirty = true;
        movementRangeDirty = true;

        var spawner = character.GetComponent<FloatingDamageSpawner>();
        if (spawner == null || battleHUD == null || !battleHUD.TryGetEnergyColor(out Color color))
        {
            Debug.LogWarning("No se puede mostrar Impulso Vital: falta el texto flotante o el color de energía del HUD.", this);
        }
        else
        {
            spawner.ShowEnergyRecovery(amount, color, character.GetHeadAnchor());
        }

        battleHUD?.Refresh();
    }

    private void Update()
    {
        if (GameSession.IsGameOver)
            return;

        UpdateEnemyHover();

        if (!isPlayerTurn) return;

        if (isHelmetInventoryOpen)
            return;

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

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (!isCharacterSelected)
                return;

            if (currentMode == ActionMode.SelfSkillConfirm)
            {
                currentMode = ActionMode.Move;
                movementRangeDirty = true;

                battleHUD.SetSelectedSkill(-1);

                ClearTiles();
                ClearAoeTiles();
                ClearRangeTiles();

                return;
            }

            if (currentMode == ActionMode.BasicAttack)
            {
                currentMode = ActionMode.Move;
                movementRangeDirty = true;

                battleHUD.SetSelectedSkill(-1);
                ClearTiles();
                ClearAoeTiles();
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
            if (PlayerSessionData.Instance != null)
                PlayerSessionData.Instance.ResetSession();

            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadScene("Inicio");
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

            bool explorationMode =
                gameStateManager != null &&
                gameStateManager.IsExplorationMode;

            if (explorationMode)
            {
                ClearRangeTiles();
            }
            else
            {
                DrawRangeTiles(movementRangeTilePrefab);
            }

            UpdatePathPreview();
            DrawPathTiles();
        }
        else if (currentMode == ActionMode.BasicAttack)
        {
            if (UpdateBasicAttackRange())
                DrawRangeTiles(skillRangeTilePrefab);

            currentPath.Clear();
            ClearTiles();

            DrawAoePreview();
        }
        else if (currentMode == ActionMode.SelfSkillConfirm)
        {
            currentPath.Clear();
            ClearTiles();
            ClearRangeTiles();

            var skill = selectedCharacter.GetSkill(selectedSkillIndex);
            DrawSelfSkillPreview(skill);
        }
    }

    private void OnTurnStarted(Character character)
    {
        skillRangeDirty = true;
        movementRangeDirty = true;
        StartCoroutine(RefreshHudNextFrame(character));
    }

    private void UpdateMovementRange()
    {
        if (selectedCharacter == null || gridSystem == null ||
            selectedCharacter.Movement == null || !selectedCharacter.Movement.isActiveAndEnabled ||
            !selectedCharacter.gameObject.activeInHierarchy ||
            !selectedCharacter.Stats.IsAlive || selectedCharacter.IsDying ||
            selectedCharacter.HasStatusEffect<RootEffect>())
        {
            reachableCells.Clear();
            movementPaths.Clear();
            dangerousPreviewCells.Clear();
            movementRangeDirty = true;
            return;
        }

        bool explorationMode =
            gameStateManager != null &&
            gameStateManager.IsExplorationMode;

        int maxSteps;

        if (explorationMode)
        {
            maxSteps = gridSystem.Width * gridSystem.Height;
        }
        else
        {
            maxSteps =
                selectedCharacter.Stats.CurrentEnergy /
                selectedCharacter.GetMovementCostPerCell();
        }

        bool selectionChanged = movementRangeDirty ||
            cachedMovementCharacter != selectedCharacter ||
            cachedMovementGrid != gridSystem ||
            cachedMovementOrigin != selectedCharacter.GridPosition ||
            cachedMovementWidth != selectedCharacter.GridWidth ||
            cachedMovementHeight != selectedCharacter.GridHeight ||
            cachedMovementMaxSteps != maxSteps ||
            cachedMovementExplorationMode != explorationMode;

        if (!selectionChanged && Time.unscaledTime < nextMovementRangeStateCheck)
            return;

        bool worldChanged = HasRangeWorldChanged(movementRangeWorldState);
        nextMovementRangeStateCheck = Time.unscaledTime + SkillRangeStateCheckInterval;

        if (!selectionChanged && !worldChanged)
            return;

        var candidates = _rangeCalculator.GetReachableCells(
            selectedCharacter.GridPosition,
            maxSteps,
            selectedCharacter
        );

        reachableCells.Clear();
        movementPaths.Clear();

        // A* may prefer a longer route due to terrain. Advertise only destinations
        // that the chosen route can actually reach with the current step budget.
        foreach (Vector2Int candidate in candidates)
        {
            List<Vector2Int> path = GetValidMovementPath(candidate);
            if (path == null)
                continue;

            reachableCells.Add(candidate);
            movementPaths[candidate] = path;
        }

        cachedMovementCharacter = selectedCharacter;
        cachedMovementGrid = gridSystem;
        cachedMovementOrigin = selectedCharacter.GridPosition;
        cachedMovementWidth = selectedCharacter.GridWidth;
        cachedMovementHeight = selectedCharacter.GridHeight;
        cachedMovementMaxSteps = maxSteps;
        cachedMovementExplorationMode = explorationMode;
        movementRangeWorldState.Clear();
        movementRangeWorldState.AddRange(pendingSkillRangeWorldState);
        movementRangeDirty = false;

        if (!explorationMode)
            UpdateDangerousPreviewCells();
        else
            dangerousPreviewCells.Clear();
    }

    private bool UpdateBasicAttackRange()
    {
        if (selectedCharacter == null || gridSystem == null)
            return false;

        var skill = selectedCharacter.GetSkill(selectedSkillIndex);
        if (skill == null)
        {
            reachableCells.Clear();
            skillRangeDirty = true;
            return rangeTiles.Count > 0;
        }

        var origin = selectedCharacter.GridPosition;
        int energy = selectedCharacter.Stats.CurrentEnergy;
        int cooldown = selectedCharacter.GetSkillCooldown(skill);

        bool selectionChanged = skillRangeDirty ||
            cachedSkillCharacter != selectedCharacter ||
            cachedSkill != skill ||
            cachedSkillGrid != gridSystem ||
            cachedSkillOrigin != origin ||
            cachedSkillWidth != selectedCharacter.GridWidth ||
            cachedSkillHeight != selectedCharacter.GridHeight ||
            cachedSkillEnergy != energy ||
            cachedSkillCooldown != cooldown;

        // Reuse both the visual range cells and their tiles while state is unchanged.
        if (!selectionChanged && Time.unscaledTime < nextSkillRangeStateCheck)
            return false;

        bool worldChanged = HasSkillRangeWorldChanged();
        nextSkillRangeStateCheck = Time.unscaledTime + SkillRangeStateCheckInterval;

        if (!selectionChanged && !worldChanged)
            return false;

        reachableCells.Clear();

        // Show geometric coverage even without a valid target. Also include
        // valid targets' full footprints, whose cells may extend beyond it.
        for (int x = 0; x < gridSystem.Width; x++)
        {
            for (int y = 0; y < gridSystem.Height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);

                if (skill.IsInRange(origin, pos) ||
                    skill.CanUse(selectedCharacter, pos, gridSystem))
                    reachableCells.Add(pos);
            }
        }

        cachedSkillCharacter = selectedCharacter;
        cachedSkill = skill;
        cachedSkillGrid = gridSystem;
        cachedSkillOrigin = origin;
        cachedSkillWidth = selectedCharacter.GridWidth;
        cachedSkillHeight = selectedCharacter.GridHeight;
        cachedSkillEnergy = energy;
        cachedSkillCooldown = cooldown;
        skillRangeWorldState.Clear();
        skillRangeWorldState.AddRange(pendingSkillRangeWorldState);
        skillRangeDirty = false;
        return true;
    }

    private bool HasSkillRangeWorldChanged()
    {
        return HasRangeWorldChanged(skillRangeWorldState);
    }

    private bool HasRangeWorldChanged(List<int> previousWorldState)
    {
        // GridSystem has no change event. Compare a small, reusable snapshot
        // periodically instead of running CanUse across the board every frame.
        pendingSkillRangeWorldState.Clear();
        pendingSkillRangeWorldState.Add(gridSystem.Width);
        pendingSkillRangeWorldState.Add(gridSystem.Height);

        for (int x = 0; x < gridSystem.Width; x++)
        {
            for (int y = 0; y < gridSystem.Height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                GridCell cell = gridSystem.GetCell(pos);
                Character occupant = gridSystem.GetCharacterAt(pos);

                pendingSkillRangeWorldState.Add(cell == null ? -1 : (int)cell.Type);
                pendingSkillRangeWorldState.Add(cell != null && cell.Walkable ? 1 : 0);
                pendingSkillRangeWorldState.Add(occupant == null ? 0 : occupant.GetInstanceID());

                if (occupant == null)
                    continue;

                pendingSkillRangeWorldState.Add(occupant.GridPosition.x);
                pendingSkillRangeWorldState.Add(occupant.GridPosition.y);
                pendingSkillRangeWorldState.Add(occupant.GridWidth);
                pendingSkillRangeWorldState.Add(occupant.GridHeight);
                pendingSkillRangeWorldState.Add(occupant.IsPlayer ? 1 : 0);
                pendingSkillRangeWorldState.Add(occupant.gameObject.activeInHierarchy ? 1 : 0);
                pendingSkillRangeWorldState.Add(occupant.IsDying ? 1 : 0);
                pendingSkillRangeWorldState.Add(occupant.Stats.CurrentHP);

                var effects = occupant.GetStatusEffects();
                pendingSkillRangeWorldState.Add(effects.Count);
                foreach (var effect in effects)
                    pendingSkillRangeWorldState.Add(effect.RemainingTurns);
            }
        }

        foreach (HelmetPickup helmet in FindObjectsByType<HelmetPickup>(FindObjectsSortMode.InstanceID))
        {
            pendingSkillRangeWorldState.Add(helmet.GetInstanceID());
            pendingSkillRangeWorldState.Add(helmet.GridPosition.x);
            pendingSkillRangeWorldState.Add(helmet.GridPosition.y);
            pendingSkillRangeWorldState.Add(helmet.IsCollected ? 1 : 0);
        }

        if (previousWorldState.Count != pendingSkillRangeWorldState.Count)
            return true;

        for (int i = 0; i < previousWorldState.Count; i++)
        {
            if (previousWorldState[i] != pendingSkillRangeWorldState[i])
                return true;
        }

        return false;
    }

    private void HandleClick()
    {
        if (isMoving) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!isCharacterSelected || selectedCharacter == null)
            return;

        if (currentMode == ActionMode.SelfSkillConfirm)
        {
            var skill = selectedCharacter.GetSkill(selectedSkillIndex);

            if (skill == null)
            {
                return;
            }

            if (!skill.CanUse(selectedCharacter, selectedCharacter.GridPosition, gridSystem))
            {
                battleHUD.FlashSkillError(selectedSkillIndex);
                return;
            }

            StartCoroutine(ExecuteSkill(skill, selectedCharacter.GridPosition));
            return;
        }

        if (!Physics.Raycast(ray, out RaycastHit hitGrid, 100f, gridLayer))
            return;

        Vector2Int targetPos = gridSystem.WorldToGrid(hitGrid.point);

        if (currentMode == ActionMode.Move)
        {
            if (!reachableCells.Contains(targetPos))
                return;

            // Validate live at the click, independently of the cached preview.
            var path = GetValidMovementPath(targetPos);
            if (path == null)
                return;

            path.RemoveAt(0);

            bool explorationMode =
                gameStateManager != null &&
                gameStateManager.IsExplorationMode;

            IAction action = new MoveAction(
                path,
                ignoreEnergyCost: explorationMode
            );
            StartCoroutine(ExecuteSelectedAction(action));
            return;
        }

        if (currentMode == ActionMode.BasicAttack)
        {
            var skill = selectedCharacter.GetSkill(selectedSkillIndex);

            if (skill == null)
            {
                return;
            }

            if (!skill.CanUse(selectedCharacter, targetPos, gridSystem))
            {
                battleHUD.FlashSkillError(selectedSkillIndex);
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
        movementRangeDirty = true;
        battleHUD.SetSelectedSkill(-1);
        battleHUD.Refresh();
    }

    private void SelectSkill(int index)
    {
        if (isMoving) return;
        if (selectedCharacter == null) return;

        var skill = selectedCharacter.GetSkill(index);

        if (skill == null)
            return;

        if (currentMode == ActionMode.SelfSkillConfirm &&
            selectedSkillIndex == index)
        {
            StartCoroutine(ExecuteSkill(skill, selectedCharacter.GridPosition));
            return;
        }

        if (!selectedCharacter.Stats.HasEnergy(skill.EnergyCost) ||
            selectedCharacter.IsSkillOnCooldown(skill))
        {
            battleHUD.FlashSkillError(index);
            return;
        }

        selectedSkillIndex = index;
        skillRangeDirty = true;
        movementRangeDirty = true;

        if (skill.TargetType == SkillTargetType.Self)
        {
            battleHUD.SetSelectedSkill(index);

            currentMode = ActionMode.SelfSkillConfirm;

            ClearTiles();
            ClearRangeTiles();
            ClearAoeTiles();

            DrawSelfSkillPreview(skill);

            return;
        }

        battleHUD.SetSelectedSkill(index);

        currentMode = ActionMode.BasicAttack;

        ClearTiles();
        ClearRangeTiles();
        ClearAoeTiles();
    }

    private IEnumerator ExecuteSelectedAction(IAction action)
    {
        isMoving = true;

        try
        {
            yield return selectedCharacter.ExecuteAction(action);

            if (selectedCharacter != null && selectedCharacter.Stats.IsAlive &&
                !selectedCharacter.IsDying && selectedCharacter.gameObject.activeInHierarchy)
            {
                HelmetPickup adjacentHelmet = GetAdjacentHelmet();
                if (adjacentHelmet != null)
                    yield return PickupHelmetRoutine(adjacentHelmet);
            }
        }
        finally
        {
            isMoving = false;
            movementRangeDirty = true;
        }

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

        selectedIndicator?.SetSelected(false);
        selectedIndicator = null;

        battleHUD.SetSelectedSkill(-1);
        battleHUD.Clear();

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

    public bool CanOpenHelmetInventory()
    {
        return isPlayerTurn &&
               !isMoving &&
               !GameSession.IsGameOver;
    }

    public void SetHelmetInventoryOpen(bool isOpen)
    {
        isHelmetInventoryOpen = isOpen;
        movementRangeDirty = true;

        if (isOpen)
        {
            ClearTiles();
            ClearRangeTiles();
            ClearAoeTiles();

            battleHUD.SetSelectedSkill(-1);

            currentMode = ActionMode.Move;
        }
        else
        {
            if (isPlayerTurn &&
                isCharacterSelected &&
                selectedCharacter != null)
            {
                UpdateMovementRange();

                bool explorationMode =
                    gameStateManager != null &&
                    gameStateManager.IsExplorationMode;

                if (!explorationMode)
                    DrawRangeTiles(movementRangeTilePrefab);
            }
        }
    }

    private IEnumerator RefreshHudNextFrame(Character character)
    {
        isPlayerTurn = character.IsPlayer;

        selectedSkillIndex = -1;

        ClearTiles();
        ClearRangeTiles();
        ClearAoeTiles();

        turnIndicator?.SetTurnActive(false);
        turnIndicator = character.GetComponentInChildren<CharacterPositionIndicator>();
        turnIndicator?.SetTurnActive(true);

        if (isPlayerTurn)
        {
            selectedCharacter = character;
            isCharacterSelected = true;
            currentMode = ActionMode.Move;

            selectedIndicator?.SetSelected(false);
            selectedIndicator = selectedCharacter.GetComponentInChildren<CharacterPositionIndicator>();
            selectedIndicator?.SetSelected(true);

            UpdateMovementRange();

            bool explorationMode =
                gameStateManager != null &&
                gameStateManager.IsExplorationMode;

            if (explorationMode)
            {
                ClearRangeTiles();
            }
            else
            {
                DrawRangeTiles(movementRangeTilePrefab);
            }

            currentPath.Clear();
            ClearTiles();
        }

        battleHUD.SetPlayerTurn(isPlayerTurn);

        yield return null;

        battleHUD.ShowCharacter(playerCharacter);
        battleHUD.SetSelectedSkill(-1);
    }

    private void UpdateEnemyHover()
    {
        if (enemyHoverUI == null)
        {
            Debug.LogError("[ENEMY HOVER] enemyHoverUI es NULL");
            return;
        }

        Ray ray =
            mainCamera.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );

        RaycastHit[] hits =
            Physics.RaycastAll(ray, 100f);

        Character hoveredEnemy = null;

        foreach (RaycastHit hit in hits)
        {
            Character character =
                hit.collider.GetComponentInParent<Character>();

            if (character == null)
                continue;

            if (character.IsPlayer)
                continue;

            hoveredEnemy = character;
            break;
        }

        if (hoveredEnemy == null)
        {
            enemyHoverUI.Hide();
            return;
        }

        enemyHoverUI.Show(hoveredEnemy);
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

    private IEnumerator PickupHelmetRoutine(HelmetPickup helmet)
    {
        if (helmet == null || selectedCharacter == null)
            yield break;

        HelmetData helmetData = helmet.GetHelmet();

        if (helmetData == null)
            yield break;

        if (HelmetInventoryData.Instance == null)
        {
            Debug.LogError(
                "[HELMET PICKUP] HelmetInventoryData.Instance es NULL."
            );

            yield break;
        }

        bool stored =
            HelmetInventoryData.Instance.AddHelmet(helmetData.Type);

        if (!stored)
        {
            Debug.Log(
                $"[HELMET PICKUP] No se pudo almacenar {helmetData.Name}."
            );

            yield break;
        }

        helmet.MarkAsCollected();

        HelmetPickupAnimator pickupAnimator =
            helmet.GetComponentInChildren<HelmetPickupAnimator>();

        if (pickupAnimator != null)
            yield return pickupAnimator.PlayPickup();

        Destroy(helmet.gameObject);

        yield return null;

        UpdateMovementRange();

        battleHUD.Refresh();
    }

    private List<Vector2Int> GetValidMovementPath(Vector2Int destination)
    {
        if (selectedCharacter == null || gridSystem == null ||
            selectedCharacter.Movement == null || !selectedCharacter.Movement.isActiveAndEnabled ||
            !selectedCharacter.gameObject.activeInHierarchy ||
            !selectedCharacter.Stats.IsAlive || selectedCharacter.IsDying ||
            selectedCharacter.HasStatusEffect<RootEffect>() ||
            !gridSystem.CanCharacterOccupy(selectedCharacter, destination, ignoreSelf: true))
            return null;

        var path = _pathfinding.FindPath(
            selectedCharacter.GridPosition, destination, selectedCharacter);

        if (path == null || path.Count <= 1)
            return null;

        bool explorationMode = gameStateManager != null && gameStateManager.IsExplorationMode;
        if (!explorationMode &&
            !selectedCharacter.Stats.HasEnergy((path.Count - 1) * selectedCharacter.GetMovementCostPerCell()))
            return null;

        // A fall ends movement. A route cannot reach a later destination after it.
        for (int i = 1; i < path.Count - 1; i++)
        {
            foreach (Vector2Int cell in selectedCharacter.GetOccupiedCells(path[i]))
            {
                if (gridSystem.GetCell(cell)?.Type == CellType.Void)
                    return null;
            }
        }

        return path;
    }

    private void UpdatePathPreview()
    {
        if (selectedCharacter == null)
            return;

        Ray ray =
            mainCamera.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                100f,
                gridLayer))
        {
            currentPath.Clear();
            return;
        }

        Vector2Int targetGridPos =
            gridSystem.WorldToGrid(hit.point);

        if (!reachableCells.Contains(targetGridPos))
        {
            currentPath.Clear();
            return;
        }

        if (!movementPaths.TryGetValue(targetGridPos, out var path))
        {
            currentPath.Clear();
            return;
        }

        // Keep the cached path intact; it includes the starting origin.
        currentPath = path.GetRange(1, path.Count - 1);
    }

    private void UpdateDangerousPreviewCells()
    {
        dangerousPreviewCells.Clear();

        foreach (var cell in reachableCells)
        {
            foreach (Vector2Int occupiedCell in selectedCharacter.GetOccupiedCells(cell))
            {
                if (gridSystem.GetCell(occupiedCell)?.Type == CellType.Void)
                {
                    dangerousPreviewCells.Add(cell);
                    break;
                }
            }

            GridCell gridCell = gridSystem.GetCell(cell);

            if (gridCell == null || gridCell.Type == CellType.Void)
                continue;

            foreach (var dir in GetDirections())
            {
                Vector2Int neighbor = cell + dir;

                GridCell neighborCell = gridSystem.GetCell(neighbor);

                if (neighborCell == null)
                    continue;

                if (neighborCell.Type == CellType.Void)
                    dangerousPreviewCells.Add(neighbor);
            }
        }
    }

    private List<Vector2Int> GetDirections()
    {
        return new List<Vector2Int>
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };
    }

    private void ClearAoeTiles()
    {
        foreach (var tile in aoeTiles)
        {
            tilePool.Release(tile);
        }

        aoeTiles.Clear();
    }

    private void ClearTiles()
    {
        foreach (var tile in activeTiles)
        {
            tilePool.Release(tile);
        }

        activeTiles.Clear();
    }

    private void ClearRangeTiles()
    {
        foreach (var tile in rangeTiles)
        {
            tilePool.Release(tile);
        }

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

            GameObject tile = tilePool.Get(
                pathTilePrefab,
                worldPos,
                Quaternion.Euler(90, 0, 0)
            );

            activeTiles.Add(tile);
        }
    }

    private void DrawRangeTiles(GameObject tilePrefab)
    {
        ClearRangeTiles();

        foreach (var cell in reachableCells)
        {
            GridCell gridCell = gridSystem.GetCell(cell);

            if (currentMode == ActionMode.Move &&
                ((gridCell != null && gridCell.Type == CellType.Void) ||
                 dangerousPreviewCells.Contains(cell)))
                continue;

            Vector3 worldPos = gridSystem.GridToWorld(cell);
            worldPos += pathTileOffset;

            GameObject tile = tilePool.Get(
                tilePrefab,
                worldPos,
                Quaternion.Euler(90, 0, 0)
            );

            rangeTiles.Add(tile);
        }

        if (currentMode != ActionMode.Move)
            return;

        foreach (var cell in dangerousPreviewCells)
        {
            Vector3 worldPos = gridSystem.GridToWorld(cell);
            worldPos += pathTileOffset;

            GameObject tile = tilePool.Get(
                dangerRangeTilePrefab,
                worldPos,
                Quaternion.Euler(90, 0, 0)
            );

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

        Ray ray =
            mainCamera.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, gridLayer))
            return;

        Vector2Int targetPos = gridSystem.WorldToGrid(hit.point);

        // Hover feedback is checked live, independently of the cached range.
        if (!skill.CanUse(selectedCharacter, targetPos, gridSystem))
            return;

        foreach (var cell in skill.GetPreviewCells(
            selectedCharacter.GridPosition,
            targetPos))
        {
            if (!gridSystem.IsValidCell(cell))
                continue;

            Vector3 worldPos = gridSystem.GridToWorld(cell);
            worldPos += pathTileOffset;

            GameObject tile = tilePool.Get(
                aoeTilePrefab,
                worldPos,
                Quaternion.Euler(90, 0, 0)
            );

            aoeTiles.Add(tile);
        }
    }

    private void DrawSelfSkillPreview(SkillBase skill)
    {
        ClearAoeTiles();

        if (selectedCharacter == null || skill == null)
            return;

        if (!skill.CanUse(selectedCharacter, selectedCharacter.GridPosition, gridSystem))
            return;

        foreach (var cell in skill.GetPreviewCells(
            selectedCharacter.GridPosition,
            selectedCharacter.GridPosition))
        {
            if (!gridSystem.IsValidCell(cell))
                continue;

            Vector3 worldPos = gridSystem.GridToWorld(cell);
            worldPos += pathTileOffset;

            GameObject tile = tilePool.Get(
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
        BasicAttack,
        SelfSkillConfirm
    }
}
