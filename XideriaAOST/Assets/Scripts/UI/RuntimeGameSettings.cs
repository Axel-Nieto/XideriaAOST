using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Session-only settings. Render pipeline assets are cloned, never edited.</summary>
public sealed class RuntimeGameSettings : MonoBehaviour
{
    public static RuntimeGameSettings Instance { get; private set; }
    public int GraphicsQuality { get; private set; } = 2;
    public float Brightness { get; private set; } = 1f;
    public float MouseSensitivity { get; private set; } = 1f;
    private UniversalRenderPipelineAsset originalPipeline;
    private UniversalRenderPipelineAsset runtimePipeline;
    private RenderPipelineAsset previousOverride;
    private readonly Dictionary<Light, float> lightIntensities = new();
    private float ambientIntensity;
    private Color ambientLight;
    private Color ambientSky;
    private Color ambientEquator;
    private Color ambientGround;
    private bool brightnessApplied;
    private bool cursorAnchored;
    private Vector2 cursorPosition;
    private Vector2 cursorRemainder;

    public static RuntimeGameSettings EnsureCreated()
    {
        if (Instance == null)
        {
            var host = new GameObject("Runtime Game Settings");
            host.AddComponent<RuntimeGameSettings>();
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        originalPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        previousOverride = QualitySettings.renderPipeline;
        CaptureLighting();
        SceneManager.sceneLoaded += OnSceneLoaded;
        InputSystem.onAfterUpdate += AdjustPointer;
    }

    public static List<Vector2Int> GetResolutions()
    {
        var result = new List<Vector2Int>();
        foreach (Resolution mode in Screen.resolutions)
        {
            var size = new Vector2Int(mode.width, mode.height);
            if (!result.Contains(size)) result.Add(size);
        }
        var current = new Vector2Int(Screen.width, Screen.height);
        if (!result.Contains(current)) result.Add(current);
        result.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return result;
    }

    public void Apply(Vector2Int resolution, int graphicsQuality, float brightness, float mouseSensitivity)
    {
        ApplyGraphics(graphicsQuality);
        ApplyBrightness(brightness);
        MouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.5f, 2f);
        cursorAnchored = false;
        if (resolution.x > 0 && resolution.y > 0 &&
            (resolution.x != Screen.width || resolution.y != Screen.height))
            Screen.SetResolution(resolution.x, resolution.y, Screen.fullScreenMode);
    }

    public void ApplyGraphics(int quality)
    {
        GraphicsQuality = Mathf.Clamp(quality, 0, 2);
        if (originalPipeline == null) return;
        if (runtimePipeline == null)
        {
            runtimePipeline = Instantiate(originalPipeline);
            runtimePipeline.name = originalPipeline.name + " (Runtime Settings)";
            runtimePipeline.hideFlags = HideFlags.HideAndDontSave;
        }
        // These controls affect URP even when the build includes only the PC quality level.
        runtimePipeline.renderScale = originalPipeline.renderScale * new[] { 0.7f, 0.85f, 1f }[GraphicsQuality];
        runtimePipeline.msaaSampleCount = GraphicsQuality == 0 ? 1 : GraphicsQuality == 1 ? 2 : 4;
        runtimePipeline.shadowDistance = originalPipeline.shadowDistance * new[] { 0.35f, 0.65f, 1f }[GraphicsQuality];
        QualitySettings.renderPipeline = runtimePipeline;
    }

    private void CaptureLighting()
    {
        lightIntensities.Clear();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            lightIntensities[light] = light.intensity;
        ambientIntensity = RenderSettings.ambientIntensity;
        ambientLight = RenderSettings.ambientLight;
        ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
    }

    public void ApplyBrightness(float brightness)
    {
        Brightness = Mathf.Clamp01(brightness);
        brightnessApplied = true;
        float multiplier = Brightness;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!lightIntensities.TryGetValue(light, out float intensity))
                lightIntensities[light] = intensity = light.intensity;
            light.intensity = intensity * multiplier;
        }
        RenderSettings.ambientIntensity = ambientIntensity * multiplier;
        RenderSettings.ambientLight = ambientLight * multiplier;
        RenderSettings.ambientSkyColor = ambientSky * multiplier;
        RenderSettings.ambientEquatorColor = ambientEquator * multiplier;
        RenderSettings.ambientGroundColor = ambientGround * multiplier;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        CaptureLighting();
        if (brightnessApplied) ApplyBrightness(Brightness);
    }

    public static Vector2 ScalePointerMovement(Vector2 previous, Vector2 sampled, float sensitivity, Vector2 bounds)
    {
        Vector2 target = previous + (sampled - previous) * sensitivity;
        return new Vector2(Mathf.Clamp(target.x, 0, Mathf.Max(0, bounds.x - 1)),
            Mathf.Clamp(target.y, 0, Mathf.Max(0, bounds.y - 1)));
    }

    private void OnApplicationFocus(bool focused) => cursorAnchored = false;

    private void AdjustPointer()
    {
        if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        Mouse mouse = Mouse.current;
        if (mouse == null || !Application.isFocused || Cursor.lockState != CursorLockMode.None ||
            Mathf.Approximately(MouseSensitivity, 1f))
        {
            cursorAnchored = false;
            return;
        }
        Vector2 sampled = mouse.position.ReadValue();
        // Do not pull a cursor entering/leaving the window back into the game.
        if (sampled.x < 0 || sampled.y < 0 || sampled.x >= Screen.width || sampled.y >= Screen.height)
        {
            cursorAnchored = false;
            return;
        }
        if (!cursorAnchored)
        {
            cursorPosition = sampled;
            cursorRemainder = Vector2.zero;
            cursorAnchored = true;
            return;
        }
        if (sampled == cursorPosition) return;
        Vector2 target = ScalePointerMovement(cursorPosition, sampled, MouseSensitivity,
            new Vector2(Screen.width, Screen.height)) + cursorRemainder;
        // Hardware cursor coordinates are integral; sync input state so both uGUI and
        // grid raycasts observe the same position before their Update callbacks.
        Vector2 rounded = new Vector2(Mathf.Round(target.x), Mathf.Round(target.y));
        cursorRemainder = target - rounded;
        target = rounded;
        if (target != sampled)
        {
            mouse.WarpCursorPosition(target);
            InputState.Change(mouse.position, target);
        }
        cursorPosition = target;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        InputSystem.onAfterUpdate -= AdjustPointer;
        if (QualitySettings.renderPipeline == runtimePipeline)
            QualitySettings.renderPipeline = previousOverride;
        if (runtimePipeline != null) CoreUtils.Destroy(runtimePipeline);
        Instance = null;
    }
}
