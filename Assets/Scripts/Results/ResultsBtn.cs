using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultsBtn : MonoBehaviour
{
    private const string MenuSceneName = "MainMenu";

    public void BackToMenu()
    {
        NetworkManager manager = FindAnyObjectByType<NetworkManager>();
        manager?.Disconect();
    }

    public void QuickRematch()
    {
        NetworkManager manager = FindAnyObjectByType<NetworkManager>();
        if (manager == null) return;

        SceneManager.sceneLoaded += OnMenuSceneLoaded;
        manager.Disconect();
    }

    private void OnMenuSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MenuSceneName) return;

        SceneManager.sceneLoaded -= OnMenuSceneLoaded;

        NetworkManager manager = FindAnyObjectByType<NetworkManager>();
        manager?.QuickPlay();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}