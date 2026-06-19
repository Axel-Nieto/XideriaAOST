using System.Collections;
using UnityEngine;

public static class BasicAttackVFX
{
    public static IEnumerator Play(
    Character target,
    DamageType damageType)
    {
        if (SkillVFXLibrary.Instance == null || target == null)
            yield break;

        Transform anchor = target.GetGroundAnchor();

        SkillVFXSpawner.SpawnSlashAtWorld(
            SkillVFXLibrary.Instance.basicSlash,
            anchor.position + Vector3.up * 0f,
            damageType,
            destroyDelay: 1.5f
        );

        yield return new WaitForSeconds(0.25f);
    }
}