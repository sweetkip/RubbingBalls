using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;

public class PlayersChat : NetworkBehaviour
{

    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private Button chatButton;
    [SerializeField] private NetworkObject chatManager;

    private void Start()
    {
        input = GameObject.Find("ChatInput").GetComponent<TMP_InputField>();
        chatText = GameObject.Find("Content").GetComponent<TMP_Text>();
        chatButton = GameObject.Find("ChatButton").GetComponent<Button>();
        chatButton.onClick.AddListener(SendMessaje);
        input.onEndEdit.AddListener(OnInputEndEdit);
        if(ChatManager.Instance == null)
        {
            Runner.Spawn(chatManager);
        }
        UpdateChat();
    }

    public void SendMessaje()
    {
        if (!HasInputAuthority) return;
        if (string.IsNullOrEmpty(input.text)) return;

        RPC_SendMessage(input.text);
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
    private void RPC_SendMessage(string message, RpcInfo info = default)
    {
        ChatManager.Instance.conversationRecord += "\n" + info.Source + ": " + message;
        UpdateChat();
    }

    private void UpdateChat()
    {
        chatText.text = ChatManager.Instance.conversationRecord.ToString();
    }
}
