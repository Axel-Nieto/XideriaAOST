using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingDamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;
    [SerializeField] private float duration = 1f;
    [SerializeField] private float moveSpeed = 1f;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TextMeshPro>();
    }

    public void Show(string value, Color color)
    {
        if (text == null)
        {
            Debug.LogError("FloatingDamageText no tiene TextMeshPro asignado.");
            return;
        }

        text.text = value;
        text.color = color;

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float timer = 0f;
        Vector3 start = transform.position;

        while (timer < duration)
        {
            transform.position = start + Vector3.up * (timer * moveSpeed);
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}