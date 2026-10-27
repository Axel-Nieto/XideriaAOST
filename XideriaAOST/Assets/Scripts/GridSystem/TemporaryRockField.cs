using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TemporaryRockField : MonoBehaviour
{
    private sealed class Rock
    {
        public Character Owner;
        public Vector2Int Cell;
        public CellType PreviousType;
        public int Turns;
        public GameObject Visual;
    }
    private readonly List<Rock> rocks = new();
    // Map art sits above the logical grid plane (about 0.25 in Zone 1).
    [SerializeField] private float visualSurfaceOffset = 0.3f;
    private GridSystem grid;
    public int Count => rocks.Count;
    private void Awake() => grid = GetComponent<GridSystem>();
    private void OnEnable()
    {
        GameEvents.OnTurnStarted += Tick;
        GameEvents.OnCharacterDeathCompleted += ClearOwner;
    }
    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= Tick;
        GameEvents.OnCharacterDeathCompleted -= ClearOwner;
        ClearAll();
    }
    public static TemporaryRockField For(GridSystem grid)
    {
        var field = grid.GetComponent<TemporaryRockField>();
        return field != null ? field : grid.gameObject.AddComponent<TemporaryRockField>();
    }
    public bool CanPlace(Vector2Int cell)
    {
        if (grid == null) grid = GetComponent<GridSystem>();
        GridCell terrain = grid != null ? grid.GetCell(cell) : null;
        return terrain != null && (terrain.Type == CellType.Ground || terrain.Type == CellType.Water || terrain.Type == CellType.Lava) &&
            terrain.Walkable && !grid.IsOccupied(cell) && !grid.HasHelmetAt(cell);
    }
    public bool TryPlace(Character owner, Vector2Int cell, GameObject prefab, int duration = 2)
    {
        if (owner == null || prefab == null || duration <= 0 || !CanPlace(cell)) return false;
        GameObject visual = Instantiate(prefab, grid.GridToWorld(cell), Quaternion.identity, transform);
        visual.name = "Temporary Rock " + cell;
        visual.SetActive(true);
        visual.transform.localScale = Vector3.one * 0.2f;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float cellSize = Vector3.Distance(grid.GridToWorld(cell), grid.GridToWorld(cell + Vector2Int.right));
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width > 0f)
            {
                visual.transform.localScale *= cellSize * 0.8f / width;
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
            Vector3 surface = grid.GridToWorld(cell) + Vector3.up * visualSurfaceOffset;
            visual.transform.position += new Vector3(surface.x - bounds.center.x, surface.y - bounds.min.y, surface.z - bounds.center.z);
        }
        rocks.Add(new Rock { Owner = owner, Cell = cell, PreviousType = grid.GetCell(cell).Type, Turns = duration, Visual = visual });
        grid.SetCellType(cell, CellType.Blocked);
        return true;
    }
    private void Tick(Character current)
    {
        for (int i = rocks.Count - 1; i >= 0; i--)
        {
            Rock rock = rocks[i];
            if (rock.Owner == null || rock.Owner.Stats == null || !rock.Owner.Stats.IsAlive || rock.Owner.IsDying ||
                (rock.Owner == current && --rock.Turns <= 0)) Remove(i);
        }
    }
    private void ClearOwner(Character owner)
    {
        for (int i = rocks.Count - 1; i >= 0; i--)
            if (rocks[i].Owner == owner) Remove(i);
    }
    public void ClearAll()
    {
        for (int i = rocks.Count - 1; i >= 0; i--) Remove(i);
    }
    private void Remove(int index)
    {
        Rock rock = rocks[index];
        if (grid != null && grid.GetCell(rock.Cell)?.Type == CellType.Blocked)
            grid.SetCellType(rock.Cell, rock.PreviousType);
        if (rock.Visual != null)
        {
            if (Application.isPlaying) Destroy(rock.Visual);
            else DestroyImmediate(rock.Visual);
        }
        rocks.RemoveAt(index);
    }
}
