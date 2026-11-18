using Fusion;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerSlot : MonoBehaviour
{
    [SerializeField] private GameObject speakerIcon;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private Image colorImage;
    [SerializeField] private TMP_Text readyText;

    private PlayerVoice voice;

    private void Awake()
    {
        if (speakerIcon == null)
        {
            Transform child = transform.Find("SpeakerIcon");

            if (child != null)
                speakerIcon = child.gameObject;
        }

        SetSpeakerVisible(false);
    }

    private void Update()
    {
        bool talking = voice != null && voice.IsTalking;
        SetSpeakerVisible(talking);
    }

    private void SetSpeakerVisible(bool visible)
    {
        if (speakerIcon == null)
            return;

        if (speakerIcon.activeSelf != visible)
            speakerIcon.SetActive(visible);
    }

    public void Show(PlayerRef player, LobbyPlayer lobbyPlayer, PlayerColorPalette palette, bool isLocalPlayer)
    {
        gameObject.SetActive(true);

        voice = lobbyPlayer.GetComponent<PlayerVoice>();

        if (playerText != null)
        {
            string nick = lobbyPlayer.Nickname.ToString();
            if (string.IsNullOrWhiteSpace(nick))
                nick = $"Player {player.PlayerId}";

            playerText.text = isLocalPlayer ? $"{nick} (You)" : nick;
        }

        if (colorImage != null && palette != null)
            colorImage.color = palette.GetColor(lobbyPlayer.ColorIndex);

        if (colorImage != null && palette != null)
            colorImage.sprite = palette.GetSprite(lobbyPlayer.SpriteIndex);

        if (readyText != null)
            readyText.text = lobbyPlayer.IsReady ? "READY" : "CHOOSING";
    }

    public void Hide()
    {
        voice = null;
        SetSpeakerVisible(false);
        gameObject.SetActive(false);
    }
}