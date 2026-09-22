using UnityEngine;

public class SkillVFXLibrary : MonoBehaviour
{
    public static SkillVFXLibrary Instance { get; private set; }

    [Header("Basic Slash")]
    public GameObject basicSlashNormal;
    public GameObject basicSlashFire;
    public GameObject basicSlashWater;

    [Header("Fire")]
    public GameObject fireballProjectile;
    public GameObject fireballExplosion;
    public GameObject flameDashTrail;
    public GameObject flameDashImpact;
    public GameObject fireExplosion;

    [Header("Water")]
    public GameObject waterHeal;
    public GameObject waterWave;
    public GameObject waterPrison;

    [Header("Larva")]
    public GameObject poisonSpit;
    public GameObject larvaBite;

    private void Awake()
    {
        Instance = this;
    }

    public GameObject GetBasicSlash(DamageType damageType)
    {
        return damageType switch
        {
            DamageType.Fire => basicSlashFire,
            DamageType.Water => basicSlashWater,
            _ => basicSlashNormal
        };
    }

    public GameObject GetLarvaBite()
    {
        return larvaBite;
    }
}
