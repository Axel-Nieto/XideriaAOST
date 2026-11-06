using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

// Scene effects are inactive templates. Casts use independent instances so that
// moving or turning the caster cannot drag effects already placed on the board.
[DisallowMultipleComponent]
public sealed class RockSkillVFX : MonoBehaviour
{
    [SerializeField] private Transform shield;
    [SerializeField] private VisualEffect slash;
    [SerializeField] private VisualEffect earthquake;
    [SerializeField] private VisualEffect eruptionRock;
    [Tooltip("Base of the main R mesh in graph space at RockProgress 1.5. Presentation only.")]
    [SerializeField] private Vector3 eruptionRockBaseOffset = new(0f, 0.285f, 0f);
    [SerializeField] private VisualEffect excavate;
    [SerializeField, Min(0.01f)] private float rockRiseDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float rockSinkDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float excavateTravelDelay = 1.1f;
    private GameObject activeShield;
    private Character owner;

    public float RockRiseDuration => rockRiseDuration;
    public float RockSinkDuration => rockSinkDuration;

    private void Awake()
    {
        owner = GetComponent<Character>();
        foreach (GameObject template in new[] { shield != null ? shield.gameObject : null, slash != null ? slash.gameObject : null,
            earthquake != null ? earthquake.gameObject : null, eruptionRock != null ? eruptionRock.gameObject : null,
            excavate != null ? excavate.gameObject : null })
            if (template != null) template.SetActive(false);
    }

    private void LateUpdate()
    {
        if (activeShield == null) return;
        bool shielded = owner != null && owner.Stats != null && owner.Stats.IsAlive && !owner.IsDying;
        bool charges = false;
        if (shielded)
            foreach (PassiveEffect passive in owner.GetPassiveEffects())
                if (passive is RockGuardPassive guard && guard.ShieldCharges > 0) charges = true;
        if (!charges) ClearShield();
    }

    private void OnDisable() => ClearShield();

    private void ClearShield()
    {
        if (activeShield != null) Destroy(activeShield);
        activeShield = null;
    }

    public void PlaySlash() => PlayBurst(slash, 1.3f);
    public void PlayEarthquake() => PlayBurst(earthquake, 2.5f);

    public void PlayShield()
    {
        ClearShield();
        if (shield == null) return;
        activeShield = Instantiate(shield.gameObject, shield.parent, false);
        activeShield.SetActive(true);
        foreach (ParticleSystem particles in activeShield.GetComponentsInChildren<ParticleSystem>())
            particles.Play();
    }

    private static void PlayBurst(VisualEffect template, float duration)
    {
        if (template == null) return;
        VisualEffect effect = Instantiate(template, template.transform.position, template.transform.rotation);
        effect.transform.localScale = template.transform.lossyScale;
        effect.gameObject.SetActive(true);
        effect.Reinit();
        Destroy(effect.gameObject, duration);
    }

    public VisualEffect CreateEruptionRock(Vector3 surface, float cellSize, Transform parent)
    {
        if (eruptionRock == null || !eruptionRock.HasFloat("RockProgress")) return null;
        VisualEffect effect = Instantiate(eruptionRock);
        PositionEruptionRock(effect.transform, surface, cellSize, parent);
        effect.SetFloat("RockProgress", 0f);
        effect.gameObject.SetActive(true);
        effect.Reinit();
        return effect;
    }

    private void PositionEruptionRock(Transform effect, Vector3 surface, float cellSize, Transform parent)
    {
        // Keep world size independent of the grid/template hierarchy. The graph's
        // resting base is y=1.5 + (-1 - 1.7) * (0.3 * 1.5) = 0.285,
        // so its origin cannot be placed directly on the tile surface.
        effect.SetParent(null, false);
        effect.SetPositionAndRotation(surface, Quaternion.identity);
        effect.localScale = Vector3.one * (cellSize * 0.8f);
        effect.SetParent(parent, true);
        effect.position -= effect.TransformVector(eruptionRockBaseOffset);
    }

    public IEnumerator PlayExcavate(Character user, GridSystem grid, Vector2Int origin, Vector2Int direction)
    {
        if (excavate == null) yield break;
        float cellSize = Vector3.Distance(grid.GridToWorld(origin), grid.GridToWorld(origin + Vector2Int.right));
        int depth = direction.x < 0 || direction.y < 0 ? 1 : (direction.x != 0 ? user.GridWidth : user.GridHeight);
        Vector2Int first = origin + direction * depth;
        Vector3 firstCenter = grid.GridToWorld(first);
        if (direction.x != 0) firstCenter.z += (user.GridHeight - 1) * cellSize * 0.5f;
        else firstCenter.x += (user.GridWidth - 1) * cellSize * 0.5f;
        // The graph advances along -Z, at 0, -1, -2.1, and is authored 1.9 units high.
        float scale = cellSize / 1.05f;
        Vector3 position = firstCenter + Vector3.up * (0.3f - 1.9f * scale);
        VisualEffect effect = Instantiate(excavate, position,
            Quaternion.LookRotation(new Vector3(-direction.x, 0f, -direction.y)));
        effect.transform.localScale = Vector3.one * scale;
        for (int index = 1; index <= 3; index++)
        {
            bool visible = CanShowExcavateRock(user, grid, origin, direction, index);
            SetFloat(effect, "Rock-" + index, visible ? 1f : 0f);
            SetFloat(effect, "Rock-" + index + " B", visible ? 10f : 0f);
        }
        effect.gameObject.SetActive(true);
        effect.Reinit();
        // Own the timeout even if the skill coroutine is interrupted.
        // The final debris burst starts at 1.1s and lives for 3.2s.
        Destroy(effect.gameObject, 4.5f);
        yield return new WaitForSeconds(excavateTravelDelay);
    }

    public static bool CanShowExcavateRock(Character user, GridSystem grid, Vector2Int origin, Vector2Int direction, int index)
    {
        int depth = direction.x != 0 ? user.GridWidth : user.GridHeight;
        Vector2Int first = origin + direction * (depth + index - 1);
        // Negative directions leave the footprint immediately, regardless of its depth.
        if (direction.x < 0 || direction.y < 0) first = origin + direction * index;
        int breadth = direction.x != 0 ? user.GridHeight : user.GridWidth;
        Vector2Int side = direction.x != 0 ? Vector2Int.up : Vector2Int.right;
        for (int i = 0; i < breadth; i++)
        {
            Vector2Int cell = first + side * i;
            GridCell terrain = grid.GetCell(cell);
            Character occupant = grid.GetCharacterAt(cell);
            if (terrain == null || !terrain.Walkable || terrain.Type == CellType.Void ||
                (occupant != null && occupant != user) || grid.HasHelmetAt(cell)) return false;
        }
        return true;
    }

    private static void SetFloat(VisualEffect effect, string property, float value)
    {
        if (effect.HasFloat(property)) effect.SetFloat(property, value);
    }
}
