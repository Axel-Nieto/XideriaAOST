using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnemySpawnerTests
{
    private const string Folder = "Assets/Prefabs/P_Enemigos/Zona_1/";
    private Scene scene;
    private GridSystem grid;
    private EnemySpawner spawner;
    private UnityEngine.Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        randomState = UnityEngine.Random.state;
        scene = EditorSceneManager.NewPreviewScene();
        grid = ObjectInScene("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 16); Set(grid, "height", 16); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        spawner = ObjectInScene("Spawner").AddComponent<EnemySpawner>();
        Set(spawner, "grid", grid);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Random.state = randomState;
        EditorSceneManager.ClosePreviewScene(scene);
    }

    [TestCase("Larva.prefab", 1)]
    [TestCase("Larva_Aire.prefab", 1)]
    [TestCase("Larva_Tierra.prefab", 1)]
    [TestCase("Larva_Fuego.prefab", 1)]
    [TestCase("Jefe_Zona1/BossZone1Combat.prefab", 2)]
    public void SpawnConfiguresBeforeAwakeAndRegistersOnlyTheIntendedFootprint(string asset, int size)
    {
        Character prefab = Prefab(asset);
        var origin = new Vector2Int(3, 4);
        var offset = new Vector3(-0.2f, 1.5f, -0.8f);
        spawner.transform.position = new Vector3(40f, 20f, 10f);
        spawner.transform.localScale = Vector3.one * 3f;
        Set(spawner, "allowedEnemies", new List<Character> { prefab });
        Set(spawner, "spawnPoints", new List<EnemySpawnPoint> {
            new() { gridPosition = origin, visualOffset = offset }
        });

        spawner.SpawnEnemies();
        Assert.That(spawner.SpawnedEnemies.Count, Is.EqualTo(1));
        Character enemy = spawner.SpawnedEnemies[0];
        Assert.That(enemy.gameObject.scene, Is.EqualTo(scene));
        Assert.That(enemy.gameObject.activeSelf, Is.True);
        Assert.That(enemy.VisualOffset, Is.EqualTo(offset));
        Assert.That(enemy.transform.localScale, Is.EqualTo(prefab.transform.localScale));
        // EditMode does not run MonoBehaviour.Awake automatically. Exercise the
        // actual Character initialization after the spawner has configured it.
        if (enemy.Stats == null) Call(enemy, "Awake");
        Assert.That(enemy.GridPosition, Is.EqualTo(origin));
        Assert.That(enemy.GridWidth, Is.EqualTo(size));
        Assert.That(enemy.GridHeight, Is.EqualTo(size));
        Assert.That(enemy.Stats.IsAlive, Is.True);
        Assert.That(enemy.Stats.CurrentEnergy, Is.Zero);
        Assert.That(enemy.transform.position, Is.EqualTo(grid.GridToWorld(origin, enemy) + offset));
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                bool occupied = enemy.GetOccupiedCells().Contains(cell);
                Assert.That(grid.GetCharacterAt(cell), occupied ? Is.SameAs(enemy) : Is.Null);
            }

        spawner.SpawnEnemies();
        Assert.That(spawner.SpawnedEnemies.Count, Is.EqualTo(1), "A repeated call cannot duplicate the encounter.");
    }

    [Test]
    public void ConfigurationRejectsActiveOrAlreadyInitializedCharacters()
    {
        GameObject go = ObjectInScene("Enemy");
        go.SetActive(false);
        var enemy = go.AddComponent<Character>();
        Set(enemy, "baseStats", new CharacterStatsData());
        go.SetActive(true);
        Assert.Throws<InvalidOperationException>(() => enemy.ConfigureSpawn(grid, Vector2Int.one, Vector3.zero));
        go.SetActive(false);
        enemy.ConfigureSpawn(grid, Vector2Int.one, Vector3.zero);
        Call(enemy, "Awake");
        Assert.Throws<InvalidOperationException>(() => enemy.ConfigureSpawn(grid, new Vector2Int(8, 8), Vector3.zero));
        Assert.That(grid.GetCharacterAt(Vector2Int.one), Is.SameAs(enemy));
        Assert.That(grid.GetCharacterAt(new Vector2Int(8, 8)), Is.Null);
    }

    [TestCase(1, CellType.Blocked)]
    [TestCase(2, CellType.Blocked)]
    [TestCase(1, CellType.Void)]
    [TestCase(2, CellType.Void)]
    public void SpawnValidationChecksTerrainAtEveryOccupiedCell(int size, CellType type)
    {
        Character prefab = Prefab(size == 1 ? "Larva.prefab" : "Jefe_Zona1/BossZone1Combat.prefab");
        var origin = new Vector2Int(3, 4);
        Assert.That(CanSpawn(prefab, origin), Is.True);
        grid.SetCellType(origin + new Vector2Int(size - 1, size - 1), type);
        Assert.That(CanSpawn(prefab, origin), Is.False);
    }

    [TestCase(1)]
    [TestCase(2)]
    public void SpawnValidationChecksBoundsAndOccupancyIncludingANonOriginCell(int size)
    {
        Character prefab = Prefab(size == 1 ? "Larva.prefab" : "Jefe_Zona1/BossZone1Combat.prefab");
        Assert.That(CanSpawn(prefab, new Vector2Int(16 - size, 16 - size)), Is.True);
        Assert.That(CanSpawn(prefab, new Vector2Int(17 - size, 16 - size)), Is.False);
        var origin = new Vector2Int(3, 4);
        GameObject go = ObjectInScene("Occupant");
        go.SetActive(false);
        Character occupant = go.AddComponent<Character>();
        grid.RegisterCharacter(origin + new Vector2Int(size - 1, size - 1), occupant);
        Assert.That(CanSpawn(prefab, origin), Is.False);
        Assert.That(grid.GetCharacterAt(origin + new Vector2Int(size - 1, size - 1)), Is.SameAs(occupant));
    }

    [Test]
    public void InvalidPointDoesNotReplaceAnOccupantOrChooseAnUnlistedEnemy()
    {
        GameObject go = ObjectInScene("Occupant");
        go.SetActive(false);
        Character occupant = go.AddComponent<Character>();
        grid.RegisterCharacter(new Vector2Int(3, 4), occupant);
        Set(spawner, "allowedEnemies", new List<Character> { Prefab("Larva_Aire.prefab") });
        Set(spawner, "spawnPoints", new List<EnemySpawnPoint> {
            new() { gridPosition = new Vector2Int(3, 4) },
            new() { gridPosition = new Vector2Int(6, 7) }
        });
        LogAssert.Expect(LogType.Error, "EnemySpawner: point 0 has no allowed enemy that fits. Check prefabs, terrain and occupancy.");
        spawner.SpawnEnemies();
        Assert.That(spawner.SpawnedEnemies.Count, Is.EqualTo(1));
        Assert.That(spawner.SpawnedEnemies[0].GetComponent<ElementalLarvaSetup>(), Is.Not.Null);
        if (spawner.SpawnedEnemies[0].Stats == null) Call(spawner.SpawnedEnemies[0], "Awake");
        Assert.That(spawner.SpawnedEnemies[0].GridPosition, Is.EqualTo(new Vector2Int(6, 7)));
        Assert.That(grid.GetCharacterAt(new Vector2Int(3, 4)), Is.SameAs(occupant));
    }

    private bool CanSpawn(Character prefab, Vector2Int origin) => (bool)Call(spawner, "CanSpawn", prefab, origin);
    private static Character Prefab(string asset) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + asset).GetComponent<Character>();
    private GameObject ObjectInScene(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
    private static void Set(object owner, string field, object value) => owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
}

