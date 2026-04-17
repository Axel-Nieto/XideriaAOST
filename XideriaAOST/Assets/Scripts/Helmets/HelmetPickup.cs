using UnityEngine;

public class HelmetPickup : MonoBehaviour
{
    [SerializeField] private HelmetType helmetType;
    [SerializeField] private Vector2Int startGridPosition;
    [SerializeField] private float visualYOffset = 0.1f;

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
            transform.position = _grid.GridToWorld(startGridPosition) + Vector3.up * visualYOffset;
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

            default:
                return HelmetFactory.CreateDefaultHelmet();
        }
    }
}

public enum HelmetType
{
    Default,
    Fire
}
