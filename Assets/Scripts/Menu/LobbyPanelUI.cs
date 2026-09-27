using UnityEngine;

public class LobbyPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager manager;
    [SerializeField] private SessionListUI sessionListUI;
    [SerializeField] private SlidePanel panel;

    private bool isOpen;

    private void OnEnable()
    {
        manager.OnSessionListChanged += sessionListUI.UpdateList;
    }

    private void OnDisable()
    {
        manager.OnSessionListChanged -= sessionListUI.UpdateList;
    }

    public void ToggleLobby()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        isOpen = true;
        manager.JoinLobby();
        panel.Open();
    }

    public void Close()
    {
        isOpen = false;
        panel.Close();
    }
    
    public void Refresh()
    {
        manager.JoinLobby();
    }
}