public class EnemySpawnerSceneTests
{
    [TestCase("Level01", "11,6;8,9")]
    [TestCase("Level02", "11,6;5,1")]
    [TestCase("Level03", "9,9;7,3")]
    [TestCase("Level04", "11,6;9,9;8,2")]
    [TestCase("Level05", "11,6;11,7")]
    [TestCase("Level06", "11,6;8,9;8,2")]
    [TestCase("Level07", "9,9;7,3")]
    [TestCase("Level08", "11,6;9,9;8,2")]
    [TestCase("Level09", "11,6;11,7")]
    [TestCase("Level10", "11,6;5,1")]
    [TestCase("LevelBossZ1", "6,11")]
    public void MapsKeepTheirOriginalEncounterAndBossIsExclusive(string map, string expectedPositions)
    {
        Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Zone -1/" + map + ".unity");
        try
        {
            var spawners = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<EnemySpawner>(true)).ToArray();
            Assert.That(spawners.Length, Is.EqualTo(1));
            EnemySpawner spawner = spawners[0];
            var grid = (GridSystem)new SerializedObject(spawner).FindProperty("grid").objectReferenceValue;
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.gameObject.scene, Is.EqualTo(scene));
            Assert.That(scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Character>(true))
                .Where(character => !character.IsPlayer), Is.Empty, "No placed enemy should remain alongside the spawner.");
            CollectionAssert.AreEqual(expectedPositions.Split(';'), spawner.SpawnPoints.Select(p => p.gridPosition.x + "," + p.gridPosition.y));
            bool boss = map == "LevelBossZ1";
            Assert.That(spawner.AllowedEnemies.Count, Is.EqualTo(boss ? 1 : 4));
            Assert.That(spawner.AllowedEnemies.All(prefab => prefab != null && !prefab.IsPlayer && EditorUtility.IsPersistent(prefab)), Is.True);
            CollectionAssert.AreEquivalent(boss ? new[] { "BossZone1Combat" } : new[] { "Larva", "Larva_Aire", "Larva_Tierra", "Larva_Fuego" },
                spawner.AllowedEnemies.Select(prefab => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(prefab))));
            Vector3 offset = boss ? new Vector3(-0.2f, 0.5f, -0.8f) : new Vector3(-0.3f, 0.1f, -0.3f);
            foreach (EnemySpawnPoint point in spawner.SpawnPoints)
                Assert.That(point.visualOffset, Is.EqualTo(offset));
            if (boss)
            {
                Assert.That(spawner.AllowedEnemies[0].GridWidth, Is.EqualTo(2));
                Assert.That(spawner.AllowedEnemies[0].GridHeight, Is.EqualTo(2));
                Assert.That(spawner.AllowedEnemies[0].GetComponent<ArmoredLarvaSetup>(), Is.Not.Null);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
