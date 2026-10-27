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
    [Tooltip("Optional stable door ID. Use the same ID on every cell of a door. Empty groups adjacent exits with identical destinations.")]
    [SerializeField] private string logicalDoorId;

    [Tooltip("Optional sign/surface transform for the reward sprite. Use the same anchor on a door's cells, or assign it on just one cell.")]
    [SerializeField] private Transform rewardIconAnchor;

    public Vector2Int Position => position;
    public CellType Type => type;
    public IReadOnlyList<string> TargetScenes => targetScenes;
    public string LogicalDoorId => logicalDoorId;
    public Transform RewardIconAnchor => rewardIconAnchor;
}
