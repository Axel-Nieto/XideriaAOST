using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private Material curtainMaterial;
    [SerializeField] private string curtainPropertyName = "_cortina";

    [SerializeField] private float closeDuration = 0.8f;
    [SerializeField] private float openDuration = 0.8f;
    [SerializeField] private float minimumClosedTime = 0.4f;
    [SerializeField] private GameObject gameplayHUD;
    private Canvas currentHudCanvas;

    private bool isTransitioning;

    private const float OpenValue = 1f;
    private const float ClosedValue = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetCurtain(OpenValue);
    }

    public void LoadScene(string sceneName)
    {
        if (isTransitioning)
            return;

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;

        SetHUDVisible(false);

        yield return AnimateCurtain(OpenValue, ClosedValue, closeDuration);

        float closedStartTime = Time.unscaledTime;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
            yield return null;

        float elapsedClosedTime = Time.unscaledTime - closedStartTime;

        if (elapsedClosedTime < minimumClosedTime)
            yield return new WaitForSecondsRealtime(minimumClosedTime - elapsedClosedTime);

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        yield return null;

        currentHudCanvas = null;
        SetHUDVisible(false);

        yield return AnimateCurtain(ClosedValue, OpenValue, openDuration);

        SetHUDVisible(true);

        isTransitioning = false;
    }

    private void FindHUD()
    {
        GameObject hud = GameObject.FindWithTag("GameplayHUD");

        if (hud != null)
            currentHudCanvas = hud.GetComponent<Canvas>();
    }

    private void SetHUDVisible(bool visible)
    {
        if (currentHudCanvas == null)
            FindHUD();

        if (currentHudCanvas != null)
            currentHudCanvas.enabled = visible;
    }

    private IEnumerator AnimateCurtain(float from, float to, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            float t = timer / duration;
            SetCurtain(Mathf.Lerp(from, to, t));

            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        SetCurtain(to);
    }

    private void SetCurtain(float value)
    {
        curtainMaterial.SetFloat(curtainPropertyName, value);
    }
}