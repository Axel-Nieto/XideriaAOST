using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioM : MonoBehaviour
{
    public static AudioM Instance { get; private set; }

    [Header("Componentes")]
    [SerializeField] private AudioSource audioSource;

    [Header("Pistas de Música")]
    public AudioClip lobbyMusic;
    public AudioClip gameplayMusic; 
    public AudioClip bossMusic;     

    void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // Se ejecuta al cambiar de escena
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Lobby")
        {
            PlayMusic(lobbyMusic);
        }
        else if (scene.name == "LevelBossZ1")
        {
            PlayMusic(bossMusic);
        }
        else if (scene.name.StartsWith("Level"))
        {
            
            PlayMusic(gameplayMusic);
        }
    }

    public void PlayMusic(AudioClip newClip)
    {
        if (newClip == null || audioSource.clip == newClip) return;

        audioSource.Stop();
        audioSource.clip = newClip;
        audioSource.Play();
    }
}
