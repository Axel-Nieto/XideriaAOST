using UnityEngine;

public class HelmetPickup : MonoBehaviour
{
    [SerializeField] private HelmetType helmetType;
    [SerializeField] private Vector2Int startGridPosition;
    [SerializeField] private Vector3 visualOffset;
    [SerializeField] private GameObject helmetVisualPrefab;

    private GridSystem _grid;
    private HelmetData _helmetData;

    public Vector2Int GridPosition { get; private set; }

    private void Awake()
    {
        _grid = FindObjectOfType<GridSystem>();
        _helmetData = CreateHelmet(helmetType);
        _helmetData.HelmetVisualPrefab = helmetVisualPrefab;

        GridPosition = startGridPosition;

        if (_grid != null)
        {
            transform.position = _grid.GridToWorld(startGridPosition) + visualOffset;
        }
    }

    public HelmetData GetHelmet()
    {
        return _helmetData;
    }

    private HelmetData CreateHelmet(HelmetType type)
    {
        switch (type)
        {
            case HelmetType.Fire:
                return HelmetFactory.CreateFireHelmet();
            case HelmetType.Water:
                return HelmetFactory.CreateWaterHelmet();
            default:
                return HelmetFactory.CreateDefaultHelmet();
        }
    }

    public void SetGridPosition(Vector2Int gridPosition)
    {
        GridPosition = gridPosition;

        if (_grid == null)
            _grid = FindObjectOfType<GridSystem>();

        if (_grid != null)
        {
            transform.position =
                _grid.GridToWorld(GridPosition) + visualOffset;
        }
    }
}

public enum HelmetType
{
    Default,
    Fire,
    Water
}
