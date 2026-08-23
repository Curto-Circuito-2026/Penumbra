using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum AudioChannelType
{
    Master,
    Music,
    SFX,
    Voice
}

public class AudioVolumeSliderUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private AudioChannelType channelType;


    private void Start()
    {
        float currentVolume = 1f;

        if (AudioController.Instance != null)
        {
            switch (channelType)
            {
                case AudioChannelType.Master:
                    currentVolume = AudioController.Instance.MasterVolume;
                    break;
                case AudioChannelType.Music:
                    currentVolume = AudioController.Instance.MusicVolume;
                    break;
                case AudioChannelType.SFX:
                    currentVolume = AudioController.Instance.SFXVolume;
                    break;
                case AudioChannelType.Voice:
                    currentVolume = AudioController.Instance.VoiceVolume;
                    break;
            }
        }
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = currentVolume;
        slider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float newValue)
    {
        if (AudioController.Instance != null)
        {
            switch (channelType)
            {
                case AudioChannelType.Master:
                    AudioController.Instance.SetMasterVolume(newValue);
                    break;
                case AudioChannelType.Music:
                    AudioController.Instance.SetMusicVolume(newValue);
                    break;
                case AudioChannelType.SFX:
                    AudioController.Instance.SetSFXVolume(newValue);
                    break;
                case AudioChannelType.Voice:
                    AudioController.Instance.SetVoiceVolume(newValue);
                    break;
            }
        }

        UpdateLabel(newValue);
    
    }

    private void UpdateLabel(float volume)
    {
        if (label != null)
        {
            int percentage = Mathf.RoundToInt(volume * 100f);
            label.text = percentage.ToString();
        }
}
}