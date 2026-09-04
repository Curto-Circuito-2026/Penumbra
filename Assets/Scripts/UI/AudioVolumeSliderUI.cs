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

    private bool isInitializing = false;

    private void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnVolumeChanged);
            slider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (AudioController.Instance != null)
        {
            AudioController.Instance.OnAudioVolumesChanged += HandleVolumesChanged;
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        if (AudioController.Instance != null)
        {
            AudioController.Instance.OnAudioVolumesChanged -= HandleVolumesChanged;
        }
    }

    private void Start()
    {
        RefreshUI();
    }

    private void HandleVolumesChanged(float master, float music, float sfx, float voice)
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (slider == null) slider = GetComponent<Slider>();
        if (slider == null) return;

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

        isInitializing = true;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(currentVolume);
        UpdateLabel(currentVolume);
        isInitializing = false;
    }

    private void OnVolumeChanged(float newValue)
    {
        if (isInitializing) return;

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