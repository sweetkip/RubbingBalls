using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Instance;
    [Networked, OnChangedRender(nameof(RefreshText))]
    public NetworkString<_512> conversationRecord { get; set; }
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private ScrollRect scrollRect;

    public override void Spawned()
    {
        Instance = this;
        chatText = GameObject.Find("Content").GetComponent<TMP_Text>();
        scrollRect = GameObject.Find("Chat").GetComponent<ScrollRect>();
        if (Object.HasStateAuthority)
            conversationRecord = "";
        scrollRect.verticalNormalizedPosition = 0f;
        RefreshText();
    }

    public void RestoreMigration(string text)
    {
        if (!Object.HasStateAuthority) return;
        conversationRecord = text ?? "";
        RefreshText();
    }

    public void AddMessage(string line)
    {
        if (!Object.HasStateAuthority) return;

        string record = conversationRecord.ToString() + "\n" + line;

        while (record.Length > 500)
        {
            int cut = record.IndexOf('\n', 1);
            if (cut > 0)
            {
                record = record.Substring(cut);
            }
            else
            {
                record = record.Substring(record.Length - 500);
            }
        }

        conversationRecord = record;
        RefreshText();
    }

    private void RefreshText()
    {
        if (chatText == null) return;
        
        chatText.text = conversationRecord.ToString();
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
