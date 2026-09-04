using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BattleHUDThemeController : MonoBehaviour
{
    [System.Serializable]
    public class HelmetUITheme
    {
        public HelmetType helmetType;

        [Header("Theme")]
        public Color color = Color.white;

        [Header("Helmet Images")]
        public Sprite helmetImage;
        public Sprite helmetSymbol;
    }

    [Header("Themes")]
    [SerializeField] private List<HelmetUITheme> themes = new();

    [Header("Helmet UI")]
    [SerializeField] private Image helmetImage;
    [SerializeField] private Image helmetSymbol;

    [Header("Materials")]
    [SerializeField] private Material helmetMaterial;
    [SerializeField] private Material statsMaterial;
    [SerializeField] private Material buttonMaterial;

    [Header("Images using Helmet material")]
    [SerializeField] private List<Image> helmetMaterialImages = new();

    [Header("Images using Stats material")]
    [SerializeField] private List<Image> statsMaterialImages = new();

    [Header("Images using Button material")]
    [SerializeField] private List<Image> buttonMaterialImages = new();

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 0.4f;
    [SerializeField] private float normalGrad = 1.2f;
    [SerializeField] private float hiddenGrad = -1f;

    private Material runtimeHelmetMaterial;
    private Material runtimeStatsMaterial;
    private Material runtimeButtonMaterial;

    private Coroutine transitionRoutine;

    private HelmetType currentHelmetType = HelmetType.Default;

    private static readonly int GradProperty =
        Shader.PropertyToID("_Grad");

    private static readonly int ActualColorProperty =
        Shader.PropertyToID("_Actual_Color");

    private static readonly int NextColorProperty =
        Shader.PropertyToID("_Next_Color");

    private void Awake()
    {
        Debug.Log("BattleHUDThemeController AWAKE");
        CreateRuntimeMaterials();
        ApplyRuntimeMaterials();

        HelmetUITheme defaultTheme =
            GetTheme(HelmetType.Default);

        if (defaultTheme != null)
        {
            SetActualColor(defaultTheme.color);
            SetNextColor(defaultTheme.color);

            SetGrad(hiddenGrad);

            ApplySprites(defaultTheme);
        }
    }

    private void CreateRuntimeMaterials()
    {
        if (helmetMaterial != null)
            runtimeHelmetMaterial = new Material(helmetMaterial);

        if (statsMaterial != null)
            runtimeStatsMaterial = new Material(statsMaterial);

        if (buttonMaterial != null)
            runtimeButtonMaterial = new Material(buttonMaterial);
    }

    private void ApplyRuntimeMaterials()
    {

        foreach (Image image in helmetMaterialImages)
        {
            if (image != null && runtimeHelmetMaterial != null)
                image.material = runtimeHelmetMaterial;
        }

        foreach (Image image in statsMaterialImages)
        {
            if (image != null && runtimeStatsMaterial != null)
                image.material = runtimeStatsMaterial;
        }

        foreach (Image image in buttonMaterialImages)
        {
            if (image != null && runtimeButtonMaterial != null)
                image.material = runtimeButtonMaterial;
        }
    }

    public void SetHelmetTheme(
    HelmetType helmetType,
    bool animate = true)
    {
        HelmetUITheme newTheme = GetTheme(helmetType);

        if (newTheme == null)
        {
            Debug.LogWarning(
                $"No existe UI Theme para {helmetType}"
            );
            return;
        }

        if (!animate)
        {
            ApplyImmediately(newTheme);
            currentHelmetType = helmetType;
            return;
        }

        if (helmetType == currentHelmetType)
            return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine =
            StartCoroutine(
                ChangeThemeRoutine(
                    newTheme,
                    helmetType
                )
            );
    }

    private IEnumerator ChangeThemeRoutine(
    HelmetUITheme newTheme,
    HelmetType newHelmetType)
    {
        HelmetUITheme currentTheme =
            GetTheme(currentHelmetType);

        Color oldColor =
            currentTheme != null
                ? currentTheme.color
                : Color.white;

        // Preparamos los dos colores
        SetActualColor(oldColor);
        SetNextColor(newTheme.color);

        // Barrido de entrada invertido
        yield return AnimateGrad(
            hiddenGrad,
            normalGrad
        );

        // Cuando ya está completamente mostrado el nuevo color
        ApplySprites(newTheme);

        SetActualColor(newTheme.color);

        // Mantenemos el nuevo como destino también
        SetNextColor(newTheme.color);

        // Regresamos el barrido
        yield return AnimateGrad(
            normalGrad,
            hiddenGrad
        );

        currentHelmetType = newHelmetType;
        transitionRoutine = null;
    }

    private IEnumerator AnimateGrad(float from,float to)
    {
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / transitionDuration);

            SetGrad(Mathf.Lerp(from, to, t));

            yield return null;
        }

        SetGrad(to);
    }

    private void ApplyImmediately(HelmetUITheme theme)
    {
        SetActualColor(theme.color);
        SetNextColor(theme.color);

        SetGrad(hiddenGrad);

        ApplySprites(theme);
    }

    private void ApplySprites(
        HelmetUITheme theme)
    {
        if (helmetImage != null)
            helmetImage.sprite = theme.helmetImage;

        if (helmetSymbol != null)
            helmetSymbol.sprite = theme.helmetSymbol;
    }

    private void SetGrad(float value)
    {
        SetFloatOnAllMaterials(GradProperty,value);
    }

    private void SetActualColor(Color color)
    {
        SetColorOnAllMaterials(ActualColorProperty,color);
    }

    private void SetNextColor(Color color)
    {
        SetColorOnAllMaterials(NextColorProperty,color);
    }

    private void SetFloatOnAllMaterials(int property,float value)
    {
        if (runtimeHelmetMaterial != null && runtimeHelmetMaterial.HasProperty(property))
        {
            runtimeHelmetMaterial.SetFloat(property,value);
        }

        if (runtimeStatsMaterial != null && runtimeStatsMaterial.HasProperty(property))
        {
            runtimeStatsMaterial.SetFloat(property,value);
        }

        if (runtimeButtonMaterial != null && runtimeButtonMaterial.HasProperty(property))
        {
            runtimeButtonMaterial.SetFloat(property,value);
        }
    }

    private void SetColorOnAllMaterials(int property, Color color)
    {
        if (runtimeHelmetMaterial != null && runtimeHelmetMaterial.HasProperty(property))
        {
            runtimeHelmetMaterial.SetColor(property,color);
        }

        if (runtimeStatsMaterial != null && runtimeStatsMaterial.HasProperty(property))
        {
            runtimeStatsMaterial.SetColor(property,color);
        }

        if (runtimeButtonMaterial != null && runtimeButtonMaterial.HasProperty(property))
        {
            runtimeButtonMaterial.SetColor(property,color);
        }
    }

    private HelmetUITheme GetTheme(HelmetType type)
    {
        foreach (HelmetUITheme theme in themes)
        {
            if (theme.helmetType == type)
                return theme;
        }

        return null;
    }

    public void RefreshFromHelmet(HelmetData helmetData, bool animate = true)
    {
        if (helmetData == null)
        {
            SetHelmetTheme(HelmetType.Default, animate);
            return;
        }

        SetHelmetTheme(helmetData.Type, animate);
    }
}