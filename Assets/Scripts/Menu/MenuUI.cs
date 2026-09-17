using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private VisualElement roomList;

    public void CreateGame()
    {
        manager.StartGameHost(input.text);
    }

    public void JoinGame()
    {
        manager.StartGameClient(input.text);
    }
    public void RoomList()
    {
        roomList.style.display = roomList.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void QuickPlay()
    {
        manager.QuickPlay();
    }
}