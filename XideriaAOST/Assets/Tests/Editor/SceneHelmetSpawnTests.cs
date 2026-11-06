using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneHelmetSpawnTests
{
    private Scene scene;
    private GridSystem grid;
    private HelmetDropTable table;
    private HelmetCatalog previousCatalog;
    private HelmetDropTable previousTable;
    private Random.State randomState;
    private readonly Vector2Int origin = new(3, 7);
    private readonly Vector3 offset = new(-0.4f, 1.2f, 0.3f);

    [SetUp]
    public void SetUp()
    {
        randomState = Random.state;
        previousCatalog = HelmetCatalog.Instance;
        previousTable = HelmetDropTable.Instance;
        scene = EditorSceneManager.NewPreviewScene();
        var catalog = ObjectInScene("Catalog").AddComponent<HelmetCatalog>();
        typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, catalog);
        grid = ObjectInScene("Grid").AddComponent<GridSystem>();
        Set(grid, "cellSize", 2f);
        table = ObjectInScene("Drops").AddComponent<HelmetDropTable>();
        // The table transform must not scale or rotate the new models.
        table.transform.SetPositionAndRotation(Vector3.one * 20f, Quaternion.Euler(0, 60, 0));
        table.transform.localScale = Vector3.one * 3f;
    }

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(scene);
        typeof(HelmetCatalog).GetProperty(nameof(HelmetCatalog.Instance)).SetValue(null, previousCatalog);
        typeof(HelmetDropTable).GetProperty(nameof(HelmetDropTable.Instance)).SetValue(null, previousTable);
        Random.state = randomState;
    }

    [TestCase("Fire", HelmetType.Fire)]
    [TestCase("Water", HelmetType.Water)]
    [TestCase("Rock", HelmetType.Rock)]
    public void ReplacementKeepsMapLocationAndUsesSelectedPrefabAndEquipment(string element, HelmetType type)
    {
        HelmetPickup original = SpawnPoint();
        Quaternion rotation = Quaternion.Euler(0, 135f, 0);
        original.transform.rotation = rotation;
        GameObject prefab = Prefab(element);
        Set(table, "helmetPickupPrefabs", new List<GameObject> { prefab });
        Call(table, "Awake");
        Call(table, "Start");

        HelmetPickup replacement = Pickups().Single();
        Assert.That(original == null, Is.True);
        Assert.That(replacement.Type, Is.EqualTo(type));
        Assert.That(replacement.GridPosition, Is.EqualTo(origin));
        Assert.That(replacement.gameObject.scene, Is.EqualTo(scene));
        Assert.That(replacement.transform.position, Is.EqualTo(grid.GridToWorld(origin) + offset));
        Assert.That(Quaternion.Angle(replacement.transform.rotation, rotation), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(replacement.transform.localScale, prefab.transform.localScale), Is.LessThan(0.0001f));
        Assert.That(replacement.GetComponentInChildren<HelmetPickupAnimator>(), Is.Not.Null);
        Assert.That(replacement.GetComponentInChildren<Animator>().runtimeAnimatorController,
            Is.SameAs(prefab.GetComponentInChildren<Animator>().runtimeAnimatorController));
        // EditMode does not automatically run Awake on runtime clones.
        Call(replacement, "Awake");
        Assert.That(replacement.GetHelmet().Type, Is.EqualTo(type));
        Assert.That(replacement.transform.position, Is.EqualTo(grid.GridToWorld(origin) + offset));
        replacement.MarkAsCollected();
        Assert.That(replacement.IsCollected, Is.True);
    }

    [Test]
    public void EachLocationRollsIndependentlyAndReloadsCanProduceAllConfiguredElements()
    {
        Random.InitState(619);
        SpawnPoint();
        SpawnPoint(new Vector2Int(8, 2));
        Set(table, "helmetPickupPrefabs", new List<GameObject> { Prefab("Fire"), Prefab("Water"), Prefab("Rock") });
        var seen = new HashSet<HelmetType>();
        bool repeatedElement = false;
        for (int load = 0; load < 24; load++)
        {
            Set(table, "sceneHelmetsRandomized", false);
            Call(table, "Awake");
            Call(table, "Start");
            HelmetPickup[] helmets = Pickups();
            Assert.That(helmets.Length, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { origin, new Vector2Int(8, 2) }, helmets.Select(h => h.GridPosition));
            foreach (HelmetPickup helmet in helmets) seen.Add(helmet.Type);
            repeatedElement |= helmets[0].Type == helmets[1].Type;
        }
        CollectionAssert.AreEquivalent(new[] { HelmetType.Fire, HelmetType.Water, HelmetType.Rock }, seen);
        Assert.That(repeatedElement, Is.True, "Each location is an independent draw, with repeats allowed.");
    }

    [Test]
    public void LaterLootAndRepeatedCallsKeepTheirIdentityAndPosition()
    {
        SpawnPoint();
        Set(table, "helmetPickupPrefabs", new List<GameObject> { Prefab("Rock") });
        Call(table, "Awake");
        HelmetPickup loot = SpawnPoint(new Vector2Int(9, 4));
        table.RandomizeSceneHelmets();
        HelmetPickup[] before = Pickups();
        table.RandomizeSceneHelmets();
        Call(table, "Start");
        CollectionAssert.AreEquivalent(before, Pickups());
        Assert.That(loot != null, Is.True);
        Assert.That(loot.Type, Is.EqualTo(HelmetType.Fire));
        Assert.That(loot.GridPosition, Is.EqualTo(new Vector2Int(9, 4)));
        Assert.That(table.GetRandomHelmetPickupPrefab(), Is.SameAs(Prefab("Rock")));
    }

    [Test]
    public void EmptyOrInvalidPoolLeavesAuthoredPickupsAvailable()
    {
        HelmetPickup original = SpawnPoint();
        Set(table, "helmetPickupPrefabs", new List<GameObject> { null, ObjectInScene("Invalid prefab") });
        Call(table, "Awake");
        table.RandomizeSceneHelmets();
        Assert.That(Pickups().Single(), Is.SameAs(original));
        Assert.That(original.gameObject.activeInHierarchy, Is.True);
    }

    [Test]
    public void MapCanOptOutOfRandomization()
    {
        HelmetPickup original = SpawnPoint();
        Set(table, "helmetPickupPrefabs", new List<GameObject> { Prefab("Rock") });
        Set(table, "randomizeSceneHelmets", false);
        Call(table, "Awake");
        Call(table, "Start");
        Assert.That(Pickups().Single(), Is.SameAs(original));
    }

    [Test]
    public void RandomizationDoesNotTouchOtherLoadedScenes()
    {
        Scene other = EditorSceneManager.NewPreviewScene();
        try
        {
            HelmetPickup original = SpawnPoint();
            SceneManager.MoveGameObjectToScene(original.gameObject, other);
            SpawnPoint();
            Set(table, "helmetPickupPrefabs", new List<GameObject> { Prefab("Water") });
            Call(table, "Awake");
            Call(table, "Start");
            Assert.That(original != null && original.gameObject.activeInHierarchy, Is.True);
            Assert.That(Pickups().Single().Type, Is.EqualTo(HelmetType.Water));
        }
        finally { EditorSceneManager.ClosePreviewScene(other); }
    }

    private HelmetPickup SpawnPoint(Vector2Int? position = null)
    {
        HelmetPickup pickup = ObjectInScene("Authored helmet").AddComponent<HelmetPickup>();
        Set(pickup, "_grid", grid);
        Set(pickup, "helmetType", HelmetType.Fire);
        Set(pickup, "startGridPosition", position ?? origin);
        Set(pickup, "visualOffset", offset);
        Call(pickup, "Awake");
        return pickup;
    }

    private GameObject ObjectInScene(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private HelmetPickup[] Pickups() => scene.GetRootGameObjects()
        .SelectMany(go => go.GetComponentsInChildren<HelmetPickup>()).Where(p => p.gameObject.activeInHierarchy).ToArray();

    private static GameObject Prefab(string element)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HelmetPrefabs/Helmet_" + element + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<HelmetPickup>(), Is.Not.Null);
        return prefab;
    }

    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void Call(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
}
