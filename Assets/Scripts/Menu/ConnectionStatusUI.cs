using TMPro;
using UnityEngine;


public static class ConnectionMessage
{
    public static string Text = "";
    public static bool IsError;
    public static string RejoinSession = "";

    public static void Set(string text, bool isError)
    {
        Text = text;
        IsError = isError;
    }
}

public class ConnectionStatusUI : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField sessionInput;  
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color errorColor = new Color(1f, 0.35f, 0.35f);

    private void OnEnable()
    {
        manager.OnStatusChanged += ShowStatus;
    }

    private void OnDisable()
    {
        manager.OnStatusChanged -= ShowStatus;
    }

    private void Start()
    {
        if (ConnectionMessage.Text != "")
        {
            ShowStatus(ConnectionMessage.Text, ConnectionMessage.IsError);
            ConnectionMessage.Text = "";
        }
        else
        {
            ShowStatus("Creá una partida, unite con un nombre o tocá Quick Play.", false);
        }

        if (ConnectionMessage.RejoinSession != "")
        {
            sessionInput.text = ConnectionMessage.RejoinSession;
            statusText.text += "\nSi la partida sigue abierta, tocá Client para volver a entrar.";
            ConnectionMessage.RejoinSession = "";
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        statusText.text = message;
        statusText.color = isError ? errorColor : normalColor;
    }
}
