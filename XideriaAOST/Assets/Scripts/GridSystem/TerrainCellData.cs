using System.Collections.Generic;
using System;
using UnityEngine;

[Serializable]
public class TerrainCellData
{
    [SerializeField] private Vector2Int position;
    [SerializeField] private CellType type;

    [Header("Special Cell")]
    [SerializeField] private List<string> targetScenes = new();

    public Vector2Int Position => position;
    public CellType Type => type;
    public IReadOnlyList<string> TargetScenes => targetScenes;
}