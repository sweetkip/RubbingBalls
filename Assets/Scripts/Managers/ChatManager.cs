using Fusion;
using UnityEngine;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Instance;
    [Networked] public NetworkString<_512> conversationRecord { get; set; }
    public override void Spawned()
    {
        Instance = this;
        conversationRecord = "";
    }
}
