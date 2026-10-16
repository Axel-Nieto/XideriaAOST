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
    [SerializeField] private GridSystem gridSystem;
    public LevelPhase CurrentPhase { get; private set; } = LevelPhase.Combat;
    public bool IsExplorationMode => CurrentPhase == LevelPhase.Exploration;

    private bool gameEnded = false;
    private bool roomHadEnemies;
    private string rewardRoomId;
    private bool reportedPendingReward;

    private void Awake()
    {
        GameSession.IsGameOver = false;
    }

    private void Start()
    {
        foreach (Character character in FindObjectsOfType<Character>())
            if (!character.IsPlayer && character.Stats.IsAlive)
                roomHadEnemies = true;

        PlayerSessionData session = PlayerSessionData.Instance;
        if (session != null)
            rewardRoomId = session.RunState.EnterScene(gameObject.scene.name).RoomId;
        gridSystem?.PrepareDoorOffers();
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
        bool playerSurvived = false;

        foreach (var character in characters)
        {
            if (character.IsPlayer)
            {
                playerSurvived |= character.gameObject.activeInHierarchy && character.Stats.IsAlive && !character.IsDying;
                continue;
            }

            if (character.Stats.IsAlive && !character.IsDying)
                return;
        }

        if (roomHadEnemies && playerSurvived)
        {
            PlayerSessionData session = PlayerSessionData.Instance;
            if (session != null)
            {
                session.TryCompleteRoom(rewardRoomId);
                foreach (RunRewardRecord offer in session.RunState.Rewards)
                    if (offer.RoomId == rewardRoomId && offer.HasReward && !offer.IsDelivered && !reportedPendingReward)
                    {
                        reportedPendingReward = true;
                        Debug.LogWarning("Room completed, but its reward could not be credited. It remains pending (check currency capacity).", this);
                    }
            }
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
        SceneTransitionManager.Instance.LoadScene("Lobby");
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

            SceneTransitionManager.Instance.LoadScene("Lobby");
        }
    }
}
