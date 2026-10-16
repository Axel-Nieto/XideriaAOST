using UnityEngine;

/// <summary>Presentation only: a fixed sprite on a surface, independent of the camera and logical cells.</summary>
public sealed class DoorRewardIcon : MonoBehaviour
{
    public void Initialize(Sprite sprite, float worldSize, Vector3 eulerAngles, int sortingOrder)
    {
        // Initialize before parenting: size is in world units; the parent then carries the sprite.
        transform.rotation = Quaternion.Euler(eulerAngles);
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        transform.localScale = Vector3.one * (worldSize / Mathf.Max(spriteSize, 0.001f));
    }
}
