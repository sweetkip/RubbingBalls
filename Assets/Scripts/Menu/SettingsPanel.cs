using System;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle fullscreenToggle;

    public event Action Closed;

    private void Awake()
    {
        masterSlider.onValueChanged.AddListener(GameSettings.SetMasterVolume);
        musicSlider.onValueChanged.AddListener(GameSettings.SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(GameSettings.SetSfxVolume);
        fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);
    }

    private void OnEnable()
    {
        masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
    }

    private void OnDisable()
    {
        GameSettings.Save();
    }

    public void Back()
    {
        gameObject.SetActive(false);
        Closed?.Invoke();
    }
}