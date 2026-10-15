using UnityEngine;

public class FloatingDamageSpawner : MonoBehaviour
{
    [SerializeField] private FloatingDamageText prefab;
    [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);
    [SerializeField, Min(0f)] private float energyTextHeight = 0.35f;

    public void ShowDamage(int damage, DamageType damageType)
    {
        var instance = Instantiate(prefab, transform.position + offset, Quaternion.identity);

        instance.Show(damage.ToString(), GetColor(damageType));
    }

    public void ShowHeal(int amount)
    {
        var instance = Instantiate(prefab, transform.position + offset, Quaternion.identity);

        instance.Show(amount.ToString(), Color.green);
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

        var instance = Instantiate(prefab, position, Quaternion.identity);
        instance.Show("+" + amount, color);
    }

    public void ShowMiss()
    {
        var instance = Instantiate(prefab, transform.position + offset, Quaternion.identity);

        instance.Show("MISS", Color.gray);
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
