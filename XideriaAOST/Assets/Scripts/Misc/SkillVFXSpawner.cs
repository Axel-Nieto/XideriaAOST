using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SkillVFXSpawner
{
    public static void SpawnAtWorld(
    GameObject prefab,
    Vector3 position,
    Quaternion rotation,
    float destroyDelay = 3f)
    {
        if (prefab == null)
            return;

        GameObject instance = Object.Instantiate(
            prefab,
            position,
            rotation
        );

        Object.Destroy(instance, destroyDelay);
    }

    public static GameObject SpawnAtWorldInstance(
    GameObject prefab,
    Vector3 position,
    Quaternion rotation)
    {
        if (prefab == null)
            return null;

        return Object.Instantiate(prefab, position, rotation);
    }

    public static void SpawnAtCell(GameObject prefab, GridSystem grid, Vector2Int cell, Vector3 offset)
    {
        if (prefab == null || grid == null)
            return;

        Vector3 worldPos = grid.GridToWorld(cell) + offset;

        Object.Destroy(
            Object.Instantiate(prefab, worldPos, Quaternion.identity),
            1.5f
        );
    }

    public static GameObject SpawnMovingVFX(
    GameObject prefab,
    Vector3 start,
    Vector3 end,
    Quaternion rotation,
    float speed)
    {
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(
            prefab,
            start,
            rotation
        );

        MovingVFX movingVFX = instance.GetComponent<MovingVFX>();

        if (movingVFX != null)
        {
            movingVFX.Initialize(end, speed);
        }

        return instance;
    }

    public static void SpawnAttached(GameObject prefab, Transform parent, Vector3 localOffset)
    {
        if (prefab == null || parent == null)
            return;

        GameObject instance = Object.Instantiate(prefab, parent);
        instance.transform.localPosition = localOffset;
        instance.transform.localRotation = Quaternion.identity;

        Object.Destroy(instance, 1.3f);
    }

    public static IEnumerator SpawnProjectile(
    GameObject projectilePrefab,
    GameObject impactPrefab,
    Vector3 start,
    Vector3 end,
    float speed = 8f,
    float destroyDelay = 1.5f)
    {
        if (projectilePrefab == null)
            yield break;

        GameObject projectile = Object.Instantiate(
            projectilePrefab,
            start,
            Quaternion.identity
        );

        while (Vector3.Distance(projectile.transform.position, end) > 0.05f)
        {
            projectile.transform.position = Vector3.MoveTowards(
                projectile.transform.position,
                end,
                speed * Time.deltaTime
            );

            Vector3 direction = end - projectile.transform.position;

            if (direction != Vector3.zero)
                projectile.transform.forward = direction.normalized;

            yield return null;
        }

        Object.Destroy(projectile);

        if (impactPrefab != null)
        {
            Object.Destroy(
                Object.Instantiate(impactPrefab, end, Quaternion.identity),
                destroyDelay
            );
        }
    }

    public static GameObject SpawnDashTrail(
    GameObject prefab,
    Vector3 start,
    Vector3 end,
    float heightOffset = 0.05f)
    {
        if (prefab == null)
            return null;

        Vector3 direction = end - start;
        float distance = direction.magnitude;

        Vector3 center = (start + end) * 0.5f;
        center.y += heightOffset;

        GameObject instance = Object.Instantiate(
            prefab,
            center,
            Quaternion.identity
        );

        if (direction != Vector3.zero)
            instance.transform.forward = direction.normalized;

        Vector3 scale = instance.transform.localScale;
        scale.z = distance;
        instance.transform.localScale = scale;

        return instance;
    }

    public static GameObject Spawn(
    GameObject prefab,
    Vector3 position,
    Quaternion rotation)
    {
        if (prefab == null)
            return null;

        return Object.Instantiate(prefab, position, rotation);
    }


    //Slash
    public static GameObject SpawnSlashAtWorld(
    GameObject prefab,
    Vector3 position,
    Vector3 offset,
    float destroyDelay = 3f)
    {
        if (prefab == null)
            return null;

        GameObject instance = Object.Instantiate(
            prefab,
            position + offset,
            prefab.transform.rotation
        );

        //instance.transform.localScale = prefab.transform.localScale * 4f;

        Object.Destroy(instance, destroyDelay);

        return instance;
    }
}
