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
    public NetworkObject MigrationChatPrefab => chatManager;

    private void Start()
    {
        if (Object == null || !Object.IsValid) return;
        if (Runner.IsServer && ChatManager.Instance == null && chatManager != null)
            Runner.Spawn(chatManager);
        if (!HasInputAuthority) return;
        input = GameObject.Find("ChatInput").GetComponent<TMP_InputField>();
        chatButton = GameObject.Find("ChatButton").GetComponent<Button>();
        chatButton.onClick.AddListener(SendMessaje);
        input.onEndEdit.AddListener(OnInputEndEdit);

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

    private void OnDestroy()
    {
        if (chatButton != null) chatButton.onClick.RemoveListener(SendMessaje);
        if (input != null) input.onEndEdit.RemoveListener(OnInputEndEdit);
    }

    public void SendMessaje()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsMigrating) return;
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SendMessage(string message, string name, RpcInfo info = default)
    {
        ChatManager.Instance.AddMessage(name + ": " + message);
    }

    public void NameChanged()
    {
        chatName = PlayerPrefs.GetString("PlayerName");
    }
}
