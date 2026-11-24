using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Runtime uGUI view, kept separate from pause and settings behavior.</summary>
public sealed class PauseMenuView : MonoBehaviour
{
    private GameObject pausePanel;
    private GameObject settingsPanel;
    private GameObject confirmationPanel;
    private TMP_Text resolutionValue;
    private TMP_Text qualityValue;
    private TMP_Text brightnessValue;
    private TMP_Text sensitivityValue;
    private Slider brightnessSlider;
    private Slider sensitivitySlider;
    private TMP_Text confirmationTitle;
    private TMP_Text confirmationMessage;
    private readonly Color panelColor = new(0.055f, 0.075f, 0.11f, 1f);
    private readonly Color buttonColor = new(0.14f, 0.21f, 0.29f, 1f);

    public static PauseMenuView Create(Transform parent, PauseMenuUI controller)
    {
        var host = new GameObject("Pause Menu", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseMenuView));
        host.transform.SetParent(parent, false);
        Canvas canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (EventSystem.current == null)
        {
            var eventHost = new GameObject("Pause Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventHost.transform.SetParent(host.transform, false);
        }
        var view = host.GetComponent<PauseMenuView>();
        view.Build(controller);
        host.SetActive(false);
        return view;
    }

    private void Build(PauseMenuUI controller)
    {
        RectTransform dimmer = Rect("Dimmer", transform, Vector2.zero, Vector2.zero);
        dimmer.anchorMin = Vector2.zero;
        dimmer.anchorMax = Vector2.one;
        dimmer.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

        pausePanel = Panel("Pause", 440, 370);
        Text("Title", pausePanel.transform, "Partida en pausa", new Vector2(0, 126), new Vector2(400, 52), 32);
        Button(pausePanel.transform, "Continuar", new Vector2(0, 48), new Vector2(340, 54), controller.Resume);
        Button(pausePanel.transform, "Ajustes", new Vector2(0, -22), new Vector2(340, 54), controller.OpenSettings);
        Button(pausePanel.transform, "Salir", new Vector2(0, -92), new Vector2(340, 54), controller.RequestExit);

        settingsPanel = Panel("Settings", 680, 520);
        Text("Title", settingsPanel.transform, "Ajustes", new Vector2(0, 207), new Vector2(600, 50), 32);
        resolutionValue = Selector(settingsPanel.transform, "Resolución de pantalla", 125, controller.ChangeResolution);
        qualityValue = Selector(settingsPanel.transform, "Calidad gráfica", 43, controller.ChangeQuality);
        brightnessSlider = SliderRow(settingsPanel.transform, "Brillo", -39, 0f, 1f, "0 %", "100 %",
            controller.SetBrightness, out brightnessValue);
        sensitivitySlider = SliderRow(settingsPanel.transform, "Sensibilidad del ratón", -121, 0.5f, 2f, "0,5", "2",
            controller.SetSensitivity, out sensitivityValue);
        Button(settingsPanel.transform, "Aplicar", new Vector2(-154, -206), new Vector2(280, 54), controller.RequestApply);
        Button(settingsPanel.transform, "Volver", new Vector2(154, -206), new Vector2(280, 54), controller.Back);

        confirmationPanel = Panel("Confirmation", 620, 330);
        confirmationTitle = Text("Title", confirmationPanel.transform, "", new Vector2(0, 108), new Vector2(570, 50), 30);
        confirmationMessage = Text("Message", confirmationPanel.transform, "", new Vector2(0, 12), new Vector2(550, 112), 23);
        Button(confirmationPanel.transform, "Confirmar", new Vector2(-141, -102), new Vector2(260, 54), controller.Confirm);
        Button(confirmationPanel.transform, "Cancelar", new Vector2(141, -102), new Vector2(260, 54), controller.Back);
    }

