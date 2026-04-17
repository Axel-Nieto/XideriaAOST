using UnityEngine;
using UnityEngine.TextCore.Text;

public class DamageSystem
{
    public int CalculateDamage(Character attacker, Character target)
    {
        int raw = attacker.Stats.Attack - target.Stats.Defense;
        return Mathf.Max(1, raw);
    }
}
