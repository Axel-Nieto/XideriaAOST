using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class LarvaVFXTests : UpgradeIntegrationFixture
{
    [Test]
    public void EarthSlashRetainsItsOriginalVfxWithoutAddedParticleSystems()
    {
        string source = ReadSource("Assets/Prefabs/P_Skills/Skill Enemigos/Larva Tierra/Slash_larvaTierra.prefab");
        Assert.That(source, Does.Contain("guid: 088b9244483ab334485f62b23fe41c5b"));
        Assert.That(source, Does.Not.Contain("!u!198"));
        Assert.That(source, Does.Not.Contain("Earth Slash Dust"));
        Assert.That(source, Does.Not.Contain("Earth Slash Fragments"));
    }

    [Test]
    public void EarthShaderOutputsUseTheAuthoredNoisePower()
    {
        string source = ReadSource("Assets/Prefabs/P_Skills/Skill Enemigos/Larva Tierra/Vfx_SkillLarva_Tierra.vfx");
        int outputs = 0;
        foreach (Match block in Regex.Matches(source, @"(?ms)^--- !u!\d+ &\d+.*?(?=^--- !u!|\z)"))
        {
            if (!block.Value.Contains("name: _PowerVoro")) continue;
            outputs++;
            Assert.That(block.Value, Does.Contain("m_SerializableObject: 0.8"));
            Assert.That(block.Value, Does.Contain("m_LinkedSlots: []"), "A dissolution curve must not replace the noise contrast.");
        }
        Assert.That(outputs, Is.EqualTo(2));
    }

    [Test]
    public void FireShaderTransparencyUsesTheTextureMaskInsteadOfOpaqueJpegAlpha()
    {
        string source = ReadSource("Assets/Prefabs/P_Skills/Skill Enemigos/Larva Fuego/New Folder/S_PanShader.shadergraph");
        var objects = Regex.Matches(source, @"(?ms)^\{.*?^\}").Cast<Match>().ToArray();
        var headers = objects.Select(m => JsonUtility.FromJson<ShaderNodeHeader>(m.Value)).ToArray();
        ShaderNodeHeader alpha = headers.Single(n => n.m_Type == "UnityEditor.ShaderGraph.BlockNode" && n.m_Name == "SurfaceDescription.Alpha");
        ShaderNodeHeader sample = headers.Single(n => n.m_Type == "UnityEditor.ShaderGraph.SampleTexture2DNode");
        int graphIndex = Array.FindIndex(headers, n => n.m_Type == "UnityEditor.ShaderGraph.GraphData");
        ShaderEdges graph = JsonUtility.FromJson<ShaderEdges>(objects[graphIndex].Value);
        ShaderEdge edge = graph.m_Edges.Single(e => e.m_InputSlot.m_Node.m_Id == alpha.m_ObjectId);
        Assert.That(edge.m_OutputSlot.m_Node.m_Id, Is.EqualTo(sample.m_ObjectId));
        Assert.That(edge.m_OutputSlot.m_SlotId, Is.EqualTo(4), "Sample Texture 2D's R channel carries the grayscale mask.");
    }

    [Serializable] private class ShaderNodeHeader { public string m_Type, m_Name, m_ObjectId; }
    [Serializable] private class ShaderEdges { public ShaderEdge[] m_Edges; }
    [Serializable] private class ShaderEdge { public ShaderSlot m_OutputSlot, m_InputSlot; }
    [Serializable] private class ShaderSlot { public ShaderNodeReference m_Node; public int m_SlotId; }
    [Serializable] private class ShaderNodeReference { public string m_Id; }

    [TestCase(1, 1, 0)] [TestCase(1, -1, 0)] [TestCase(1, 0, 1)] [TestCase(1, 0, -1)]
    [TestCase(2, 1, 0)] [TestCase(2, -1, 0)] [TestCase(2, 0, 1)] [TestCase(2, 0, -1)]
    public void FrontEffectUsesTheCellBeyondTheWholeFootprint(int size, int dx, int dy)
    {
        GridSystem grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "cellSize", 1.5f);
        Character owner = Child("Larva").AddComponent<Character>();
        Set(owner, "gridWidth", size); Set(owner, "gridHeight", size);
        owner.SetGridPosition(new Vector2Int(4, 4));
        Vector3 visualPosition = owner.transform.position;
        Vector3 direction = new(dx, 0, dy);
        Vector3 position = (Vector3)Invoke("FrontPosition", owner, grid, direction);
        Vector3 expected = grid.GridToWorld(owner.GridPosition, owner) + direction * (1.5f * (size + 1) * 0.5f);
        Assert.That(Vector3.Distance(position, expected), Is.LessThan(0.001f));
        Assert.That(owner.GridPosition, Is.EqualTo(new Vector2Int(4, 4)));
        Assert.That(owner.transform.position, Is.EqualTo(visualPosition));
    }

    [Test]
    public void AirUsesAlignedFootprintCellsEvenWhenTheCentersAreOffset()
    {
        GridSystem grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "cellSize", 1f);
        Character owner = Child("Larva").AddComponent<Character>();
        owner.SetGridPosition(new Vector2Int(2, 3));
        Character target = Child("Large target").AddComponent<Character>();
        Set(target, "gridWidth", 2); Set(target, "gridHeight", 2);
        target.SetGridPosition(new Vector2Int(6, 2));
        Assert.That((Vector3)Invoke("Direction", owner, target, grid, true), Is.EqualTo(Vector3.right));
    }

    [TestCase(1, 1)] [TestCase(2, 1)] [TestCase(1, 2)] [TestCase(2, 2)]
    public void EarthChainSlashStaysCenteredOnAndFollowsTheLarva(int ownerSize, int targetSize)
    {
        GridSystem grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "cellSize", 1.5f);
        Character owner = Child("Earth larva").AddComponent<Character>();
        owner.enabled = false;
        Set(owner, "baseStats", new CharacterStatsData());
        Set(owner, "gridWidth", ownerSize); Set(owner, "gridHeight", ownerSize);
        Set(owner, "visualSpawnOffset", new Vector3(-0.3f, 0.1f, -0.3f));
        owner.transform.localScale = Vector3.one * 0.7f;
        owner.gameObject.SetActive(true);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(owner, new CharacterStats(4, 2, 100, 10, 5));
        owner.Stats.SetCurrentEnergy(10);
        owner.SetGridPosition(new Vector2Int(4, 4));
        owner.transform.position = grid.GridToWorld(owner.GridPosition, owner) + owner.VisualOffset;
        Character target = Child("Tete").AddComponent<Character>();
        Set(target, "isPlayer", true);
        Set(target, "gridWidth", targetSize); Set(target, "gridHeight", targetSize);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(target, new CharacterStats(4, 2, 100, 10, 5));
        target.SetGridPosition(new Vector2Int(4 + ownerSize, 4));
        LarvaVFX presentation = owner.gameObject.AddComponent<LarvaVFX>();
        GameObject prefab = Child("Earth slash template");
        Set(presentation, "specialEffectPrefab", prefab);
        // Run the real Cadenazo effect hook, up to the pending push coroutine.
        var skill = new LarvaChainSkill();
        IEnumerator hit = (IEnumerator)typeof(LarvaChainSkill).GetMethod("ApplyEffect", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(skill, new object[] { owner, target, grid });
        GameObject effect = null;
        try
        {
            ExpectRuntimeDestroy(); // staging holder
            ExpectRuntimeDestroy(); // slash lifetime
            Assert.That(hit.MoveNext(), Is.True);
            effect = Resources.FindObjectsOfTypeAll<GameObject>().Single(g => g.name == "Earth slash template(Clone)");
            Assert.That(effect.transform.parent, Is.SameAs(owner.transform));
            Vector3 center = owner.transform.position + Vector3.up * 0.3f;
            Assert.That(Vector3.Distance(effect.transform.position, center), Is.LessThan(0.001f));
            Assert.That(effect.transform.lossyScale.x, Is.EqualTo(1.5f).Within(0.001f));
            target.transform.position += Vector3.right * 3f;
            Assert.That(Vector3.Distance(effect.transform.position, center), Is.LessThan(0.001f));
            Vector3 translation = new Vector3(2f, 0f, -1f);
            owner.transform.position += translation;
            Assert.That(Vector3.Distance(effect.transform.position, center + translation), Is.LessThan(0.001f));
            Quaternion rotation = effect.transform.rotation;
            owner.transform.Rotate(Vector3.up, 90f, Space.World);
            Assert.That(Quaternion.Angle(effect.transform.rotation, Quaternion.AngleAxis(90f, Vector3.up) * rotation), Is.LessThan(0.01f));
            Assert.That(owner.GridPosition, Is.EqualTo(new Vector2Int(4, 4)));
            Assert.That(target.Stats.CurrentHP, Is.EqualTo(98));
        }
        finally
        {
            ((IDisposable)hit).Dispose();
            if (effect != null) UnityEngine.Object.DestroyImmediate(effect);
            ClearStagingObjects();
        }
    }

    [Test]
    public void DeathParticlesIgnoreStagingOffsetsAndRemainActiveWithoutTheLarva()
    {
        GameObject prefab = Child("Burst template");
        prefab.transform.position = new Vector3(-7, 0, 10);
        GameObject emitter = new GameObject("SplashDeath");
        emitter.transform.SetParent(prefab.transform, false);
        emitter.transform.localPosition = new Vector3(0.53f, 0.76f, 0.16f);
        ParticleSystem source = emitter.AddComponent<ParticleSystem>();
        var main = source.main;
        main.loop = true;
        Vector3 center = new(4, 1, 6);
        ExpectRuntimeDestroy();
        GameObject effect = (GameObject)Invoke("CreateEffect", prefab, center, Quaternion.identity, 2f, true);
        try
        {
            ParticleSystem burst = effect.GetComponentInChildren<ParticleSystem>();
            Assert.That(burst, Is.Not.Null);
            Assert.That(Vector3.Distance(burst.transform.position, center), Is.LessThan(0.001f));
            Assert.That(burst.main.loop, Is.False);
            Assert.That(burst.isPlaying, Is.True);
            Assert.That(source.main.loop, Is.True, "The source prefab must remain unchanged.");
            Assert.That(effect.transform.parent, Is.Null);
            root.SetActive(false);
            Assert.That(effect.activeInHierarchy, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
            ClearStagingObjects();
        }
    }

    [Test]
    public void FireSpitStartsAtTheLarvaAndTravelsToTheTargetsCenter()
    {
        GridSystem grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "cellSize", 1f);
        Character owner = Child("Larva").AddComponent<Character>();
        Character target = Child("Target").AddComponent<Character>();
        owner.SetGridPosition(new Vector2Int(2, 2));
        target.SetGridPosition(new Vector2Int(6, 2));
        LarvaVFX presentation = owner.gameObject.AddComponent<LarvaVFX>();
        GameObject prefab = Child("Spit template");
        Set(presentation, "specialEffectPrefab", prefab);
        IEnumerator projectile = presentation.PlayProjectile(target, grid);
        GameObject effect = null;
        try
        {
            ExpectRuntimeDestroy(); // temporary inactive holder
            ExpectRuntimeDestroy(); // projectile timeout
            Assert.That(projectile.MoveNext(), Is.True);
            effect = Resources.FindObjectsOfTypeAll<GameObject>().Single(g => g.name == "Spit template(Clone)");
            Vector3 end = grid.GridToWorld(target.GridPosition, target) + target.VisualOffset + Vector3.up * 0.7f;
            Assert.That(Vector3.Distance(effect.transform.forward, Vector3.right), Is.LessThan(0.001f));
            Assert.That(effect.transform.position.y, Is.EqualTo(end.y).Within(0.001f));
            Assert.That(effect.transform.position.x, Is.LessThan(end.x));
            Assert.That(effect.transform.parent, Is.Null);
            effect.transform.position = end;
            ExpectRuntimeDestroy(); // projectile removal at arrival
            Assert.That(projectile.MoveNext(), Is.False);
        }
        finally
        {
            ((IDisposable)projectile).Dispose();
            if (effect != null) UnityEngine.Object.DestroyImmediate(effect);
            ClearStagingObjects();
        }
    }

    private static void ExpectRuntimeDestroy() => LogAssert.Expect(LogType.Error,
        new Regex("Destroy may not be called from edit mode!"));

    private static void ClearStagingObjects()
    {
        foreach (GameObject holder in Resources.FindObjectsOfTypeAll<GameObject>())
            if (holder.name == "Larva effect staging" && holder.scene.IsValid() && holder.transform.childCount == 0)
                UnityEngine.Object.DestroyImmediate(holder);
    }

    [TestCase("Larva", null)] [TestCase("Larva Tierra", null)]
    [TestCase("Larva_Aire", "6cd1d3794afde1145bdf65a9073c5320")]
    [TestCase("Larva_Tierra", "1c22063ad51208c43866358a7be55382")]
    [TestCase("Larva_Fuego", "ff4d3871969e224418f2bc1aaf90f329")]
    public void CombatPrefabsReferenceTheDeathBurstAndTheirOwnSpecial(string name, string specialGuid)
    {
        string source = ReadSource($"Assets/Prefabs/P_Enemigos/Zona_1/{name}.prefab");
        Match presentation = Regex.Match(source,
            @"(?ms)^--- !u!114 &(?<id>\d+)\r?\n(?:(?!^--- !u!).)*guid: 4fe187eabfca4ac9978cfddf51321a3f(?:(?!^--- !u!).)*");
        Assert.That(presentation.Success, Is.True);
        Assert.That(presentation.Value, Does.Contain("deathEffectPrefab: {fileID: 7517365012362386973, guid: 9d4608bc4971b2d4ea416f1031927b43"));
        Assert.That(source, Does.Contain("component: {fileID: " + presentation.Groups["id"].Value + "}"));
        if (specialGuid == null) Assert.That(presentation.Value, Does.Contain("specialEffectPrefab: {fileID: 0}"));
        else Assert.That(presentation.Value, Does.Contain("guid: " + specialGuid));
    }

    [TestCase("Aire")] [TestCase("Tierra")] [TestCase("Fuego")]
    public void SpecialAnimationsStillSignalTheCastHitAndFinish(string element)
    {
        string source = ReadSource($"Assets/Prefabs/P_Enemigos/Zona_1/ElementalAnimations/Larva_{element}_Skill.anim");
        Assert.That(Regex.Matches(source, "functionName: OnCastHitFrame").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(source, "functionName: OnCastAnimationFinished").Count, Is.EqualTo(1));
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name);
        child.SetActive(false);
        child.transform.SetParent(root.transform, false);
        return child;
    }

    private static object Invoke(string method, params object[] args) => typeof(LarvaVFX)
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

    private static string ReadSource(string path)
    {
        if (!File.Exists(path)) path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../../..")), path);
        return File.ReadAllText(path);
    }
}
