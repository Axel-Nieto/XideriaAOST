using UnityEngine;

public class HelmetPickup : MonoBehaviour
{
    [SerializeField] private HelmetType helmetType;
    [SerializeField] private Vector2Int startGridPosition;
    [SerializeField] private Vector3 visualOffset;
    [SerializeField] private GameObject rockObstaclePrefab;

    private GridSystem _grid;
    private HelmetData _helmetData;

    public Vector2Int GridPosition { get; private set; }
    public HelmetType Type => helmetType;

    private bool _collected;

    public bool IsCollected => _collected;

    public void MarkAsCollected()
    {
        _collected = true;
    }

    private void Awake()
    {
        if (_grid == null)
            _grid = FindObjectOfType<GridSystem>();

        if (HelmetCatalog.Instance != null)
        {
            _helmetData =
                HelmetCatalog.Instance.CreateHelmet(helmetType);
        }
        else
        {
            Debug.LogError(
                $"[HELMET PICKUP] HelmetCatalog.Instance es NULL en {name}."
            );

            _helmetData = CreateHelmet(helmetType);
        }

        GridPosition = startGridPosition;

        if (_grid != null)
        {
            transform.position =
                _grid.GridToWorld(startGridPosition) + visualOffset;
        }
    }

    public HelmetData GetHelmet()
    {
        return _helmetData;
    }

    // Configure an inactive replacement before Awake so the prefab's authored
    // cell and offset never replace the location chosen by the map.
    internal void ConfigureSceneSpawn(HelmetPickup spawnPoint)
    {
        if (gameObject.activeInHierarchy || _helmetData != null)
            throw new System.InvalidOperationException("Configure the helmet before activation.");

        _grid = spawnPoint._grid;
        startGridPosition = spawnPoint.GridPosition;
        GridPosition = startGridPosition;
        visualOffset = spawnPoint.visualOffset;
        transform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);
    }

    private HelmetData CreateHelmet(HelmetType type)
    {
        switch (type)
        {
            case HelmetType.Fire:
                return HelmetFactory.CreateFireHelmet();
            case HelmetType.Water:
                return HelmetFactory.CreateWaterHelmet();
            case HelmetType.Rock:
                return HelmetFactory.CreateRockHelmet(rockObstaclePrefab);
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
    Water,
    Rock = 3
}
