using UnityEngine;

[CreateAssetMenu(menuName = "Grid/Grid Config")]
public class GridConfig : ScriptableObject
{
    public int width = 13;
    public int height = 13;
    public float cellSize = 1f;
}