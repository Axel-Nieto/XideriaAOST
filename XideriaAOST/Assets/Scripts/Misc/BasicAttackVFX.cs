using System.Collections;
using UnityEngine;

public static class BasicAttackVFX
{
    public static IEnumerator PlayFire(
        Character attacker,
        Character target,
        GridSystem grid,
        float hitDelay)
    {
        return PlayOriented(attacker, target, grid, DamageType.Fire, "SlashSkill/FireHit", hitDelay);
    }

    public static IEnumerator PlayWater(
        Character attacker,
        Character target,
        GridSystem grid,
        float hitDelay)
    {
        return PlayOriented(attacker, target, grid, DamageType.Water,
            "SlashSkill/WaterHitBuble/WaterHit", hitDelay);
    }

    private static IEnumerator PlayOriented(
        Character attacker,
        Character target,
        GridSystem grid,
        DamageType damageType,
        string impactPath,
        float hitDelay)
    {
        if (SkillVFXLibrary.Instance == null || attacker == null || target == null || grid == null)
            yield break;

        GameObject slashPrefab = SkillVFXLibrary.Instance.GetBasicSlash(damageType);
        if (slashPrefab == null)
        {
            if (hitDelay > 0f)
                yield return new WaitForSeconds(hitDelay);
            yield break;
        }

        // GridToWorld's character overload returns the footprint's center,
        // regardless of model rotation or which occupied cell was clicked.
        Vector3 targetCenter = grid.GridToWorld(target.GridPosition, target);
        Vector3 attackerCenter = grid.GridToWorld(attacker.GridPosition, attacker);
        Camera camera = Camera.main;
        Vector3 screenRight = camera != null ? camera.transform.right : Vector3.right;
        float attackSide = Vector3.Dot(targetCenter - attackerCenter, screenRight);

        if (damageType == DamageType.Water)
        {
            GameObject waterInstance = SpawnWaterSlash(slashPrefab, targetCenter, screenRight, attackSide);
            Object.Destroy(waterInstance, 3f);

            if (hitDelay > 0f)
                yield return new WaitForSeconds(hitDelay);
            yield break;
        }

        // Preserve the existing three-dimensional turn for the fire effect.
        Transform slash = slashPrefab.transform.Find("SlashSkill");
        Vector3 authoredRight = slash != null ? slash.right : slashPrefab.transform.right;
        float authoredSide = Vector3.Dot(authoredRight, screenRight);

        Quaternion rotation = slashPrefab.transform.rotation;
        if (Mathf.Abs(attackSide) > 0.001f && attackSide * authoredSide < 0f)
            rotation = Quaternion.AngleAxis(180f, Vector3.up) * rotation;

        // Each effect's root is offset from its actual impact. Align the authored
        // hit point, rotating that offset together with the axe when it flips.
        Vector3 position = targetCenter;
        Transform impact = slashPrefab.transform.Find(impactPath);
        if (impact != null)
        {
            Vector3 localImpact = slashPrefab.transform.InverseTransformPoint(impact.position);
            position -= rotation * Vector3.Scale(localImpact, slashPrefab.transform.localScale);
        }

        GameObject instance = SkillVFXSpawner.SpawnAtWorldInstance(slashPrefab, position, rotation);
        if (instance != null)
            Object.Destroy(instance, 3f);

        if (hitDelay > 0f)
            yield return new WaitForSeconds(hitDelay);
    }

    private static GameObject SpawnWaterSlash(
        GameObject prefab,
        Vector3 targetCenter,
        Vector3 screenRight,
        float attackSide)
    {
        // Configure the hierarchy before playOnAwake can emit any particles.
        GameObject root = new GameObject("WaterSlashVFX");
        root.SetActive(false);
        root.transform.position = targetCenter;

        Vector3 right = Vector3.ProjectOnPlane(screenRight, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;

        Transform mirror = new GameObject("AxeMirror").transform;
        mirror.SetParent(root.transform, false);
        mirror.rotation = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);

        GameObject instance = Object.Instantiate(prefab, mirror);
        instance.transform.SetPositionAndRotation(targetCenter, prefab.transform.rotation);

        Transform slash = instance.transform.Find("SlashSkill");
        Transform splash = instance.transform.Find("SlashSkill/WaterHitBuble");
        Transform impact = instance.transform.Find("SlashSkill/WaterHitBuble/WaterHit");
        if (impact != null)
            instance.transform.position += targetCenter - impact.position;

        // The emitter starts to one side of the hit. This describes where the
        // swing comes from; the mesh's local right vector does not.
        float authoredApproach = slash != null
            ? Vector3.Dot(targetCenter - slash.position, right)
            : 1f;

        if (splash != null)
        {
            // Keep the hit independent of the mirrored axe. Its particle
            // systems use Local scaling, so preserve their authored scale.
            Vector3 splashScale = splash.localScale;
            splash.SetParent(root.transform, true);
            splash.localScale = splashScale;
        }

        if (slash != null && slash.TryGetComponent(out ParticleSystem particles))
        {
            var main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        // Mirror in screen space instead of turning the mesh away from the
        // camera. Hierarchy scaling makes the particles inherit the reflection.
        if (Mathf.Abs(attackSide) > 0.001f && attackSide * authoredApproach < 0f)
            mirror.localScale = new Vector3(-1f, 1f, 1f);

        root.SetActive(true);
        return root;
    }

    public static IEnumerator Play(
        Character target,
        DamageType damageType,
        Vector3 positionOffset,
        float hitDelay)
    {
        if (SkillVFXLibrary.Instance == null || target == null)
            yield break;

        Transform anchor = target.GetGroundAnchor();

        GameObject slashPrefab =
            SkillVFXLibrary.Instance.GetBasicSlash(damageType);

        SkillVFXSpawner.SpawnSlashAtWorld(
            slashPrefab,
            anchor.position,
            positionOffset,
            destroyDelay: 3f
        );

        if (hitDelay > 0f)
            yield return new WaitForSeconds(hitDelay);
    }
}
