using System.Collections.Generic;
using UnityEngine;

public class PlayerSessionData : MonoBehaviour
{
    public static PlayerSessionData Instance { get; private set; }

    public bool hasSavedData = false;

    public int currentHP;
    public int currentEnergy;

    public bool hasHelmet;
    public HelmetType equippedHelmetType = HelmetType.Default;

    public Dictionary<string, int> skillCooldowns = new();

    public bool forceNewRun = false;
    public int equippedHelmetCharges = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetSession()
    {
        hasSavedData = false;
        forceNewRun = true;

        hasHelmet = false;
        equippedHelmetCharges = 0;
        equippedHelmetType = HelmetType.Default;

        skillCooldowns.Clear();
    }
}
