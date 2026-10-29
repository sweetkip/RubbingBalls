using Fusion;
using TMPro;
using UnityEngine;

public class SessionInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text infoText;

    private void Update()
    {
        if (!NetworkManager.TryGetRunner(out NetworkRunner runner))
            return;

        string ping;
        if (runner.IsServer)
        {
            ping = "You're hosting";
        }
        else
        {
            double rttSegundos = runner.GetPlayerRtt(runner.LocalPlayer);
            ping = "Ping: " + Mathf.RoundToInt((float)(rttSegundos * 1000)) + " ms";
        }

        infoText.text = "Room: " + runner.SessionInfo.Name + "   Players: " + runner.SessionInfo.PlayerCount + "/" + runner.SessionInfo.MaxPlayers + "   " + ping;

        if (runner.SessionInfo.PlayerCount <= 1)
            infoText.text = infoText.text + " not enough players";
    }
}