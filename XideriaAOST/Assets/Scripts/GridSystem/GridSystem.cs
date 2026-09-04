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

    private readonly List<GameObject> activeSpecialCellVisuals = new();
    public int Width => width;
    public int Height => height;

    private void Awake()
    {
        GenerateGrid();
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

        foreach (var helmet in helmets)
        {
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

    public void SetSpecialCellsVisible(bool visible)
    {
        ClearSpecialCellVisuals();

        if (!visible)
            return;

        foreach (var cell in GetAllCells())
        {
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
        foreach (var terrainCell in terrainCells)
        {
            if (terrainCell.Position != position)
                continue;

            var targetScenes = terrainCell.TargetScenes;

            if (targetScenes == null || targetScenes.Count == 0)
            {
                Debug.LogWarning(
                    $"La celda {position} no tiene escenas destino configuradas."
                );

                return null;
            }

            if (targetScenes.Count == 1)
                return targetScenes[0];

            int randomIndex = Random.Range(0, targetScenes.Count);

            return targetScenes[randomIndex];
        }

        return null;
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
    }
}