using TMPro;
using UnityEngine;

// Guarda el mensaje para mostrarlo cuando se vuelve a cargar el menú.
// Es "static" para que sobreviva al cambio de escena (igual que PlayerLocalData).
public static class ConnectionMessage
{
    public static string Text = "";
    public static bool IsError;
    public static string RejoinSession = "";   // nombre de la partida donde se cortó la conexión

    public static void Set(string text, bool isError)
    {
        Text = text;
        IsError = isError;
    }
}

// Muestra en el menú el estado de la conexión y los errores.
public class ConnectionStatusUI : MonoBehaviour
{
    [SerializeField] private NetworkManager manager;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField sessionInput;   // el mismo input del nombre de partida
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
        // ¿Volví al menú por un error o una desconexión? Muestro el motivo.
        if (ConnectionMessage.Text != "")
        {
            ShowStatus(ConnectionMessage.Text, ConnectionMessage.IsError);
            ConnectionMessage.Text = "";
        }
        else
        {
            ShowStatus("Creá una partida, unite con un nombre o tocá Quick Play.", false);
        }

        // Reconexión básica: si se cortó en medio de una partida, dejo su nombre escrito
        // para que el jugador pueda tocar Client e intentar volver a entrar.
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
