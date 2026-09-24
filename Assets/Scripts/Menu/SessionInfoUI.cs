using Fusion;
using TMPro;
using UnityEngine;

// NUEVO: muestra el estado de la conexión mientras estás en una partida:
// nombre de la sala, cantidad de jugadores y ping.
// Sirve para la sala de espera (SceneLoad) y para la arena (Lobby).
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
            double rttSegundos = runner.GetPlayerRtt(runner.LocalPlayer);   // ida y vuelta, en segundos
            ping = "Ping: " + Mathf.RoundToInt((float)(rttSegundos * 1000)) + " ms";
        }

        infoText.text = "Sala: " + runner.SessionInfo.Name
            + "   Jugadores: " + runner.SessionInfo.PlayerCount + "/" + runner.SessionInfo.MaxPlayers
            + "   " + ping;
    }
}
