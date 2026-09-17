using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

//<>
//{}

public class RoomListUI : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    [SerializeField] private ScrollRect scrollLobby;


    private void OnEnable()
    {
        manager.OnSessionListChanged += RefreshList;
    }

    private void OnDisable()
    {
        manager.OnSessionListChanged -= RefreshList;
    }

    private void RefreshList(List<SessionInfo> sessions)
    {
        for (int i = scrollLobby.content.childCount - 1; i >= 0; i--)
        {
            Destroy(scrollLobby.content.GetChild(i).gameObject);
        }

        //Primero se van a mostrar las salas con jugadores o creadas por los mismos
        var realNames = new HashSet<string>();
        foreach (var session in sessions)
        {
            if (!session.IsValid) continue;
            realNames.Add(session.Name);

            bool joinable = session.IsOpen && session.PlayerCount < session.MaxPlayers;
            AddEntry(session.Name, session.PlayerCount, session.MaxPlayers, joinable);
        }

        //Después las salas preseteadas vacías
        foreach (var presetName in manager.PresetRooms)
        {
            if (!realNames.Contains(presetName))
            {
                AddEntry(presetName, 0, 4, true);
            }
        }
    }

    private void AddEntry(string roomName, int current, int max, bool joinable)
    {
        GameObject row = new GameObject($"Room_{roomName}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
    }
}