using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class GameSession
{
    public static bool IsGameOver { get; set; }
}

public class GameStateManager : MonoBehaviour
{
    [SerializeField] private string nextLevelName;
    [SerializeField] private string tutorialSceneName = "Tutorial";

    private bool gameEnded = false;

    private void Awake()
    {
        GameSession.IsGameOver = false;
    }

    private void Update()
    {
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            SkipLevel();
            return;
        }

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

        Character player = FindPlayer();

        if (!string.IsNullOrEmpty(nextLevelName) && nextLevelName != tutorialSceneName)
        {
            player?.SavePlayerSession();
            SceneTransitionManager.Instance.LoadScene(nextLevelName);
        }
        else
        {
            PlayerSessionData.Instance?.ResetSession();
            SceneTransitionManager.Instance.LoadScene(tutorialSceneName);
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

        PlayerSessionData.Instance?.ResetSession();
        SceneTransitionManager.Instance.LoadScene(tutorialSceneName);
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

    private void SkipLevel()
    {
        Character player = FindPlayer();

        if (!string.IsNullOrEmpty(nextLevelName))
        {
            player?.SavePlayerSession();

            SceneTransitionManager.Instance.LoadScene(nextLevelName);
        }
        else
        {

            PlayerSessionData.Instance?.ResetSession();

            SceneTransitionManager.Instance.LoadScene(tutorialSceneName);
        }
    }
}