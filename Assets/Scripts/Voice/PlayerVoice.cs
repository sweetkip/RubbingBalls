using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerVoice : MonoBehaviour
{
    [SerializeField] private KeyCode pushToTalkKey = KeyCode.Space;
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
            Speaker speaker = voice.SpeakerInUse;

            if (speaker != null && speaker.IsPlaying)
                soundNow = true;

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
