using UnityEngine;

// One asset with the 4 icons, so each scene needs 1 reference instead of 4.
// Create it from: Assets > Create > Rubbing Balls > Voice Icons
[CreateAssetMenu(fileName = "VoiceIcons", menuName = "Rubbing Balls/Voice Icons")]
public class VoiceIcons : ScriptableObject
{
    [Header("Mute button")]
    [Tooltip("Shown while the player is muted (speakerOffIcon)")]
    [SerializeField] private Sprite mutedIcon;
    [Tooltip("Shown while the player can talk (speakerIcon)")]
    [SerializeField] private Sprite unmutedIcon;

    [Header("Talk mode button")]
    [Tooltip("Continuous mode (oldMicrophone)")]
    [SerializeField] private Sprite continuousIcon;
    [Tooltip("Push to talk mode (buttonFinger)")]
    [SerializeField] private Sprite pushToTalkIcon;

    public Sprite MutedIcon
    {
        get { return mutedIcon; }
    }

    public Sprite UnmutedIcon
    {
        get { return unmutedIcon; }
    }

    public Sprite ContinuousIcon
    {
        get { return continuousIcon; }
    }

    public Sprite PushToTalkIcon
    {
        get { return pushToTalkIcon; }
    }
}
