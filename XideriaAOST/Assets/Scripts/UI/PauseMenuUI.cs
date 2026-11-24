using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum PauseMenuPage { Closed, Pause, Settings, ConfirmApply, ConfirmExit }

/// <summary>Created by GridInputHandler on playable maps; no scene wiring required.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }
    private static int closedFrame = -1;
    public static bool IsPaused => Instance != null && Instance.Page != PauseMenuPage.Closed;
    public static bool BlocksGameplayInput => IsPaused || closedFrame == Time.frameCount;
    public PauseMenuPage Page { get; private set; }
    public Vector2Int PendingResolution { get; private set; }
    public int PendingQuality { get; private set; }
    public float PendingBrightness { get; private set; }
    public float PendingSensitivity { get; private set; }
    private List<Vector2Int> resolutions;
    private int resolutionIndex;
    private float previousTimeScale;
    private bool previousAudioPause;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private GameObject previousSelection;
    private PauseMenuView view;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; closedFrame = -1; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        RuntimeGameSettings.EnsureCreated();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (Page == PauseMenuPage.Closed) Open();
        else if (Page == PauseMenuPage.Pause) Resume();
        else Back();
    }

    public void Open()
    {
        if (IsPaused || GameSession.IsGameOver ||
            (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)) return;
        if (view == null) view = PauseMenuView.Create(transform, this);
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Show(PauseMenuPage.Pause);
    }

    public void Resume()
    {
        if (Page == PauseMenuPage.Closed) return;
        Show(PauseMenuPage.Closed);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        closedFrame = Time.frameCount;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
    }

    public void OpenSettings()
    {
        if (Page != PauseMenuPage.Pause) return;
        var settings = RuntimeGameSettings.EnsureCreated();
        resolutions = RuntimeGameSettings.GetResolutions();
        PendingResolution = new Vector2Int(Screen.width, Screen.height);
        resolutionIndex = resolutions.IndexOf(PendingResolution);
        PendingQuality = settings.GraphicsQuality;
        PendingBrightness = settings.Brightness;
        PendingSensitivity = settings.MouseSensitivity;
        Show(PauseMenuPage.Settings);
    }

    public void ChangeResolution(int direction)
    {
        if (Page != PauseMenuPage.Settings) return;
        resolutionIndex = (resolutionIndex + direction + resolutions.Count) % resolutions.Count;
        PendingResolution = resolutions[resolutionIndex];
        view.RefreshSettings(this);
    }

    public void ChangeQuality(int direction)
    {
        if (Page != PauseMenuPage.Settings) return;
        PendingQuality = (PendingQuality + direction + 3) % 3;
        view.RefreshSettings(this);
    }

    public void SetBrightness(float value)
    {
        if (Page != PauseMenuPage.Settings) return;
        PendingBrightness = Mathf.Round(Mathf.Clamp01(value) * 100f) / 100f;
        view.RefreshSettings(this);
    }

    public void RequestApply()
    {
        if (Page == PauseMenuPage.Settings) Show(PauseMenuPage.ConfirmApply);
    }

    public void SetSensitivity(float value)
    {
        if (Page != PauseMenuPage.Settings) return;
        PendingSensitivity = Mathf.Round(Mathf.Clamp(value, 0.5f, 2f) * 100f) / 100f;
        view.RefreshSettings(this);
    }

    public void RequestExit()
    {
        if (Page == PauseMenuPage.Pause) Show(PauseMenuPage.ConfirmExit);
    }

    public void Confirm()
    {
        if (Page == PauseMenuPage.ConfirmApply)
        {
            RuntimeGameSettings.EnsureCreated().Apply(PendingResolution, PendingQuality, PendingBrightness, PendingSensitivity);
            Show(PauseMenuPage.Settings);
        }
        else if (Page == PauseMenuPage.ConfirmExit)
        {
            PlayerSessionData.Instance?.ResetSession();
            Resume();
            if (SceneTransitionManager.Instance != null) SceneTransitionManager.Instance.LoadScene("Inicio");
            else SceneManager.LoadScene("Inicio");
        }
    }

    public void Back()
    {
        if (Page == PauseMenuPage.ConfirmApply) Show(PauseMenuPage.Settings);
        else if (Page == PauseMenuPage.Settings || Page == PauseMenuPage.ConfirmExit) Show(PauseMenuPage.Pause);
    }

    private void Show(PauseMenuPage page)
    {
        Page = page;
        view?.Show(page, this);
    }

    private void OnDisable()
    {
        if (Instance != this) return;
        Resume();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Resume();
        Instance = null;
    }
}
