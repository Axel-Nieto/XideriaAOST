using UnityEngine;

public class CharacterColorByHelmet : MonoBehaviour
{
    [SerializeField] private Material teteMaterial;
    [SerializeField] private string colorPropertyName = "_ColorTete";

    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color fireHelmetColor = new Color(255f, 76f, 0f);
    [SerializeField] private Color waterHelmetColor = new Color(0f, 134f, 255f);

    public void ApplyHelmetColor(HelmetData helmet)
    {
        if (helmet == null)
        {
            SetColor(defaultColor);
            return;
        }

        switch (helmet.Name)
        {
            case "Casco de Fuego":
                SetColor(fireHelmetColor);
                break;

            case "Casco de Agua":
                SetColor(waterHelmetColor);
                break;

            default:
                SetColor(defaultColor);
                break;
        }
    }

    private void SetColor(Color color)
    {
        if (teteMaterial == null)
            return;

        teteMaterial.SetColor(colorPropertyName, color);
    }

    private void OnDisable()
    {
        SetColor(defaultColor);
    }

    private void OnDestroy()
    {
        SetColor(defaultColor);
    }
}