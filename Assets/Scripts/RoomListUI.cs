using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

//<>
public class RoomListUI : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    [SerializeField] private ScrollView scrollLobby;

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
        scrollLobby.Clear();

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
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 4;
        row.style.paddingBottom = 4;
        row.style.paddingLeft = 8;
        row.style.paddingRight = 8;

        var nameLabel = new Label(roomName);
        var countLabel = new Label($"{current}/{max}");

        var joinButton = new Button(() => manager.JoinOrCreateSession(roomName))
        {
            text = joinable ? "Unirse" : "Llena"
        };
        joinButton.SetEnabled(joinable);

        row.Add(nameLabel);
        row.Add(countLabel);
        row.Add(joinButton);

        scrollLobby.Add(row);
    }
}