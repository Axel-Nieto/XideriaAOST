using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public void SavePlayerSession()
{
    if (!IsPlayer || PlayerSessionData.Instance == null)
        return;

    var session = PlayerSessionData.Instance;

    session.currentHP = Stats.CurrentHP;
    session.currentEnergy = Stats.CurrentEnergy;

    session.hasHelmet = EquippedHelmet != null;

    if (EquippedHelmet != null)
        session.equippedHelmetType = EquippedHelmet.Type;

    session.skillCooldowns.Clear();

    foreach (var skill in GetActiveSkills())
    {
        session.skillCooldowns[skill.Name] = GetSkillCooldown(skill);
    }
}
