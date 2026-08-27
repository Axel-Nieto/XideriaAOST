using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum LevelPhase
{
    Combat,
    Exploration
}

public static class GameSession
{
    public static bool IsGameOver { get; set; }
}

public class GameStateManager : MonoBehaviour
{
    [SerializeField] private string nextLevelName;
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private GridSystem gridSystem;
    public LevelPhase CurrentPhase { get; private set; } = LevelPhase.Combat;
    public bool IsExplorationMode => CurrentPhase == LevelPhase.Exploration;

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
            if (character.IsPlayer)
                continue;

            if (character.Stats.IsAlive && !character.IsDying)
                return;
        }

        EnterExplorationMode();
    }

    private void EnterExplorationMode()
    {
        if (CurrentPhase == LevelPhase.Exploration)
            return;

        CurrentPhase = LevelPhase.Exploration;

        gridSystem.SetSpecialCellsVisible(true);
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