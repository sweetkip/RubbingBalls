using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

// Goes on the root of LobbyPlayer.prefab and Ball.prefab (next to NetworkObject and VoiceNetworkObject).
// It does two things:
//  1) If this object is MINE (input authority): decides whether the Recorder transmits.
//  2) For EVERY player: exposes IsTalking (is this player making sound now?) to draw the icon.
public class PlayerVoice : MonoBehaviour
{
    [SerializeField] private KeyCode pushToTalkKey = KeyCode.Space;
    [Tooltip("How long the icon stays on after the last sound, so it does not flicker")]
    [SerializeField] private float talkHoldSeconds = 0.25f;

    private NetworkObject networkObject;
    private VoiceNetworkObject voice;
    private Recorder recorder;
    private Ball ball;

    private float lastVoiceTime = -100f;
    private int hudId = -1;

    public bool IsTalking { get; private set; }

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        voice = GetComponent<VoiceNetworkObject>();
        recorder = GetComponentInChildren<Recorder>(true);
        ball = GetComponent<Ball>();

        // Nobody transmits by default until ApplyTransmitRule says so.
        if (recorder != null)
            recorder.TransmitEnabled = false;
    }

    private void Update()
    {
        if (networkObject == null || !networkObject.IsValid)
            return;

        if (networkObject.HasInputAuthority)
            ApplyTransmitRule();

        UpdateTalking();
    }

    private void OnDisable()
    {
        ClearHud();
    }

    // One rule: muted always wins.
    //   transmit = NOT muted  AND  (continuous mode  OR  space held while not typing)
    private void ApplyTransmitRule()
    {
        if (recorder == null)
            return;

        bool shouldTransmit = false;

        if (!VoiceSettings.Muted)
        {
            if (VoiceSettings.Mode == VoiceMode.Continuous)
            {
                shouldTransmit = true;
            }
            else
            {
                shouldTransmit = Input.GetKey(pushToTalkKey) && !TextInputFocus.IsTyping();
            }
        }

        if (recorder.TransmitEnabled != shouldTransmit)
            recorder.TransmitEnabled = shouldTransmit;
    }

    private void UpdateTalking()
    {
        bool soundNow = false;

        if (voice != null)
        {
            // Remote player: their Speaker is playing audio that arrived from the network.
            Speaker speaker = voice.SpeakerInUse;

            if (speaker != null && speaker.IsPlaying)
                soundNow = true;

            // Local player: my Recorder is really sending.
            Recorder activeRecorder = voice.RecorderInUse;

            if (activeRecorder != null && activeRecorder.IsCurrentlyTransmitting)
                soundNow = true;
        }

        if (soundNow)
            lastVoiceTime = Time.unscaledTime;

        bool talking = Time.unscaledTime - lastVoiceTime < talkHoldSeconds;

        if (talking != IsTalking)
        {
            IsTalking = talking;
            PushToHud(talking);
            return;
        }

        // The ball id arrives over the network: if it changes, move the icon to the right slot.
        if (ball != null && ball.id != hudId)
            PushToHud(IsTalking);
    }

    private void PushToHud(bool talking)
    {
        if (ball == null || UIManager.Instance == null)
            return;

        int id = ball.id;

        if (hudId != id)
        {
            if (hudId >= 0)
                UIManager.Instance.SetPlayerSpeaking(hudId, false);

            hudId = id;
        }

        UIManager.Instance.SetPlayerSpeaking(id, talking);
    }

    private void ClearHud()
    {
        if (hudId >= 0 && UIManager.Instance != null)
            UIManager.Instance.SetPlayerSpeaking(hudId, false);

        hudId = -1;
        IsTalking = false;
    }
}
