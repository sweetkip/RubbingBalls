using UnityEngine;
using UnityEngine.Audio;

public enum BallFeedbackSound { Launch, MarbleImpact, WallImpact, Damage, Respawn, Fall }

public static class BallFeedbackAudio
{
    private static readonly AudioClip[] clips = new AudioClip[6];

    public static AudioClip GetClip(BallFeedbackSound sound)
    {
        int index = (int)sound;
        if (clips[index] == null)
            clips[index] = Resources.Load<AudioClip>("BallFeedback/" + sound);
        return clips[index];
    }

    public static void PlayDetached(AudioClip customClip, BallFeedbackSound sound, Vector3 position, float volume, AudioMixerGroup mixer = null)
    {
        if (Application.isBatchMode || volume <= 0f)
            return;

        AudioClip clip = customClip != null ? customClip : GetClip(sound);
        if (clip == null)
            return;

        GameObject soundObject = new GameObject("Ball SFX " + sound);
        soundObject.transform.position = position;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = mixer;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Random.Range(0.96f, 1.04f);
        source.clip = clip;
        source.Play();
        Object.Destroy(soundObject, clip.length / source.pitch + 0.1f);
    }

    public static void PreloadAll()
    {
        for (int i = 0; i < clips.Length; i++)
            GetClip((BallFeedbackSound)i);
    }
}
