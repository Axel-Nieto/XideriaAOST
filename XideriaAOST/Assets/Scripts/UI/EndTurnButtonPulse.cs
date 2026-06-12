using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EndTurnButtonPulse : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pulseColor = Color.yellow;
    [SerializeField] private float pulseSpeed = 3f;

    private bool isPulsing;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null)
            normalColor = targetImage.color;
    }

    private void Update()
    {
        if (!isPulsing || targetImage == null)
            return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        targetImage.color = Color.Lerp(normalColor, pulseColor, t);
    }

    public void SetPulsing(bool value)
    {
        isPulsing = value;

        if (!isPulsing && targetImage != null)
            targetImage.color = normalColor;
    }
}