    public void Show(PauseMenuPage page, PauseMenuUI controller)
    {
        gameObject.SetActive(page != PauseMenuPage.Closed);
        pausePanel.SetActive(page == PauseMenuPage.Pause);
        settingsPanel.SetActive(page == PauseMenuPage.Settings);
        bool confirming = page == PauseMenuPage.ConfirmApply || page == PauseMenuPage.ConfirmExit;
        confirmationPanel.SetActive(confirming);
        if (page == PauseMenuPage.Settings) RefreshSettings(controller);
        if (confirming)
        {
            bool applying = page == PauseMenuPage.ConfirmApply;
            confirmationTitle.text = applying ? "Aplicar ajustes" : "Salir al menú principal";
            confirmationMessage.text = applying ? "¿Deseás aplicar los cambios de configuración?" :
                "¿Deseás finalizar la partida y volver al menú principal? Se perderá el progreso de esta partida.";
        }
        if (page == PauseMenuPage.Closed) return;
        // Explicit navigation confines keyboard/gamepad selection to the current modal.
        Selectable[] controls = GetComponentsInChildren<Selectable>();
        for (int i = 0; i < controls.Length; i++)
        {
            Selectable previous = controls[(i + controls.Length - 1) % controls.Length];
            Selectable next = controls[(i + 1) % controls.Length];
            // Leave horizontal navigation free for adjusting a slider with arrows.
            bool isSlider = controls[i] is Slider;
            controls[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = previous, selectOnLeft = isSlider ? null : previous,
                selectOnDown = next, selectOnRight = isSlider ? null : next
            };
        }
        if (EventSystem.current != null && controls.Length > 0)
        {
            // Destructive/application dialogs initially select Cancelar.
            EventSystem.current.SetSelectedGameObject(controls[confirming ? controls.Length - 1 : 0].gameObject);
        }
    }

    public void RefreshSettings(PauseMenuUI controller)
    {
        resolutionValue.text = $"{controller.PendingResolution.x} × {controller.PendingResolution.y}";
        qualityValue.text = new[] { "Baja", "Media", "Alta" }[controller.PendingQuality];
        brightnessSlider.SetValueWithoutNotify(controller.PendingBrightness);
        sensitivitySlider.SetValueWithoutNotify(controller.PendingSensitivity);
        brightnessValue.text = Mathf.RoundToInt(controller.PendingBrightness * 100f) + " %";
        sensitivityValue.text = controller.PendingSensitivity.ToString("0.##",
            System.Globalization.CultureInfo.GetCultureInfo("es-AR"));
    }

    private GameObject Panel(string name, float width, float height)
    {
        RectTransform panel = Rect(name, transform, Vector2.zero, new Vector2(width, height));
        panel.gameObject.AddComponent<Image>().color = panelColor;
        return panel.gameObject;
    }

    private TMP_Text Selector(Transform parent, string label, float y, Action<int> change)
    {
        TMP_Text caption = Text(label, parent, label, new Vector2(-156, y), new Vector2(280, 56), 22);
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        Button(parent, "<", new Vector2(23, y), new Vector2(44, 48), () => change(-1));
        TMP_Text value = Text(label + " Value", parent, "", new Vector2(163, y), new Vector2(218, 50), 23);
        Button(parent, ">", new Vector2(303, y), new Vector2(44, 48), () => change(1));
        return value;
    }

    private Slider SliderRow(Transform parent, string label, float y, float minimum, float maximum,
        string minimumText, string maximumText, UnityAction<float> changed, out TMP_Text value)
    {
        TMP_Text caption = Text(label, parent, label, new Vector2(-156, y), new Vector2(280, 56), 22);
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        value = Text(label + " Value", parent, "", new Vector2(163, y + 22), new Vector2(218, 24), 21);
        Text(label + " Minimum", parent, minimumText, new Vector2(43, y - 28), new Vector2(80, 20), 16);
        Text(label + " Maximum", parent, maximumText, new Vector2(283, y - 28), new Vector2(80, 20), 16);

        RectTransform root = Rect(label + " Slider", parent, new Vector2(163, y - 5), new Vector2(280, 28));
        // A transparent hit area makes the full row height usable for clicks/drags.
        root.gameObject.AddComponent<Image>().color = Color.clear;
        RectTransform track = Rect("Track", root, Vector2.zero, new Vector2(280, 8));
        track.gameObject.AddComponent<Image>().color = buttonColor;
        RectTransform fill = Rect("Fill", track, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.62f, 0.82f);
        RectTransform handleArea = Rect("Handle Area", root, Vector2.zero, new Vector2(264, 28));
        RectTransform handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(16, 0));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.88f, 0.94f, 1f);
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(changed);
        return slider;
    }

    private void Button(Transform parent, string caption, Vector2 position, Vector2 size, UnityAction action)
    {
        RectTransform rect = Rect(caption, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = buttonColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0f;
        button.colors = colors;
        button.onClick.AddListener(action);
        Text("Label", rect, caption, Vector2.zero, size - new Vector2(12, 4), 23);
    }

    private TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = Rect(name, parent, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.94f, 0.96f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
