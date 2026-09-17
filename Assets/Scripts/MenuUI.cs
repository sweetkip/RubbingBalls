using UnityEngine;
using TMPro;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    [SerializeField] private TMP_InputField input;

    public void CreateGame()
    {
        manager.StartGameHost(input.text);
    }

    public void JoinGame()
    {
        manager.StartGameClient(input.text);
    }
}
