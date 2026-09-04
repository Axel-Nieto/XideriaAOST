using System.Collections;
using UnityEngine;

public static class BasicAttackVFX
{
    public static IEnumerator Play(
        Character target,
        DamageType damageType,
        Vector3 positionOffset,
        float hitDelay)
    {
        if (SkillVFXLibrary.Instance == null || target == null)
            yield break;

        Transform anchor = target.GetGroundAnchor();

        GameObject slashPrefab =
            SkillVFXLibrary.Instance.GetBasicSlash(damageType);

        SkillVFXSpawner.SpawnSlashAtWorld(
            slashPrefab,
            anchor.position,
            positionOffset,
            destroyDelay: 3f
        );

        yield return new WaitForSeconds(hitDelay);
    }
}