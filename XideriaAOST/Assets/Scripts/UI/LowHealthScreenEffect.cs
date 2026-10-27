using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Uses URP's existing fullscreen pass without changing the source material.
public sealed class LowHealthScreenEffect : FullScreenPassRendererFeature
{
    private const string IntensityProperty = "_VignetteInten";

    private Material sourceMaterial;
    private Material runtimeMaterial;
    private Character cachedPlayer;
    private int cachedSceneHandle = -1;

    public override void Create()
    {
        base.Create();
        ReleaseMaterial();
        cachedPlayer = null;
        cachedSceneHandle = -1;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Application.isPlaying ||
            renderingData.cameraData.cameraType != CameraType.Game ||
            renderingData.cameraData.renderType == CameraRenderType.Overlay ||
            IsExcludedScene(SceneManager.GetActiveScene().name) ||
            (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning))
        {
            return;
        }

        Scene scene = renderingData.cameraData.camera.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded || IsExcludedScene(scene.name))
            return;

        Character player = FindPlayer(scene);
        if (player == null || !player.gameObject.activeInHierarchy || player.Stats == null)
            return;

        float intensity = CalculateIntensity(player.Stats.CurrentHP, player.Stats.MaxHP);
        if (intensity <= 0f || !PrepareMaterial())
            return;

        runtimeMaterial.SetFloat(IntensityProperty, intensity);

        // URP keeps the runtime material in its render pass. Restore the serialized
        // reference immediately so the renderer asset is never changed by gameplay.
        Material original = passMaterial;
        try
        {
            passMaterial = runtimeMaterial;
            base.AddRenderPasses(renderer, ref renderingData);
        }
        finally
        {
            passMaterial = original;
        }
    }

    private Character FindPlayer(Scene scene)
    {
        if (cachedSceneHandle != scene.handle || cachedPlayer == null ||
            !cachedPlayer.gameObject.activeInHierarchy || cachedPlayer.gameObject.scene != scene)
        {
            cachedPlayer = null;
            cachedSceneHandle = scene.handle;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Character character in root.GetComponentsInChildren<Character>())
                {
                    if (character.IsPlayer)
                    {
                        cachedPlayer = character;
                        return cachedPlayer;
                    }
                }
            }
        }

        return cachedPlayer;
    }

    private bool PrepareMaterial()
    {
        if (passMaterial == null)
            return false;

        if (sourceMaterial != passMaterial || runtimeMaterial == null)
        {
            ReleaseMaterial();
            if (!passMaterial.HasProperty(IntensityProperty))
            {
                Debug.LogWarning("LowHealthScreenEffect requires a material with _VignetteInten.", this);
                return false;
            }

            sourceMaterial = passMaterial;
            runtimeMaterial = new Material(sourceMaterial)
            {
                name = sourceMaterial.name + " (Low Health Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return true;
    }

    internal static float CalculateIntensity(int currentHP, int maxHP)
    {
        if (maxHP <= 0)
            return 0f;

        float health = (float)currentHP / maxHP;
        if (health >= 1f)
            return 0f;
        if (health > 0.3f)
            return (1f - health) / 0.7f;
        if (health > 0.2f)
            return 1f + (0.3f - health) / 0.1f;
        if (health > 0.1f)
            return 2f + (0.2f - health) / 0.1f;
        if (health > 0.05f)
            return 3f + (0.1f - health) / 0.05f;

        return 4f;
    }

    internal static bool IsExcludedScene(string sceneName)
    {
        return string.Equals(sceneName, "Inicio", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sceneName, "Lobby", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sceneName, "Tutorial", StringComparison.OrdinalIgnoreCase);
    }

    protected override void Dispose(bool disposing)
    {
        ReleaseMaterial();
        cachedPlayer = null;
        base.Dispose(disposing);
    }

    private void ReleaseMaterial()
    {
        CoreUtils.Destroy(runtimeMaterial);
        runtimeMaterial = null;
        sourceMaterial = null;
    }
}
