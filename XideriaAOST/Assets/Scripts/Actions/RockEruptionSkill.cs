using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RockEruptionSkill : RockSkillBase
{
    private readonly GameObject rockPrefab;
    private readonly int rockCount;
    private readonly int pushDistance;
    public override int AreaRadius => 5;
    public RockEruptionSkill(GameObject rockPrefab, int energyCost = 6, int cooldown = 4, int rockCount = 3, int pushDistance = 1)
        : base("Erupción de Tierra", "Golpea y aleja a los enemigos cercanos. Crea hasta tres rocas en casillas libres del área durante dos turnos propios.", energyCost, cooldown,
            icon: SkillIconLibrary.Instance != null ? SkillIconLibrary.Instance.rockEruption : null)
    {
        this.rockPrefab = rockPrefab;
        this.rockCount = Mathf.Max(0, rockCount);
        this.pushDistance = Mathf.Max(0, pushDistance);
    }
    public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid) =>
        Ready(user, grid, user?.Stats?.CurrentEnergy ?? 0) && rockPrefab != null;
    public override bool CanUseFromPosition(Character user, Vector2Int origin, Character target, GridSystem grid, int energy) =>
        Ready(user, grid, energy) && rockPrefab != null && Enemies(user, grid, origin, AreaRadius).Contains(target);
    public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid)
    {
        if (!CanUse(user, targetCell, grid)) yield break;
        Spend(user);
        yield return Cast(user);
        List<Character> targets = Enemies(user, grid, user.GridPosition, AreaRadius);
        foreach (Character target in targets)
        {
            if (target == null || !target.Stats.IsAlive || target.IsDying) continue;
            Damage(user, target);
            if (target.Stats.IsAlive && !target.IsDying)
                yield return PushSystem.Push(user, target, grid, pushDistance);
        }
        TemporaryRockField field = TemporaryRockField.For(grid);
        var cells = new List<Vector2Int>();
        foreach (Vector2Int cell in Area(user.GridPosition, user.GridWidth, user.GridHeight, AreaRadius))
            if (field.CanPlace(cell)) cells.Add(cell);
        int remaining = rockCount;
        while (remaining > 0 && cells.Count > 0)
        {
            int index = Random.Range(0, cells.Count);
            Vector2Int cell = cells[index];
            cells.RemoveAt(index);
            if (field.TryPlace(user, cell, rockPrefab, 2)) remaining--;
        }
        yield return FinishCast(user);
    }
    public override IEnumerable<Vector2Int> GetPreviewCells(Vector2Int origin, Vector2Int targetCell) => Area(origin, 1, 1, AreaRadius);
    public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid) =>
        Area(origin, user.GridWidth, user.GridHeight, AreaRadius);
    public override string GetTooltipDetails(Character user) => $"Daño físico: {user?.Stats?.Attack ?? 0}. Empuje: {pushDistance}. Rocas: hasta {rockCount}, 2 turnos.";
}
