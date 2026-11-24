using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed class PauseMenuTests
{
    private GameObject root;
    private PauseMenuUI menu;
    private RuntimeGameSettings settings;
    private float previousTime;
    private bool previousAudio;
    private bool previousGameOver;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private UnityEngine.Rendering.RenderPipelineAsset previousPipeline;
    private UniversalRenderPipelineAsset sourcePipeline;
    private Light light;
    private float ambientIntensity;
    private Color ambientLight, ambientSky, ambientEquator, ambientGround;

    [SetUp]
    public void SetUp()
    {
        previousTime = Time.timeScale;
        previousAudio = AudioListener.pause;
        previousGameOver = GameSession.IsGameOver;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        previousPipeline = QualitySettings.renderPipeline;
        ambientIntensity = RenderSettings.ambientIntensity;
        ambientLight = RenderSettings.ambientLight;
        ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
        GameSession.IsGameOver = false;
        root = new GameObject("Pause menu tests");
        var lighting = Child("Test light");
        light = lighting.AddComponent<Light>();
        light.intensity = 2f;
        settings = Child("Settings service").AddComponent<RuntimeGameSettings>();
        // EditMode binds the service explicitly without exercising DontDestroyOnLoad.
        typeof(RuntimeGameSettings).GetProperty(nameof(RuntimeGameSettings.Instance)).SetValue(null, settings);
        Set(settings, "previousOverride", previousPipeline);
        Call(settings, "CaptureLighting");
        menu = Child("Pause controller").AddComponent<PauseMenuUI>();
        Call(menu, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        if (menu != null) menu.Resume();
        UnityEngine.Object.DestroyImmediate(root);
        if (sourcePipeline != null) UnityEngine.Object.DestroyImmediate(sourcePipeline);
        QualitySettings.renderPipeline = previousPipeline;
        Time.timeScale = previousTime;
        AudioListener.pause = previousAudio;
        GameSession.IsGameOver = previousGameOver;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        typeof(PauseMenuUI).GetField("closedFrame", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, -1);
    }

    [Test]
    public void OpenAndContinueRestoreTimeAndAudioAndBlockTheClosingClick()
    {
        Time.timeScale = 0.65f;
        AudioListener.pause = false;
        menu.Open();
        Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.Pause));
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(AudioListener.pause, Is.True);
        menu.Open(); // Reopening must not overwrite the saved time scale with zero.
        Click("Continuar");
        Assert.That(PauseMenuUI.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(0.65f));
        Assert.That(AudioListener.pause, Is.False);
        Assert.That(PauseMenuUI.BlocksGameplayInput, Is.True);
    }

    [Test]
    public void SettingsStayPendingAndBackDiscardsAllFourValues()
    {
        menu.Open(); Click("Ajustes");
        menu.ChangeResolution(1); menu.ChangeQuality(1);
        FindSlider("Brillo").value = 0.6f;
        FindSlider("Sensibilidad del ratón").value = 1.25f;
        Assert.That(settings.GraphicsQuality, Is.EqualTo(2));
        Assert.That(settings.Brightness, Is.EqualTo(1f));
        Assert.That(settings.MouseSensitivity, Is.EqualTo(1f));
        Click("Volver"); Click("Ajustes");
        Assert.That(menu.PendingResolution, Is.EqualTo(new Vector2Int(Screen.width, Screen.height)));
        Assert.That(menu.PendingQuality, Is.EqualTo(2));
        Assert.That(menu.PendingBrightness, Is.EqualTo(1f));
        Assert.That(menu.PendingSensitivity, Is.EqualTo(1f));
    }

    [Test]
    public void ApplyRequiresConfirmationAndCancelKeepsTheDraft()
    {
        menu.Open(); menu.OpenSettings();
        menu.ChangeQuality(1);
        FindSlider("Brillo").value = 0.6f;
        FindSlider("Sensibilidad del ratón").value = 1.5f;
        Click("Aplicar");
        Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.ConfirmApply));
        Assert.That(settings.MouseSensitivity, Is.EqualTo(1f));
        Click("Cancelar");
        Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.Settings));
        Assert.That(menu.PendingSensitivity, Is.EqualTo(1.5f));
        menu.RequestApply(); Click("Confirmar");
        Assert.That(settings.GraphicsQuality, Is.Zero);
        Assert.That(settings.Brightness, Is.EqualTo(0.6f));
        Assert.That(settings.MouseSensitivity, Is.EqualTo(1.5f));
        Assert.That(light.intensity, Is.EqualTo(1.2f).Within(0.001f));
        Assert.That(PauseMenuUI.IsPaused, Is.True);
        Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.Settings));
        menu.Back(); menu.OpenSettings();
        Assert.That(menu.PendingSensitivity, Is.EqualTo(1.5f));
    }

    [Test]
    public void ExitRequiresConfirmationAndCancelPreservesTheSession()
    {
        PlayerSessionData previousSession = PlayerSessionData.Instance;
        var session = Child("Session").AddComponent<PlayerSessionData>();
        typeof(PlayerSessionData).GetProperty(nameof(PlayerSessionData.Instance)).SetValue(null, session);
        try
        {
            session.TryAddCurrency(RunCurrency.Gold, 27);
            menu.Open(); Click("Salir");
            Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.ConfirmExit));
            Assert.That(session.Gold, Is.EqualTo(27));
            Click("Cancelar");
            Assert.That(session.Gold, Is.EqualTo(27));
            Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.Pause));
            Assert.That(PauseMenuUI.IsPaused, Is.True);
        }
        finally
        {
            typeof(PlayerSessionData).GetProperty(nameof(PlayerSessionData.Instance)).SetValue(null, previousSession);
        }
    }

    [Test]
    public void BackReturnsOnePageAtATimeWithoutResuming()
    {
        menu.Open(); menu.OpenSettings(); menu.RequestApply(); menu.Back();
        Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.Settings));
        menu.Back();
        Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.Pause));
        Assert.That(Time.timeScale, Is.Zero);
    }

    [Test]
    public void DisablingWhileInAConfirmationRestoresThePreviousPauseState()
    {
        AudioListener.pause = true;
        menu.Open(); menu.OpenSettings(); menu.RequestApply();
        Call(menu, "OnDisable");
        Assert.That(PauseMenuUI.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(previousTime));
        Assert.That(AudioListener.pause, Is.True);
    }

    [Test]
    public void GameOverCannotOpenPause()
    {
        GameSession.IsGameOver = true;
        menu.Open();
        Assert.That(menu.Page, Is.EqualTo(PauseMenuPage.Closed));
    }

    [Test]
    public void HUDAndInventoryEntryPointsAreBlockedDuringPause()
    {
        var input = Child("Input").AddComponent<GridInputHandler>();
        Set(input, "isPlayerTurn", true);
        var actor = Child("Player").AddComponent<Character>();
        Set(input, "playerCharacter", actor);
        var inventory = Child("Inventory").AddComponent<HelmetInventoryUI>();
        Set(inventory, "gridInputHandler", input);
        menu.Open();
        Assert.That(input.CanEndTurn, Is.False);
        Assert.That(input.CanOpenHelmetInventory(), Is.False);
        Assert.That(input.CanOpenUpgradeShop(actor), Is.False);
        input.EndTurnFromUI();
        inventory.Toggle();
        Assert.That(inventory.IsOpen, Is.False);
    }

    [TestCase(1)]
    [TestCase(2)]
    public void PausedMovementPreservesFootprintAndEnergyThenCommitsExactlyOnce(int size)
    {
        var grid = Child("Grid").AddComponent<GridSystem>();
        Set(grid, "width", 6); Set(grid, "height", 6); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        var actor = Child("Actor").AddComponent<Character>();
        actor.enabled = false;
        Set(actor, "_grid", grid);
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size);
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(5, 2, 20, 8, 8));
        actor.Stats.SetCurrentEnergy(8);
        var movement = actor.gameObject.AddComponent<CharacterMovement>();
        Set(movement, "_grid", grid); Set(movement, "_character", actor);
        Vector2Int origin = new(1, 1);
        Vector2Int destination = new(2, 1);
        actor.SetGridPosition(origin); grid.RegisterCharacter(actor);
        actor.transform.position = grid.GridToWorld(origin, actor) + actor.VisualOffset;
        int commits = 0;
        var action = new InterruptibleRoutine(movement.MoveAlongRoutine(new List<Vector2Int> { destination },
            () => { commits++; actor.Stats.ConsumeEnergy(1); }, () => true));
        Assert.That(action.MoveNext(), Is.True);
        Vector3 position = actor.transform.position;
        int energy = actor.Stats.CurrentEnergy;
        menu.Open();
        for (int i = 0; i < 3; i++) Assert.That(action.MoveNext(), Is.True);
        Assert.That(commits, Is.Zero);
        Assert.That(actor.GridPosition, Is.EqualTo(origin));
        Assert.That(actor.transform.position, Is.EqualTo(position));
        Assert.That(actor.Stats.CurrentEnergy, Is.EqualTo(energy));
        foreach (Vector2Int cell in actor.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(actor));
        menu.Resume();
        actor.transform.position = grid.GridToWorld(destination, actor) + actor.VisualOffset;
        Assert.That(action.MoveNext(), Is.False);
        Assert.That(commits, Is.EqualTo(1));
        Assert.That(actor.Stats.CurrentEnergy, Is.EqualTo(energy - 1));
        Assert.That(actor.GridPosition, Is.EqualTo(destination));
        foreach (Vector2Int cell in actor.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(actor));
    }

    [Test]
    public void CancellingAPausedIteratorStillRunsItsCleanup()
    {
        bool cleaned = false;
        IEnumerator Action()
        {
            try { yield return null; yield return null; }
            finally { cleaned = true; }
        }
        var action = new InterruptibleRoutine(Action());
        action.MoveNext(); menu.Open(); action.Cancel();
        Assert.That(cleaned, Is.True);
        Assert.That(action.MoveNext(), Is.False);
    }

    [Test]
    public void PausingBeforeTheNextTurnDelaysItsEventsUntilContinue()
    {
        var actor = Child("Waiting actor").AddComponent<Character>();
        actor.enabled = false;
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(5, 2, 20, 8, 8));
        var turns = Child("Turns").AddComponent<TurnManager>();
        Set(turns, "turnOrder", new List<Character> { actor });
        var loop = (IEnumerator)Call(turns, "TurnLoop");
        var previous = GameEvents.OnTurnStarted;
        int started = 0;
        GameEvents.OnTurnStarted = _ => started++;
        try
        {
            menu.Open();
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(started, Is.Zero);
            Assert.That(turns.CurrentCharacter, Is.Null);
            menu.Resume();
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(started, Is.EqualTo(1));
            Assert.That(turns.CurrentCharacter, Is.SameAs(actor));
        }
        finally { GameEvents.OnTurnStarted = previous; (loop as System.IDisposable)?.Dispose(); }
    }

    [TestCase(1)]
    [TestCase(2)]
    public void PauseHoldsDeathCompletionAndOccupancyRemoval(int size)
    {
        var grid = Child("Death grid").AddComponent<GridSystem>();
        Set(grid, "width", 6); Set(grid, "height", 6); Set(grid, "cellSize", 1f);
        Call(grid, "GenerateGrid");
        var actor = Child("Dying actor").AddComponent<Character>();
        actor.enabled = false;
        Set(actor, "_grid", grid); Set(actor, "isPlayer", true);
        Set(actor, "gridWidth", size); Set(actor, "gridHeight", size);
        actor.SetGridPosition(new Vector2Int(1, 1)); grid.RegisterCharacter(actor);
        var routine = (IEnumerator)Call(actor, "DieRoutine");
        var previous = GameEvents.OnCharacterDeathCompleted;
        int deaths = 0;
        GameEvents.OnCharacterDeathCompleted = _ => deaths++;
        try
        {
            menu.Open();
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(deaths, Is.Zero);
            foreach (Vector2Int cell in actor.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.SameAs(actor));
            menu.Resume();
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(actor.gameObject.activeSelf, Is.False);
            foreach (Vector2Int cell in actor.GetOccupiedCells()) Assert.That(grid.GetCharacterAt(cell), Is.Null);
        }
        finally { GameEvents.OnCharacterDeathCompleted = previous; (routine as System.IDisposable)?.Dispose(); }
    }

    [Test]
    public void BrightnessUsesTheOriginalValuesWithoutMultiplyingRepeatedly()
    {
        settings.ApplyBrightness(0.6f); settings.ApplyBrightness(0.6f);
        Assert.That(light.intensity, Is.EqualTo(1.2f).Within(0.001f));
        settings.ApplyBrightness(0f);
        Assert.That(light.intensity, Is.Zero);
        settings.ApplyBrightness(1f);
        Assert.That(light.intensity, Is.EqualTo(2f));
    }

    [Test]
    public void GraphicsPresetsChangeARuntimeCloneAndPreserveTheSourceAsset()
    {
        sourcePipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        sourcePipeline.renderScale = 1f; sourcePipeline.shadowDistance = 50f;
        Set(settings, "originalPipeline", sourcePipeline);
        settings.ApplyGraphics(0);
        var runtime = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        Assert.That(runtime, Is.Not.SameAs(sourcePipeline));
        Assert.That(runtime.renderScale, Is.EqualTo(0.7f).Within(0.001f));
        Assert.That(runtime.msaaSampleCount, Is.EqualTo(1));
        settings.ApplyGraphics(2);
        Assert.That(runtime.renderScale, Is.EqualTo(1f));
        Assert.That(runtime.msaaSampleCount, Is.EqualTo(4));
        Assert.That(sourcePipeline.renderScale, Is.EqualTo(1f));
        Assert.That(sourcePipeline.shadowDistance, Is.EqualTo(50f));
    }

    [TestCase(0.5f, 110f)]
    [TestCase(1f, 120f)]
    [TestCase(2f, 140f)]
    public void SensitivityScalesPointerMovement(float sensitivity, float expected)
    {
        Vector2 result = RuntimeGameSettings.ScalePointerMovement(new Vector2(100, 100),
            new Vector2(120, 120), sensitivity, new Vector2(1280, 720));
        Assert.That(result, Is.EqualTo(new Vector2(expected, expected)));
    }

    [Test]
    public void SensitivityClampsThePointerToTheWindow()
    {
        Vector2 result = RuntimeGameSettings.ScalePointerMovement(new Vector2(100, 100),
            new Vector2(1000, -100), 2f, new Vector2(640, 480));
        Assert.That(result, Is.EqualTo(new Vector2(639, 0)));
    }

    [Test]
    public void ResolutionsIncludeCurrentSizeAndDoNotDuplicateRefreshRates()
    {
        List<Vector2Int> resolutions = RuntimeGameSettings.GetResolutions();
        Assert.That(resolutions, Does.Contain(new Vector2Int(Screen.width, Screen.height)));
        Assert.That(resolutions.Distinct().Count(), Is.EqualTo(resolutions.Count));
    }

    [Test]
    public void ApplyAndBackShareARowAndKeyboardNavigationStaysInsideTheModal()
    {
        menu.Open(); menu.OpenSettings();
        Button apply = FindButton("Aplicar"), back = FindButton("Volver");
        Assert.That(((RectTransform)apply.transform).anchoredPosition.y,
            Is.EqualTo(((RectTransform)back.transform).anchoredPosition.y));
        foreach (Selectable control in menu.GetComponentsInChildren<Selectable>())
        {
            Assert.That(control.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(control.navigation.selectOnDown.gameObject.activeInHierarchy, Is.True);
        }
    }

    [Test]
    public void SlidersStartAtFullBrightnessAndUnitSensitivityAndUseALinearScale()
    {
        menu.Open(); menu.OpenSettings();
        Slider brightness = FindSlider("Brillo");
        Slider sensitivity = FindSlider("Sensibilidad del ratón");
        Assert.That(brightness.minValue, Is.Zero);
        Assert.That(brightness.maxValue, Is.EqualTo(1f));
        Assert.That(brightness.value, Is.EqualTo(1f));
        Assert.That(sensitivity.minValue, Is.EqualTo(0.5f));
        Assert.That(sensitivity.maxValue, Is.EqualTo(2f));
        Assert.That(sensitivity.value, Is.EqualTo(1f));
        Assert.That(sensitivity.normalizedValue, Is.EqualTo(1f / 3f).Within(0.001f));
        sensitivity.normalizedValue = 0f;
        Assert.That(menu.PendingSensitivity, Is.EqualTo(0.5f));
        sensitivity.normalizedValue = 0.5f;
        Assert.That(menu.PendingSensitivity, Is.EqualTo(1.25f));
        sensitivity.normalizedValue = 1f;
        Assert.That(menu.PendingSensitivity, Is.EqualTo(2f));
        var texts = menu.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text).ToList();
        Assert.That(texts, Does.Contain("Brillo"));
        Assert.That(texts, Does.Contain("Sensibilidad del ratón"));
        Assert.That(texts.Any(t => t.Contains("Los cambios se realizan")), Is.False);
    }

    private Button FindButton(string name) => menu.GetComponentsInChildren<Button>().Single(b => b.name == name);
    private Slider FindSlider(string label) => menu.GetComponentsInChildren<Slider>().Single(s => s.name == label + " Slider");
    private void Click(string name) => FindButton(name).onClick.Invoke();
    private GameObject Child(string name)
    {
        var child = new GameObject(name); child.transform.SetParent(root.transform, false); return child;
    }
    private static void Set(object owner, string field, object value) => owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static object Call(object owner, string method) => owner.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
}
