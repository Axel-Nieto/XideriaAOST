using System.Collections.Generic;
using UnityEngine;

public class EnemyLootDropper : MonoBehaviour
{
    [Header("Drop Chances")]
    [Range(0f, 1f)]
    [SerializeField] private float rewardDropChance = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float helmetDropChance = 0.6f;

    [Header("Heal Reward")]
    [SerializeField] private GameObject healRewardPrefab;
    [SerializeField] private float healRewardLifetime = 3f;
    [SerializeField] private Vector3 healRewardOffset = Vector3.up * 0.5f;
    [SerializeField] private int healAmount = 5;


    public void TryDrop(Character deadEnemy)
    {
        if (deadEnemy == null)
            return;

        if (Random.value > rewardDropChance)
            return;

        if (Random.value <= helmetDropChance)
            DropHelmet(deadEnemy);
        else
            HealPlayer(deadEnemy);
    }

    private void DropHelmet(Character deadEnemy)
    {
        if (HelmetDropTable.Instance == null)
        {
            Debug.LogWarning("No existe HelmetDropTable en la escena.");
            return;
        }

        GameObject prefab = HelmetDropTable.Instance.GetRandomHelmetPickupPrefab();

        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab);

        HelmetPickup pickup = instance.GetComponent<HelmetPickup>();

        if (pickup == null)
        {
            Debug.LogWarning($"{prefab.name} no tiene HelmetPickup.");
            Destroy(instance);
            return;
        }

        pickup.SetGridPosition(deadEnemy.GridPosition);
    }

    private void HealPlayer(Character deadEnemy)
    {
        if (healRewardPrefab != null)
        {
            GameObject heart = Instantiate(
                healRewardPrefab,
                deadEnemy.transform.position + healRewardOffset,
                Quaternion.identity
            );

            Destroy(heart, healRewardLifetime);
        }

        Character player = FindPlayer();

        if (player != null)
        {
            player.Heal(healAmount);
        }
    }

    private Character FindPlayer()
    {
        Character[] characters = FindObjectsOfType<Character>();

        foreach (var character in characters)
        {
            if (character.IsPlayer)
                return character;
        }

        return null;
    }
}