using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class SkillIconLibrary : MonoBehaviour
{
    public static SkillIconLibrary Instance { get; private set; }

    [Header("Basic")]
    public Sprite basicAttack;

    [Header("Fire")]
    public Sprite fireBasic;
    public Sprite fireball;
    public Sprite flameDash;
    public Sprite fireExplosion;

    [Header("Water")]
    public Sprite waterBasic;
    public Sprite waterHeal;
    public Sprite waterWave;
    public Sprite waterPrison;

    [Header("Rock")]
    public Sprite rockShield;
    public Sprite rockExcavate;
    public Sprite rockEruption;

    [Header("Larva")]
    public Sprite larvaBite;
    public Sprite poisonSpit;

    private void Awake()
    {
        Instance = this;

        Debug.Log("SkillIconLibrary cargado");
    }
}
