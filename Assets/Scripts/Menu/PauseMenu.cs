using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private SettingsPanel settingsPanel;

    private bool isOpen;

    private void Start()
    {
        settingsPanel.Closed += ShowMainPanel;
        pauseRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (settingsPanel != null)
            settingsPanel.Closed -= ShowMainPanel;

        // Si cambia la escena con el menú abierto, no dejar el input bloqueado
        if (isOpen && NetworkManager.Instance != null)
            NetworkManager.Instance.SetInputEnabled(true);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (!isOpen)
            Open();
        else if (settingsPanel.gameObject.activeSelf)
            settingsPanel.Back();
        else
            Close();
    }

    public void Open()
    {
        isOpen = true;
        pauseRoot.SetActive(true);
        mainPanel.SetActive(true);
        settingsPanel.gameObject.SetActive(false);
        SetGameInput(false);
    }

    public void Close()
    {
        isOpen = false;
        pauseRoot.SetActive(false);
        SetGameInput(true);
    }

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.gameObject.SetActive(true);
    }

    public void BackToMainMenu()
    {
        Close();

        NetworkManager manager = NetworkManager.Find();

        if (manager != null)
            manager.Disconnect();
    }

    private void ShowMainPanel()
    {
        mainPanel.SetActive(true);
    }

    private void SetGameInput(bool enabled)
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SetInputEnabled(enabled);
    }

    public async void QuitGame()
    {
        NetworkManager manager = NetworkManager.Find();

        if (manager != null && manager.Runner != null && manager.Runner.IsRunning)
            await manager.Runner.Shutdown();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }
}