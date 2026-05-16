using UnityEngine;

public class FloatingDamageSpawner : MonoBehaviour
{
    [SerializeField] private FloatingDamageText prefab;
    [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);

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