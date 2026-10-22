using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private AudioClip menuOST;
    [SerializeField] private AudioClip charOST;
    [SerializeField] private AudioClip gameOST;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayForScene(scene.name);
    }

    private void PlayForScene(string sceneName)
    {
        AudioClip clip = sceneName switch
        {
            "MainMenu" => menuOST,
            "Lobby" => charOST,
            "Partida" => gameOST,
            _ => null
        };

        if (clip == null || audioSource.clip == clip)
            return;

        audioSource.clip = clip;
        audioSource.Play();
    }
}