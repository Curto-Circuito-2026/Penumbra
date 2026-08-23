using System;
using UnityEngine;
using UnityEngine.Audio;

public enum AudioChannel
{
    Master,
    Music,
    SFX,
    Voice
}

public class AudioSettingsManager : MonoBehaviour
{
    private static AudioSettingsManager instance;
    
    public static AudioSettingsManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance=UnityEngine.Object.FindAnyObjectByType<AudioSettingsManager>();
            }
            return instance;
        }
    }
    [Header("Referencie o AudioMixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Nome dos Parâmetros Expostos no Mixer")]
    [SerializeField] private string masterParameter = "MasterVolume";
    [SerializeField] private string musicParameter = "MusicVolume";
    [SerializeField] private string sfxParameter = "SFXVolume";
    [SerializeField] private string voiceParameter = "VoiceVolume";

    [Header("Configurações")]
    [SerializeField] private float minVolume = -80f; // Volume mínimo em decibéis

    private const string PrefPrefix = "Volume_";
    private const float DefaultVolume = 100f; // Volume padrão em decibéis

    public event Action<AudioChannel, float> OnVolumeChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    private void Start()
    {
        foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
        {
            ApplyToMixer(channel, GetVolume(channel));
        }
    }

    public float GetVolume(AudioChannel channel)
    {
        return PlayerPrefs.GetFloat(PrefPrefix + channel, DefaultVolume);
    }

    public void SetVolume(AudioChannel channel, float percent0to100)
    {
        percent0to100 = Mathf.Clamp(percent0to100, 0f, 100f);

        ApplyToMixer(channel, percent0to100);

        PlayerPrefs.SetFloat(PrefPrefix + channel, percent0to100);
        PlayerPrefs.Save();

        OnVolumeChanged?.Invoke(channel, percent0to100);
    }

    private void ApplyToMixer(AudioChannel channel, float percent0to100)
    {
        if (audioMixer == null) return;

        string paramName = GetParamName(channel);
        if (string.IsNullOrEmpty(paramName)) return;

        float normalized = percent0to100 / 100f;

        float dB = normalized <= 0.0001f ? minVolume : Mathf.Log10(normalized) * 20f;

        audioMixer.SetFloat(paramName, dB);
    }

    private string GetParamName(AudioChannel channel)
    {
        switch (channel)
        {
            case AudioChannel.Master: return masterParameter;
            case AudioChannel.Music: return musicParameter;
            case AudioChannel.SFX: return sfxParameter;
            case AudioChannel.Voice: return voiceParameter;
            default: return null;
        }
    }
}
