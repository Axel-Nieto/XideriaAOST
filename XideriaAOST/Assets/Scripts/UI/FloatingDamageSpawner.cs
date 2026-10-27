using UnityEngine;

public class FloatingDamageSpawner : MonoBehaviour
{
    [SerializeField] private FloatingDamageText prefab;
    [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);
    [SerializeField, Min(0f)] private float energyTextHeight = 0.35f;
    [SerializeField, Min(0f)] private float textInterval = 0.4f;
    [SerializeField, Range(0.1f, 1f)] private float damageTextScale = 0.8f;

    private float nextTextTime;

    public void ShowDamage(int damage, DamageType damageType)
    {
        SpawnText(damage.ToString(), GetColor(damageType), transform.position + offset, damageTextScale);
    }

    public void ShowHeal(int amount)
    {
        SpawnText(amount.ToString(), Color.green, transform.position + offset);
    }

    public void ShowEnergyRecovery(int amount, Color color, Transform headAnchor)
    {
        if (amount <= 0)
            return;

        if (prefab == null)
        {
            Debug.LogWarning("No se puede mostrar la recuperación de energía: falta FloatingDamageText.", this);
            return;
        }

        if (headAnchor == null)
            Debug.LogWarning("Falta el ancla de cabeza; se usará la posición habitual del texto flotante.", this);

        Vector3 position = headAnchor != null
            ? headAnchor.position + Vector3.up * energyTextHeight
            : transform.position + offset;

        SpawnText("+" + amount, color, position);
    }

    public void ShowMiss()
    {
        SpawnText("MISS", Color.gray, transform.position + offset);
    }

    private void SpawnText(string value, Color color, Vector3 position, float sizeMultiplier = 1f)
    {
        if (prefab == null)
        {
            Debug.LogWarning("No se puede mostrar el texto flotante: falta FloatingDamageText.", this);
            return;
        }

        // Reserve a display time per character without delaying combat logic.
        float showTime = Mathf.Max(Time.time, nextTextTime);
        nextTextTime = showTime + textInterval;

        var instance = Instantiate(prefab, position, Quaternion.identity);
        instance.Show(value, color, showTime - Time.time, sizeMultiplier);
    }

    private Color GetColor(DamageType type)
    {
        return type switch
        {
            DamageType.Physical => Color.white,
            DamageType.Fire => Color.red,
            DamageType.Ice => Color.cyan,
            DamageType.Water => Color.blue,
            DamageType.Poison => Color.darkOliveGreen,
            _ => Color.white
        };
    }
}
