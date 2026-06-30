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

        GameObject slashPrefab =
            SkillVFXLibrary.Instance.GetBasicSlash(damageType);

        SkillVFXSpawner.SpawnSlashAtWorld(
            slashPrefab,
            anchor.position,
            new Vector3(0f, 1.2f, 0f),
            destroyDelay: 3f
        );

        yield return new WaitForSeconds(0.25f);
    }
}