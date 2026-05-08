using UnityEngine;

public class HelmetPickup : MonoBehaviour
{
    [SerializeField] private HelmetType helmetType;
    [SerializeField] private Vector2Int startGridPosition;
    [SerializeField] private Vector3 visualOffset;

    private GridSystem _grid;
    private HelmetData _helmetData;

    public Vector2Int GridPosition { get; private set; }

    private void Awake()
    {
        _grid = FindObjectOfType<GridSystem>();
        _helmetData = CreateHelmet(helmetType);

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
}

public enum HelmetType
{
    Default,
    Fire,
    Water
}
