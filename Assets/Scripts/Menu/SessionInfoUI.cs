using Fusion;
using TMPro;
using UnityEngine;

public class SessionInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text infoText;

    private void Update()
    {
        if (NetworkManager.Instance == null) return;

        NetworkRunner runner = NetworkManager.Instance.Runner;
        if (runner == null || !runner.IsRunning) return;

        string ping;
        if (runner.IsServer)
        {
            ping = "Sos el host";
        }
        else
        {
            double rttSegundos = runner.GetPlayerRtt(runner.LocalPlayer);
            ping = "Ping: " + Mathf.RoundToInt((float)(rttSegundos * 1000)) + " ms";
        }

        infoText.text = "Sala: " + runner.SessionInfo.Name
            + "   Jugadores: " + runner.SessionInfo.PlayerCount + "/" + runner.SessionInfo.MaxPlayers
            + "   " + ping;
        if(runner.SessionInfo.PlayerCount <= 1)
        {
            infoText.text = infoText.text + " necesitamos un player mas para empezar";
        }
    }
}
