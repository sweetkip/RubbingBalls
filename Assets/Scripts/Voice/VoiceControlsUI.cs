using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One per scene (Lobby, Partida, map2, map3, map4), next to the two buttons.
// It never touches the microphone: it only changes VoiceSettings and draws the icons.
// PlayerVoice is the one that applies the result to the Recorder.
public class VoiceControlsUI : MonoBehaviour
{
    [SerializeField] private VoiceIcons icons;
    [SerializeField] private Button muteButton;
    [SerializeField] private Button talkModeButton;
    [SerializeField] private KeyCode muteKey = KeyCode.M;

    private void OnEnable()
    {
        if (muteButton != null)
            muteButton.onClick.AddListener(OnMuteClicked);

        if (talkModeButton != null)
            talkModeButton.onClick.AddListener(OnTalkModeClicked);

        VoiceSettings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (muteButton != null)
            muteButton.onClick.RemoveListener(OnMuteClicked);

        if (talkModeButton != null)
            talkModeButton.onClick.RemoveListener(OnTalkModeClicked);

        VoiceSettings.Changed -= Refresh;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(muteKey))
            return;

        if (TextInputFocus.IsTyping())
            return;

        VoiceSettings.ToggleMute();
    }

    private void OnMuteClicked()
    {
        VoiceSettings.ToggleMute();
        ReleaseSelection();
    }

    private void OnTalkModeClicked()
    {
        VoiceSettings.ToggleMode();
        ReleaseSelection();
    }

    // After a click the button stays "selected", and the Enter key would press it again.
    private static void ReleaseSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Refresh()
    {
        if (icons == null)
            return;

        if (muteButton != null && muteButton.image != null)
        {
            if (VoiceSettings.Muted)
                muteButton.image.sprite = icons.MutedIcon;
            else
                muteButton.image.sprite = icons.UnmutedIcon;
        }

        if (talkModeButton != null && talkModeButton.image != null)
        {
            if (VoiceSettings.Mode == VoiceMode.PushToTalk)
                talkModeButton.image.sprite = icons.PushToTalkIcon;
            else
                talkModeButton.image.sprite = icons.ContinuousIcon;
        }
    }
}
