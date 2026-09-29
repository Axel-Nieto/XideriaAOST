using TMPro;
using UnityEngine;

public class EnemyHoverUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Enemy Info")]
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI energyText;

    private Character currentEnemy;

    private void Awake()
    {
        Hide();
    }

    public void Show(Character enemy)
    {
        if (enemy == null || enemy.IsPlayer)
        {
            Hide();
            return;
        }

        currentEnemy = enemy;

        if (panel != null)
            panel.SetActive(true);

        if (enemyNameText != null)
            enemyNameText.text = enemy.name;

        if (hpText != null)
            hpText.text = enemy.Stats.CurrentHP.ToString();

        if (energyText != null)
            energyText.text = enemy.Stats.CurrentEnergy.ToString();
    }

    public void Hide()
    {
        currentEnemy = null;

        if (panel != null)
            panel.SetActive(false);
    }
}