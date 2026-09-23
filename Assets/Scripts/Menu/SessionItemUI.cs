using UnityEngine;
using TMPro;
using Fusion;
using UnityEngine.UI;

public class SessionItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text sessionNameTxt;
    [SerializeField] private TMP_Text playersTxt;
    [SerializeField] private Button joinBtn;

    private string sessionName;
    private NetworkManager networkManager;

    public void Setup(SessionInfo session, NetworkManager manager)
    {
        sessionName = session.Name;
        networkManager = manager;

        sessionNameTxt.text = session.Name;
        playersTxt.text = session.PlayerCount + "/" + session.MaxPlayers;

        bool hasSlot = session.IsOpen && session.PlayerCount < session.MaxPlayers;
        joinBtn.interactable = hasSlot;
    }

    public void Join()
    {
        networkManager.StartGameClient(sessionName);
    }
}