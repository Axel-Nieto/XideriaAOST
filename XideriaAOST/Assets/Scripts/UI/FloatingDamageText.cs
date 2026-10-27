using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingDamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;
    [SerializeField] private float duration = 1f;
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField, Min(0f)] private float appearanceDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float initialScale = 0.5f;
    private Camera mainCamera;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TextMeshPro>();
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
            return;

        transform.forward = mainCamera.transform.forward;
    }

    public void Show(string value, Color color, float delay = 0f, float sizeMultiplier = 1f)
    {
        if (text == null)
        {
            Debug.LogError("FloatingDamageText no tiene TextMeshPro asignado.");
            return;
        }

        text.text = value;
        text.color = color;
        text.enabled = false;

        StartCoroutine(Animate(delay, sizeMultiplier));
    }

    private IEnumerator Animate(float delay, float sizeMultiplier)
    {
        // This coroutine belongs to the text, so queued hits remain visible
        // even if their character is disabled by its death sequence.
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float timer = 0f;
        Vector3 start = transform.position;
        Vector3 fullScale = transform.localScale * sizeMultiplier;
        Color color = text.color;
        float appearTime = Mathf.Min(appearanceDuration, duration);
        text.enabled = true;

        while (timer < duration)
        {
            float progress = appearTime > 0f ? Mathf.Clamp01(timer / appearTime) : 1f;
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            float growthProgress = Mathf.Clamp01(timer / duration);

            transform.position = start + Vector3.up * (timer * moveSpeed);
            transform.localScale = fullScale * Mathf.Lerp(initialScale, 1f, growthProgress);
            text.color = new Color(color.r, color.g, color.b, color.a * easedProgress);
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
