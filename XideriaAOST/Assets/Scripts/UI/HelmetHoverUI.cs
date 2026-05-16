using TMPro;
using UnityEngine;

public class HelmetHoverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI hoverText;

    public void Show(string text, Vector3 screenPosition)
    {
        gameObject.SetActive(true);

        hoverText.text = text;

        transform.position = screenPosition;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}