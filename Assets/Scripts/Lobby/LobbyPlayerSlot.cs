using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private Image colorImage;
    [SerializeField] private TMP_Text readyText;

    public void Show(
        PlayerRef player,
        LobbyPlayer lobbyPlayer,
        PlayerColorPalette palette,
        bool isLocalPlayer)
    {
        gameObject.SetActive(true);

        if (playerText != null)
        {
            if (isLocalPlayer)
                playerText.text = $"Jugador {player.PlayerId} (Tú)";
            else
                playerText.text = $"Jugador {player.PlayerId}";
        }

        if (colorImage != null && palette != null)
        {
            colorImage.color = palette.GetColor(lobbyPlayer.ColorIndex);
        }

        if (readyText != null)
        {
            readyText.text = lobbyPlayer.IsReady
                ? "LISTO"
                : "ESPERANDO";
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}