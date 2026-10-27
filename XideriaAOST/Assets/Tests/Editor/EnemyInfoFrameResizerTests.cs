using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class EnemyInfoFrameResizerTests
{
    private GameObject panel;
    private Texture2D texture;
    private Sprite sprite;
    private Image image;
    private EnemyInfoFrameResizer resizer;

    [SetUp]
    public void SetUp()
    {
        texture = new Texture2D(787, 787);
        sprite = Sprite.Create(texture, new Rect(0, 0, 787, 787), Vector2.one * 0.5f);
        panel = new GameObject("Enemy frame", typeof(RectTransform), typeof(Image));
        image = panel.GetComponent<Image>();
        image.sprite = sprite;
        resizer = panel.AddComponent<EnemyInfoFrameResizer>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(panel);
        Object.DestroyImmediate(sprite);
        Object.DestroyImmediate(texture);
    }

    [TestCase(400f, 400f)]
    [TestCase(800f, 400f)]
    [TestCase(400f, 800f)]
    [TestCase(250f, 400f)]
    [TestCase(800f, 100f)]
    public void ResizingPreservesOrnamentProportionsAndKeepsMeshInsidePanel(float width, float height)
    {
        image.rectTransform.sizeDelta = new Vector2(width, height);
        image.rectTransform.pivot = new Vector2(0.25f, 0.75f);
        Rect rect = image.GetPixelAdjustedRect();

        using (var mesh = new VertexHelper())
        {
            resizer.ModifyMesh(mesh);
            Assert.That(mesh.currentVertCount, Is.EqualTo(24));
            Assert.That(mesh.currentIndexCount, Is.EqualTo(90));

            // Both central ornaments and all corners must have equal X/Y scale.
            foreach (int row in new[] { 0, 2 })
            {
                foreach (int column in new[] { 0, 2, 4 })
                {
                    UIVertex a = Vertex(mesh, row * 6 + column);
                    UIVertex b = Vertex(mesh, (row + 1) * 6 + column + 1);
                    float scaleX = (b.position.x - a.position.x) / ((b.uv0.x - a.uv0.x) * 787f);
                    float scaleY = (b.position.y - a.position.y) / ((b.uv0.y - a.uv0.y) * 787f);
                    Assert.That(scaleX, Is.EqualTo(scaleY).Within(0.00001f));
                }
            }

            Assert.That((Vertex(mesh, 2).position.x + Vertex(mesh, 3).position.x) * 0.5f,
                Is.EqualTo(rect.center.x).Within(0.001f));
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                Vector3 position = Vertex(mesh, i).position;
                Assert.That(position.x, Is.InRange(rect.xMin - 0.001f, rect.xMax + 0.001f));
                Assert.That(position.y, Is.InRange(rect.yMin - 0.001f, rect.yMax + 0.001f));
                if (i % 6 != 0)
                    Assert.That(position.x + 0.001f, Is.GreaterThanOrEqualTo(Vertex(mesh, i - 1).position.x));
            }
        }
    }

    [Test]
    public void ExpandingPanelKeepsDecorationSize()
    {
        using (var mesh = new VertexHelper())
        {
            image.rectTransform.sizeDelta = new Vector2(400f, 400f);
            resizer.ModifyMesh(mesh);
            Vector3 originalCorner = Vertex(mesh, 7).position - Vertex(mesh, 0).position;
            image.rectTransform.sizeDelta = new Vector2(800f, 800f);
            resizer.ModifyMesh(mesh);
            Vector3 expandedCorner = Vertex(mesh, 7).position - Vertex(mesh, 0).position;
            Assert.That(expandedCorner.x, Is.EqualTo(originalCorner.x).Within(0.001f));
            Assert.That(expandedCorner.y, Is.EqualTo(originalCorner.y).Within(0.001f));
        }
    }

    [Test]
    public void ZeroSizePanelProducesEmptyMesh()
    {
        image.rectTransform.sizeDelta = Vector2.zero;
        using (var mesh = new VertexHelper())
        {
            resizer.ModifyMesh(mesh);
            Assert.That(mesh.currentVertCount, Is.Zero);
        }
    }

    private static UIVertex Vertex(VertexHelper mesh, int index)
    {
        UIVertex vertex = default;
        mesh.PopulateUIVertex(ref vertex, index);
        return vertex;
    }
}
