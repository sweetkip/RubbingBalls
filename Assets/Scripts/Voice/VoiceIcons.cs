using UnityEngine;

[CreateAssetMenu(fileName = "VoiceIcons", menuName = "Rubbing Balls/Voice Icons")]
public class VoiceIcons : ScriptableObject
{
    [Header("Mute button")]
    [SerializeField] private Sprite mutedIcon;
    [SerializeField] private Sprite unmutedIcon;

    [Header("Talk mode button")]
    [SerializeField] private Sprite continuousIcon;
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
