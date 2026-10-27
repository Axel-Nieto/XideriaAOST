using System.Collections.Generic;
using UnityEngine;

public class GridSystem : MonoBehaviour
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private float cellSize;
    [SerializeField] private List<TerrainCellData> terrainCells = new();
    [SerializeField] private float yOffset = 0.5f;

    private Dictionary<Vector2Int, GridCell> _grid = new();

    private Dictionary<Vector2Int, Character> _characters = new();
    
    [SerializeField] private GameObject exitRunTilePrefab;
    [SerializeField] private GameObject exitTutorialTilePrefab;
    [SerializeField] private GameObject shopTilePrefab;

    [Header("Door room rewards (configure on each source scene)")]
    [Tooltip("Explicit allowlist. Leave Lobby, Tutorial and scenes without combat out of this list.")]
    [SerializeField] private List<string> combatRewardScenes = new();
    [SerializeField] private DoorRewardSettings doorRewards = new();
    [SerializeField] private Sprite goldRewardSprite;
    [SerializeField] private Sprite xideriaFragmentsRewardSprite;
    [Tooltip("Position relative to a door's Reward Icon Anchor. Without an anchor, world offset from the door center.")]
    [SerializeField] private Vector3 rewardIconOffset = new(0f, 2f, 0f);
    [Tooltip("Fixed rotation relative to a door's Reward Icon Anchor, or world angles without an anchor. Never follows the camera.")]
    [SerializeField] private Vector3 rewardIconRotationOffset;
    [SerializeField, Min(0.01f)] private float rewardIconWorldSize = 0.75f;
    [SerializeField] private int rewardIconSortingOrder = 100;

    private List<LogicalDoor> logicalDoors = new();
    private readonly Dictionary<Vector2Int, LogicalDoor> doorsByCell = new();
    private readonly HashSet<string> reportedDoorErrors = new();
    private readonly System.Random doorRandom = new();

    private readonly List<GameObject> activeSpecialCellVisuals = new();
    public int Width => width;
    public int Height => height;

    private void Awake()
    {
        GenerateGrid();
        logicalDoors = LogicalDoor.Build(terrainCells);
        foreach (LogicalDoor door in logicalDoors)
            foreach (Vector2Int cell in door.Cells)
                doorsByCell[cell] = door;
    }

    private void GenerateGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var pos = new Vector2Int(x, y);
                _grid[pos] = new GridCell(pos, CellType.Ground);
            }
        }

        ApplyTerrainOverrides();
    }

    public void SetCellType(Vector2Int pos, CellType type)
    {
        GridCell cell = GetCell(pos);

        if (cell == null)
            return;

        cell.SetType(type);
    }

    private void ApplyTerrainOverrides()
    {
        foreach (var terrainCell in terrainCells)
        {
            SetCellType(terrainCell.Position, terrainCell.Type);
        }
    }

    public bool IsValidCell(Vector2Int pos)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x < width && pos.y < height;
    }

    public GridCell GetCell(Vector2Int pos)
    {
        return _grid.TryGetValue(pos, out var cell) ? cell : null;
    }

    public bool IsWalkable(Vector2Int pos)
    {
        var cell = GetCell(pos);

        return cell != null && cell.Walkable;
    }

    public bool IsOccupied(Vector2Int pos)
    {
        return _characters.ContainsKey(pos);
    }

    public bool IsBlockedForMovement(Vector2Int pos)
    {
        if (!IsValidCell(pos))
            return true;

        if (!IsWalkable(pos))
            return true;

        if (IsOccupied(pos))
            return true;

        if (HasHelmetAt(pos))
            return true;

        return false;
    }

    public void SetOccupied(Vector2Int pos, bool occupied)
    {
        if (!IsValidCell(pos)) return;

        if (occupied)
        {
            // se setea desde RegisterCharacter
            return;
        }
        else
        {
            _characters.Remove(pos);
        }
    }

    public bool HasHelmetAt(Vector2Int pos)
    {
        HelmetPickup[] helmets = FindObjectsOfType<HelmetPickup>();

        foreach (HelmetPickup helmet in helmets)
        {
            if (helmet == null)
                continue;

            if (helmet.IsCollected)
                continue;

            if (helmet.GridPosition == pos)
                return true;
        }

        return false;
    }

    public void RegisterCharacter(Vector2Int pos, Character character)
    {
        if (!IsValidCell(pos)) return;
        _characters[pos] = character;
    }

    public bool IsSafeForAI(Vector2Int pos)
    {
        GridCell cell = GetCell(pos);

        if (cell == null)
            return false;

        if (!cell.Walkable)
            return false;

        if (cell.Type == CellType.Void)
            return false;

        return true;
    }

    public void UnregisterCharacter(Vector2Int pos)
    {
        if (_characters.ContainsKey(pos))
            _characters.Remove(pos);
    }

    public Character GetCharacterAt(Vector2Int pos)
    {
        return _characters.TryGetValue(pos, out var character)
            ? character
            : null;
    }

    public void RegisterCharacter(Character character)
    {
        if (character == null)
            return;

        foreach (Vector2Int pos in character.GetOccupiedCells())
        {
            if (!IsValidCell(pos))
                continue;

            _characters[pos] = character;
        }
    }

    public void UnregisterCharacter(Character character)
    {
        if (character == null)
            return;

        List<Vector2Int> positionsToRemove = new();

        foreach (var pair in _characters)
        {
            if (pair.Value == character)
                positionsToRemove.Add(pair.Key);
        }

        foreach (Vector2Int pos in positionsToRemove)
        {
            _characters.Remove(pos);
        }
    }


    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 localPos = worldPos - transform.position;

        int x = Mathf.FloorToInt(localPos.x / cellSize);
        int y = Mathf.FloorToInt(localPos.z / cellSize);

        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return transform.position + new Vector3(
            gridPos.x * cellSize + cellSize / 2f,
            yOffset,
            gridPos.y * cellSize + cellSize / 2f
        );
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return GridToWorld(new Vector2Int(x, y));
    }

    private IEnumerable<GridCell> GetAllCells()
    {
        return _grid.Values;
    }

    private void ClearSpecialCellVisuals()
    {
        foreach (var visual in activeSpecialCellVisuals)
        {
            if (visual != null)
                Destroy(visual);
        }

        activeSpecialCellVisuals.Clear();
    }

    public Vector3 GridToWorld(
    Vector2Int gridPos,
    Character character)
    {
        Vector3 basePosition = GridToWorld(gridPos);

        if (character == null)
            return basePosition;

        float offsetX =
            (character.GridWidth - 1) * cellSize * 0.5f;

        float offsetZ =
            (character.GridHeight - 1) * cellSize * 0.5f;

        return basePosition + new Vector3(
            offsetX,
            0f,
            offsetZ
        );
    }

    public void SetSpecialCellsVisible(bool visible)
    {
        ClearSpecialCellVisuals();

        if (!visible)
            return;

        PrepareDoorOffers();

        foreach (var cell in GetAllCells())
        {
            if (doorsByCell.TryGetValue(cell.Position, out LogicalDoor door) &&
                !TryGetPreparedOffer(door, out _))
                continue;

            GameObject prefab = GetSpecialCellPrefab(cell.Type);

            if (prefab == null)
                continue;

            Vector3 position = GridToWorld(cell.Position);

            GameObject visual = Instantiate(
                prefab,
                position,
                Quaternion.Euler(90f, 0f, 0f)
            );

            activeSpecialCellVisuals.Add(visual);
        }

        foreach (LogicalDoor door in logicalDoors)
        {
            if (!TryGetPreparedOffer(door, out RunRewardRecord offer) || !offer.HasReward)
                continue;

            Vector3 center = Vector3.zero;
            foreach (Vector2Int cell in door.Cells)
                center += GridToWorld(cell);
            center /= door.Cells.Count;

            var iconObject = new GameObject($"Door reward {door.Id}");
            iconObject.AddComponent<DoorRewardIcon>().Initialize(GetRewardSprite(offer.Currency),
                rewardIconWorldSize, rewardIconRotationOffset, rewardIconSortingOrder);
            if (door.RewardIconAnchor != null)
            {
                Transform anchor = door.RewardIconAnchor;
                iconObject.transform.position = anchor.TransformPoint(rewardIconOffset);
                iconObject.transform.rotation = anchor.rotation * Quaternion.Euler(rewardIconRotationOffset);
                iconObject.transform.SetParent(anchor, true);
            }
            else
            {
                iconObject.transform.position = center + rewardIconOffset;
                iconObject.transform.SetParent(transform, true);
            }
            activeSpecialCellVisuals.Add(iconObject);
        }
    }

    private GameObject GetSpecialCellPrefab(CellType type)
    {
        return type switch
        {
            CellType.ExitRun => exitRunTilePrefab,
            CellType.ExitTutorial => exitTutorialTilePrefab,
            CellType.Shop => shopTilePrefab,
            _ => null
        };
    }

    public string GetTargetScene(Vector2Int position)
    {
        // Read only: all randomness happens in PrepareDoorOffers before visuals appear.
        return doorsByCell.TryGetValue(position, out LogicalDoor door) &&
            TryGetPreparedOffer(door, out RunRewardRecord offer) ? offer.DestinationScene : null;
    }

    public void PrepareDoorOffers()
    {
        PlayerSessionData session = PlayerSessionData.Instance;
        if (session == null)
        {
            ReportDoorError("session", "Door offers require the persistent PlayerSessionData (start from Lobby or Tutorial).");
            return;
        }

        RunRoomRecord source = session.RunState.FindRoom(session.RunState.CurrentRoomId);
        if (source == null || source.SceneId != gameObject.scene.name)
            source = session.RunState.EnterScene(gameObject.scene.name);
        foreach (LogicalDoor door in logicalDoors)
        {
            if (!door.IsValid)
            {
                ReportDoorError(door.Id, "Cells sharing a door must have identical types and destinations, and at most one shared reward icon anchor.");
                continue;
            }

            if (session.RunState.FindDoorOffer(source.RoomId, door.Id) == null && !CanLoadDoorTargets(door))
                continue;

            IReadOnlyList<string> combatScenes = door.Type == CellType.ExitRun ? combatRewardScenes : null;
            if (!session.RunState.TryPrepareDoorOffer(source.RoomId, door.Id, door.TargetScenes,
                combatScenes, doorRewards, doorRandom.NextDouble, out _, out string error))
                ReportDoorError(door.Id, error);
        }
    }

    private bool CanLoadDoorTargets(LogicalDoor door)
    {
        if (door.TargetScenes == null || door.TargetScenes.Count == 0)
        {
            ReportDoorError(door.Id, "Configure at least one destination scene.");
            return false;
        }
        foreach (string target in door.TargetScenes)
            if (string.IsNullOrWhiteSpace(target) || !Application.CanStreamedLevelBeLoaded(target))
            {
                ReportDoorError(door.Id, $"Destination '{target}' must be present and enabled in the build scene list.");
                return false;
            }
        return true;
    }

    public bool TryGetDoorOffer(Character character, out RunRewardRecord offer)
    {
        offer = null;
        return character != null && LogicalDoor.TryFindTouched(logicalDoors,
            character.GetOccupiedCells(character.GridPosition), out LogicalDoor door) &&
            TryGetPreparedOffer(door, out offer);
    }

    private bool TryGetPreparedOffer(LogicalDoor door, out RunRewardRecord offer)
    {
        offer = null;
        PlayerSessionData session = PlayerSessionData.Instance;
        if (session == null || !door.IsValid)
            return false;
        RunRoomRecord source = session.RunState.FindRoom(session.RunState.CurrentRoomId);
        if (source == null || source.SceneId != gameObject.scene.name)
            return false;
        offer = session.RunState.FindDoorOffer(source.RoomId, door.Id);
        if (offer == null)
            return false;
        if (offer.HasReward && (GetRewardSprite(offer.Currency) == null ||
            rewardIconWorldSize <= 0 || float.IsNaN(rewardIconWorldSize) || float.IsInfinity(rewardIconWorldSize)))
        {
            ReportDoorError(door.Id + ":icon", "Assign the announced currency sprite and a positive finite icon size. The reward exit stays disabled until configured.");
            return false;
        }
        return true;
    }

    private Sprite GetRewardSprite(RunCurrency currency) =>
        currency == RunCurrency.Gold ? goldRewardSprite : xideriaFragmentsRewardSprite;

    private void ReportDoorError(string id, string error)
    {
        if (reportedDoorErrors.Add(id))
            Debug.LogError($"Door {id}: {error}", this);
    }

    public bool CanCharacterOccupy(
    Character character,
    Vector2Int origin,
    bool ignoreSelf = true)
    {
        if (character == null)
            return false;

        foreach (Vector2Int pos in character.GetOccupiedCells(origin))
        {
            if (!IsValidCell(pos))
                return false;

            if (!IsWalkable(pos))
                return false;

            if (HasHelmetAt(pos))
                return false;

            Character occupant = GetCharacterAt(pos);

            if (occupant != null)
            {
                if (ignoreSelf && occupant == character)
                    continue;

                return false;
            }
        }

        return true;
    }

    private void OnDrawGizmos()
    {
        if (_grid == null || _grid.Count == 0)
            return;

        foreach (var cell in _grid.Values)
        {
            Vector3 worldPos = GridToWorld(cell.Position);

            switch (cell.Type)
            {
                case CellType.Ground:
                    Gizmos.color = Color.white;
                    break;

                case CellType.Water:
                    Gizmos.color = Color.blue;
                    break;

                case CellType.Lava:
                    Gizmos.color = Color.red;
                    break;
                case CellType.Void:
                    Gizmos.color = Color.brown;
                    break;
                case CellType.Blocked:
                    Gizmos.color = Color.violet;
                    break;
            }

            Gizmos.DrawWireCube(
                worldPos,
                new Vector3(cellSize, 0.1f, cellSize)
            );

        }
        //Borrar luego
        if (!Application.isPlaying)
            return;

        foreach (var pair in _characters)
        {
            Character character = pair.Value;

            if (character == null)
                continue;

            // Solo personajes con footprint mayor a 1x1
            if (character.GridWidth == 1 &&
                character.GridHeight == 1)
                continue;

            Vector3 worldPos = GridToWorld(pair.Key);

            Gizmos.color = Color.magenta;

            Gizmos.DrawCube(
                worldPos + Vector3.up * 1.5f,
                new Vector3(
                    cellSize * 0.7f,
                    0.15f,
                    cellSize * 0.7f
                )
            );

            Gizmos.color = Color.yellow;

            Gizmos.DrawLine(
                worldPos,
                worldPos + Vector3.up * 1.5f
            );
        }
    }
}
