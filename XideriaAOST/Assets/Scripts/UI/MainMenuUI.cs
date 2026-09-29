using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [Header("Confirmation")]
    [SerializeField] private ConfirmationManager confirmationManager;

    [Header("Scenes")]
    [SerializeField] private string firstGameScene = "Lobby";

    public void NewGame()
    {
        confirmationManager.Show(
            "Nueva partida",
            "¿Deseas comenzar una nueva partida?",
            StartNewGame
        );
    }

    public void ExitGame()
    {
        confirmationManager.Show(
            "Salir del juego",
            "¿Seguro que deseas salir del juego?",
            QuitGame
        );
    }

    private void StartNewGame()
    {
        if (PlayerSessionData.Instance != null)
            PlayerSessionData.Instance.ResetSession();

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(firstGameScene);
    }

    private void QuitGame()
    {
        Debug.Log("[MAIN MENU] Cerrando juego...");

        Application.Quit();
    }
}