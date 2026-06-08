using UnityEngine;

public class SkillVFXLibrary : MonoBehaviour
{
    public static SkillVFXLibrary Instance { get; private set; }

    [Header("Fire")]
    public GameObject fireBasic;
    public GameObject fireballProjectile;
    public GameObject fireballExplosion;
    public GameObject flameDashTrail;
    public GameObject flameDashImpact;
    public GameObject fireExplosion;

    [Header("Water")]
    public GameObject waterBasic;
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
}
