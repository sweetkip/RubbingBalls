using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;

public class PlayersChat : NetworkBehaviour
{

    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button chatButton;
    [SerializeField] private NetworkObject chatManager;
    private string chatName;

    private void Start()
    {
        input = GameObject.Find("ChatInput").GetComponent<TMP_InputField>();
        chatButton = GameObject.Find("ChatButton").GetComponent<Button>();
        chatButton.onClick.AddListener(SendMessaje);
        input.onEndEdit.AddListener(OnInputEndEdit);
        if(Runner.IsServer && ChatManager.Instance == null)
        {
            Runner.Spawn(chatManager);
        }

        if(PlayerPrefs.HasKey("PlayerName"))
        {
            chatName = PlayerPrefs.GetString("PlayerName");
        }
        else
        {
            PlayerPrefs.SetString("PlayerName", "NoName");
            chatName = "NoName";
        }
    }

    public void SendMessaje()
    {
        if (!HasInputAuthority) return;
        if (string.IsNullOrEmpty(input.text)) return;

        RPC_SendMessage(input.text, chatName);
        input.text = "";
    }
    private void OnInputEndEdit(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            SendMessaje();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_SendMessage(string message, string name, RpcInfo info = default)
    {
        ChatManager.Instance.AddMessage(name + ": " + message);
    }

    public void NameChanged()
    {
        chatName = PlayerPrefs.GetString("PlayerName");
    }
}
