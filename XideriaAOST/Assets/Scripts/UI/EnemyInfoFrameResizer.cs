using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// MarcoEnemigos has ornaments in the middle of both horizontal edges.
// Five columns preserve those ornaments as well as the four corners.
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
[AddComponentMenu("UI/Enemy Info Frame Resizer")]
public class EnemyInfoFrameResizer : BaseMeshEffect
{
    [SerializeField, Min(1f)]
    [Tooltip("Width of the original square frame in UI units. Controls the size of its decorations.")]
    private float referenceSize = 400f;

    // Cuts in the original 787 x 787 artwork, measured from the bottom left.
    // The narrow flexible strips avoid the skull, spikes and bottom ornament.
    private static readonly float[] SourceX = { 0f, 128f, 144f, 643f, 659f, 787f };
    private static readonly float[] SourceY = { 0f, 128f, 607f, 787f };
    private readonly float[] positionsX = new float[6];
    private readonly float[] positionsY = new float[4];

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || !(graphic is Image image) || image.overrideSprite == null)
            return;

        Rect rect = image.GetPixelAdjustedRect();
        vertices.Clear();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float scale = Mathf.Max(1f, referenceSize) / 787f;
        // Below the space required by the ornaments, shrink every fixed region
        // uniformly so the skull and corners keep their proportions and cannot overlap.
        scale = Mathf.Min(scale, rect.width / 755f, rect.height / 308f);
        float side = 128f * scale;
        float halfCenter = 249.5f * scale;

        positionsX[0] = rect.xMin;
        positionsX[1] = rect.xMin + side;
        positionsX[2] = rect.center.x - halfCenter;
        positionsX[3] = rect.center.x + halfCenter;
        positionsX[4] = rect.xMax - side;
        positionsX[5] = rect.xMax;

        positionsY[0] = rect.yMin;
        positionsY[1] = rect.yMin + 128f * scale;
        positionsY[2] = rect.yMax - 180f * scale;
        positionsY[3] = rect.yMax;

        Vector4 uv = DataUtility.GetOuterUV(image.overrideSprite);
        for (int y = 0; y < SourceY.Length; y++)
        {
            for (int x = 0; x < SourceX.Length; x++)
            {
                vertices.AddVert(
                    new Vector3(positionsX[x], positionsY[y], 0f),
                    image.color,
                    new Vector2(
                        Mathf.Lerp(uv.x, uv.z, SourceX[x] / 787f),
                        Mathf.Lerp(uv.y, uv.w, SourceY[y] / 787f)));
            }
        }

        for (int y = 0; y < SourceY.Length - 1; y++)
        {
            for (int x = 0; x < SourceX.Length - 1; x++)
            {
                int bottomLeft = y * SourceX.Length + x;
                vertices.AddTriangle(bottomLeft, bottomLeft + 6, bottomLeft + 7);
                vertices.AddTriangle(bottomLeft + 7, bottomLeft + 1, bottomLeft);
            }
        }
    }
}
