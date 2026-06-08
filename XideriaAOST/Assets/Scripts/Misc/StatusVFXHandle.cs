using UnityEngine;

public class StatusVFXHandle : MonoBehaviour
{
    private GameObject instance;

    public void Attach(GameObject prefab, Transform anchor, Vector3 worldOffset)
    {
        if (prefab == null || anchor == null)
            return;

        Clear();

        instance = Instantiate(
            prefab,
            anchor.position + worldOffset,
            Quaternion.identity
        );

        instance.transform.SetParent(anchor, worldPositionStays: true);
    }

    public void Clear()
    {
        if (instance != null)
        {
            Destroy(instance);
            instance = null;
        }
    }
}
