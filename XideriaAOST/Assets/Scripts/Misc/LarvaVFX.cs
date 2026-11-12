using System.Collections;
using UnityEngine;

// Effects stay in world space: turning, moving or disabling the larva must not drag them.
[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
public sealed class LarvaVFX : MonoBehaviour
{
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private GameObject specialEffectPrefab;
    [SerializeField, Min(0.01f)] private float specialEffectDuration = 1.5f;
    [SerializeField] private float specialHeight = 0.3f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 10f;
    [SerializeField] private float projectileHeight = 0.7f;
    private bool deathEffectPlayed;

    private void OnEnable() => deathEffectPlayed = false;

    public void PlayDeathBurst(GridSystem grid)
    {
        if (deathEffectPlayed || deathEffectPrefab == null) return;
        deathEffectPlayed = true;
        Character owner = GetComponent<Character>();
        float size = CellSize(grid, owner.GridPosition);
        Vector3 center = grid != null ? grid.GridToWorld(owner.GridPosition, owner) + owner.VisualOffset : transform.position;
        GameObject effect = CreateEffect(deathEffectPrefab, center + Vector3.up * (0.5f * size),
            Quaternion.identity, size * Mathf.Max(owner.GridWidth, owner.GridHeight), true);
        float lifetime = 0f;
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            lifetime = Mathf.Max(lifetime, main.duration + main.startDelay.constantMax + main.startLifetime.constantMax);
        }
        Destroy(effect, Mathf.Max(1f, lifetime) + 0.1f);
    }

    public void PlaySpecial(Character target, GridSystem grid, bool cardinal = false)
    {
        if (specialEffectPrefab == null || target == null || grid == null) return;
        Character owner = GetComponent<Character>();
        Vector3 direction = Direction(owner, target, grid, cardinal);
        if (direction.sqrMagnitude < 0.001f) return;
        float size = CellSize(grid, owner.GridPosition);
        Vector3 position = FrontPosition(owner, grid, direction) + Vector3.up * specialHeight;
        GameObject effect = CreateEffect(specialEffectPrefab, position, Quaternion.LookRotation(direction), size);
        Destroy(effect, specialEffectDuration);
    }

    private static Vector3 FrontPosition(Character owner, GridSystem grid, Vector3 direction)
    {
        float size = CellSize(grid, owner.GridPosition);
        // Intersect the facing ray with the whole footprint, then advance half a tile.
        float edge = Mathf.Min(Mathf.Abs(direction.x) > 0.001f ? owner.GridWidth * 0.5f / Mathf.Abs(direction.x) : float.PositiveInfinity,
            Mathf.Abs(direction.z) > 0.001f ? owner.GridHeight * 0.5f / Mathf.Abs(direction.z) : float.PositiveInfinity);
        return grid.GridToWorld(owner.GridPosition, owner) + direction * ((edge + 0.5f) * size);
    }

    public IEnumerator PlayProjectile(Character target, GridSystem grid)
    {
        if (specialEffectPrefab == null || target == null || grid == null) yield break;
        Character owner = GetComponent<Character>();
        float size = CellSize(grid, owner.GridPosition);
        Vector3 start = grid.GridToWorld(owner.GridPosition, owner) + owner.VisualOffset + Vector3.up * (projectileHeight * size);
        Vector3 end = grid.GridToWorld(target.GridPosition, target) + target.VisualOffset + Vector3.up * (projectileHeight * size);
        Vector3 direction = end - start;
        GameObject effect = CreateEffect(specialEffectPrefab, start,
            direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : Quaternion.identity, size);
        // Also own a timeout if the caster is disabled before its coroutine is disposed.
        Destroy(effect, Vector3.Distance(start, end) / projectileSpeed + 2f);
        try
        {
            while (effect != null && Vector3.Distance(effect.transform.position, end) > 0.05f)
            {
                effect.transform.position = Vector3.MoveTowards(effect.transform.position, end, projectileSpeed * Time.deltaTime);
                yield return null;
            }
        }
        finally
        {
            if (effect != null) Destroy(effect);
        }
    }

    private static float CellSize(GridSystem grid, Vector2Int origin) => grid != null
        ? Vector3.Distance(grid.GridToWorld(origin), grid.GridToWorld(origin + Vector2Int.right)) : 1f;

    private static Vector3 Direction(Character owner, Character target, GridSystem grid, bool cardinal)
    {
        if (cardinal)
        {
            // Air's rule allows aligned occupied cells even when the two centers are offset.
            Vector2Int closest = Vector2Int.zero;
            int distance = int.MaxValue;
            foreach (Vector2Int source in owner.GetOccupiedCells())
                foreach (Vector2Int destination in target.GetOccupiedCells())
                {
                    Vector2Int delta = destination - source;
                    int length = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                    if ((delta.x == 0 || delta.y == 0) && length > 0 && length < distance)
                    {
                        closest = delta;
                        distance = length;
                    }
                }
            return new Vector3(closest.x, 0f, closest.y).normalized;
        }
        Vector3 direction = grid.GridToWorld(target.GridPosition, target) - grid.GridToWorld(owner.GridPosition, owner);
        direction.y = 0f;
        return direction.normalized;
    }

    private static GameObject CreateEffect(GameObject prefab, Vector3 position, Quaternion rotation, float scale, bool centerParticles = false)
    {
        // Instantiate inactive so authored playOnAwake cannot emit at an editor staging offset.
        GameObject holder = new GameObject("Larva effect staging");
        holder.SetActive(false);
        GameObject effect = Instantiate(prefab, holder.transform, false);
        effect.transform.SetPositionAndRotation(position, rotation * prefab.transform.rotation);
        effect.transform.localScale = prefab.transform.localScale * scale;
        if (centerParticles)
        {
            ParticleSystem first = effect.GetComponentInChildren<ParticleSystem>(true);
            if (first != null) effect.transform.position += position - first.transform.position;
        }
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.loop = false;
        }
        effect.SetActive(true);
        // Detaching activates the prefab's VFX and sends its authored OnPlay event.
        // Fresh instances must not receive a second initialization immediately afterwards.
        effect.transform.SetParent(null, true);
        Destroy(holder);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>()) particles.Play();
        return effect;
    }
}
