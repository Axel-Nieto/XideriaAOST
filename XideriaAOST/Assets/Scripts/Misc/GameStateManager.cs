using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSession
{
    public static bool IsGameOver { get; set; }
}

public class GameStateManager : MonoBehaviour
{
    [SerializeField] private string nextLevelName;

    private bool gameEnded = false;

    private void Awake()
    {
        GameSession.IsGameOver = false;
    }

    private void Update()
    {
        if (gameEnded)
            return;

        CheckVictory();
        CheckDefeat();
    }

    private void CheckVictory()
    {
        Character[] characters = FindObjectsOfType<Character>();

        foreach (var character in characters)
        {
            if (!character.IsPlayer && character.gameObject.activeSelf)
                return;
        }

        gameEnded = true;
        GameSession.IsGameOver = true;

        Debug.Log("NIVEL COMPLETADO");

        if (!string.IsNullOrEmpty(nextLevelName))
        {
            SceneTransitionManager.Instance.LoadScene(nextLevelName);
        }
        else
        {
            Debug.Log("VICTORIA FINAL");
        }
    }

    private void CheckDefeat()
    {
        Character[] characters = FindObjectsOfType<Character>();

        foreach (var character in characters)
        {
            if (character.IsPlayer && character.gameObject.activeSelf)
                return;
        }

        gameEnded = true;
        GameSession.IsGameOver = true;

        Debug.Log("DERROTA");

        SceneTransitionManager.Instance.LoadScene("Tutorial");
    }
}