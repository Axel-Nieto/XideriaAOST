using UnityEngine;

public class GridHighlighter : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private GameObject highlightPrefab;

    private GameObject currentHighlight;
    [SerializeField] private float heightOffset = 0.5f;

    public void SetHighlight(Vector2Int gridPos)
    {
        if (!gridSystem.IsValidCell(gridPos))
        {
            currentHighlight.SetActive(false);
            return;
        }

        currentHighlight.SetActive(true);

        Vector3 pos = gridSystem.GridToWorld(gridPos);
        pos.y += heightOffset;

        currentHighlight.transform.position = pos;
    }
}