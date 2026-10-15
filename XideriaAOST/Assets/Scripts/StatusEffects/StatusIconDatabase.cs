using UnityEngine;

[CreateAssetMenu(menuName = "UI/Status Icon Database")]
public class StatusIconDatabase : ScriptableObject
{
    private static StatusIconDatabase _instance;

    public static StatusIconDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<StatusIconDatabase>("StatusIconDatabase");

                if (_instance == null)
                {
                    Debug.LogError("No se encontró StatusIconDatabase en Resources.");
                }
            }

            return _instance;
        }
    }

    public Sprite defaultStatusIcon;
    public Sprite defaultPassiveIcon;

    public Sprite burnIcon;
    public Sprite rootIcon;
    public Sprite poisonIcon;
    public Sprite slowIcon;

    public Sprite fireResistanceIcon;
    public Sprite waterResistanceIcon;
    public Sprite poisonResistanceIcon;
    public Sprite vitalImpulseIcon;
}