using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Runtime.CompilerServices;

public class VolumeSliderUI : MonoBehaviour
{
    [SerializeField] private Slider VolumeSlider;
    [SerializeField] private TextMeshProUGUI VolumeLabel;


    private void Start()
    {
        float currentVolume = AudioController.Instance != null ? AudioController.Instance.GetMasterVolume() : 1f;
        VolumeSlider.minValue = 0f;
        VolumeSlider.maxValue = 1f;
        VolumeSlider.value = currentVolume;

        VolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float newValue)
    {
        if (AudioController.Instance != null)
        {
            AudioController.Instance.SetMasterVolume(newValue);
        }

        UpdateLabel(newValue);
    }

    private void UpdateLabel(float volume)
    {
        if (VolumeLabel != null)
        {
            int percentage = Mathf.RoundToInt(volume * 100f);
            VolumeLabel.text = percentage.ToString();
            Canvas.ForceUpdateCanvases(); // Force the UI to update immediately
        }
    }
}