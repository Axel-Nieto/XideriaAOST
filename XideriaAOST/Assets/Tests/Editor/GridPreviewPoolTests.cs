using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GridPreviewPoolTests
{
    private Scene scene;
    private GameObject root;
    private GridInputHandler input;

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Preview pool tests");
        SceneManager.MoveGameObjectToScene(root, scene);
        input = Child("Input").AddComponent<GridInputHandler>();
    }

    [TearDown]
    public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

    [Test]
    public void AwakePreservesAnAssignedPool()
    {
        TileObjectPool assigned = Child("Assigned pool").AddComponent<TileObjectPool>();
        Set("tilePool", assigned);
        Call("Awake");
        Assert.That(Get<TileObjectPool>("tilePool"), Is.SameAs(assigned));
        Assert.That(input.GetComponent<TileObjectPool>(), Is.Null);
    }

    [TestCase(false)] [TestCase(true)]
    public void AwakeResolvesAnUnassignedPoolWithoutCreatingDuplicates(bool hasLocalPool)
    {
        TileObjectPool local = hasLocalPool ? input.gameObject.AddComponent<TileObjectPool>() : null;
        Call("Awake");
        TileObjectPool resolved = Get<TileObjectPool>("tilePool");
        Assert.That(resolved, Is.Not.Null);
        if (hasLocalPool) Assert.That(resolved, Is.SameAs(local));
        Call("Awake");
        Assert.That(Get<TileObjectPool>("tilePool"), Is.SameAs(resolved));
        Assert.That(input.GetComponents<TileObjectPool>().Length, Is.EqualTo(1));
    }

    [TestCase(1)] [TestCase(2)]
    public void MissingPoolSupportsPathRangeAndAreaPreviewsWithoutChangingTheActor(int size)
    {
        GridSystem grid = Child("Grid").AddComponent<GridSystem>();
        typeof(GridSystem).GetField("width", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grid, 8);
        typeof(GridSystem).GetField("height", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grid, 8);
        typeof(GridSystem).GetField("cellSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grid, 1f);
        grid.GetType().GetMethod("GenerateGrid", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grid, null);
        Character actor = Child("Actor").AddComponent<Character>();
        typeof(Character).GetField("gridWidth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, size);
        typeof(Character).GetField("gridHeight", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, size);
        actor.SetGridPosition(new Vector2Int(1, 1));
        grid.RegisterCharacter(actor);
        Vector3 position = actor.transform.position;
        GameObject prefab = Child("Tile prefab");
        Set("gridSystem", grid);
        Set("pathTilePrefab", prefab);
        Set("dangerRangeTilePrefab", prefab);
        Set("aoeTilePrefab", prefab);
        Set("selectedCharacter", actor);
        Set("currentPath", new List<Vector2Int> { new(2, 1), new(3, 1) });
        Set("reachableCells", new HashSet<Vector2Int> { new(2, 1), new(3, 1) });
        Set("dangerousPreviewCells", new HashSet<Vector2Int> { new(4, 1) });
        Call("Awake");
        Call("DrawPathTiles");
        GameObject[] pathTiles = Get<List<GameObject>>("activeTiles").ToArray();
        Assert.That(pathTiles.Length, Is.EqualTo(2));
        Call("DrawPathTiles");
        Assert.That(Get<List<GameObject>>("activeTiles"), Is.EquivalentTo(pathTiles));
        Assert.That(pathTiles[0].transform.position, Is.EqualTo(grid.GridToWorld(new Vector2Int(2, 1))));
        Call("DrawRangeTiles", prefab);
        Assert.That(Get<List<GameObject>>("rangeTiles").Count, Is.EqualTo(3));
        Call("DrawSelfSkillPreview", new FootprintPreviewSkill());
        Assert.That(Get<List<GameObject>>("aoeTiles").Count, Is.EqualTo(size * size));
        Call("ClearTiles");
        Call("ClearRangeTiles");
        Call("ClearAoeTiles");
        Assert.That(Get<TileObjectPool>("tilePool").GetComponentsInChildren<Transform>(true)
            .Skip(1).All(t => !t.gameObject.activeSelf), Is.True);
        Assert.That(actor.GridPosition, Is.EqualTo(new Vector2Int(1, 1)));
        Assert.That(actor.transform.position, Is.EqualTo(position));
        foreach (Vector2Int cell in actor.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(actor));
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name);
        child.SetActive(false);
        child.transform.SetParent(root.transform, false);
        return child;
    }

    private void Set(string name, object value) => typeof(GridInputHandler)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, value);
    private T Get<T>(string name) => (T)typeof(GridInputHandler)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
    private void Call(string name, params object[] args) => typeof(GridInputHandler)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, args);

    private sealed class FootprintPreviewSkill : SkillBase
    {
        public FootprintPreviewSkill() : base("Preview", "", 0, 0, 0, 0, SkillTargetType.Self, DamageType.Physical) { }
        public override bool CanUse(Character user, Vector2Int targetCell, GridSystem grid) => true;
        public override IEnumerator Execute(Character user, Vector2Int targetCell, GridSystem grid) { yield break; }
        public override IEnumerable<Vector2Int> GetPreviewCells(Character user, Vector2Int origin, Vector2Int targetCell, GridSystem grid) => user.GetOccupiedCells(origin);
    }
}
