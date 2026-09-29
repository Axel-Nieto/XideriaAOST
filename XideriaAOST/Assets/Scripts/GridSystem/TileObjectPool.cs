using System.Collections.Generic;
using UnityEngine;

public class TileObjectPool : MonoBehaviour
{
    private class Pool
    {
        public GameObject prefab;
        public Queue<GameObject> available = new();
    }

    private readonly Dictionary<GameObject, Pool> pools = new();
    private readonly Dictionary<GameObject, Pool> instanceToPool = new();

    public GameObject Get(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        Pool pool = GetOrCreatePool(prefab);

        GameObject instance;

        if (pool.available.Count > 0)
        {
            instance = pool.available.Dequeue();
        }
        else
        {
            instance = Instantiate(prefab, transform);

            instanceToPool[instance] = pool;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        return instance;
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
            return;

        if (!instanceToPool.TryGetValue(instance, out Pool pool))
        {
            Debug.LogWarning(
                $"[TILE POOL] {instance.name} no pertenece a este pool."
            );

            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform);

        pool.available.Enqueue(instance);
    }

    private Pool GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out Pool existingPool))
            return existingPool;

        Pool newPool = new Pool
        {
            prefab = prefab
        };

        pools.Add(prefab, newPool);

        return newPool;
    }
}
