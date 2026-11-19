using System;
using UnityEngine;

public enum VoiceMode
{
    Continuous = 0,
    PushToTalk = 1
}

public static class VoiceSettings
{
    private const string ModeKey = "VoiceMode";

    public static event Action Changed;

    private static bool muted = true;
    private static bool modeLoaded;
    private static VoiceMode mode = VoiceMode.Continuous;

    public static bool Muted
    {
        get { return muted; }
    }

    public static VoiceMode Mode
    {
        get
        {
            if (!modeLoaded)
                LoadMode();

            return mode;
        }
    }

    private static void LoadMode()
    {
        modeLoaded = true;

        int saved = PlayerPrefs.GetInt(ModeKey, (int)VoiceMode.Continuous);

        if (saved == (int)VoiceMode.PushToTalk)
            mode = VoiceMode.PushToTalk;
        else
            mode = VoiceMode.Continuous;
    }

    public static void SetMuted(bool value)
    {
        if (muted == value)
            return;

        muted = value;
        Changed?.Invoke();
    }

    public static void ToggleMute()
    {
        SetMuted(!muted);
    }

    public static void ResetMute()
    {
        SetMuted(true);
    }

    public static void SetMode(VoiceMode value)
    {
        if (Mode == value)
            return;

        mode = value;
        PlayerPrefs.SetInt(ModeKey, (int)value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ToggleMode()
    {
        if (Mode == VoiceMode.Continuous)
            SetMode(VoiceMode.PushToTalk);
        else
            SetMode(VoiceMode.Continuous);
    }
}
