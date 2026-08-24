using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VolumeSliderUI : MonoBehaviour
{
    [SerializeField] private Slider VolumeSlider;
    [SerializeField] private TextMeshProUGUI VolumeLabel;

    private bool isInitializing = false;

    private void Awake()
    {
        if (VolumeSlider == null) VolumeSlider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (VolumeSlider != null)
        {
            VolumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            VolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        RefreshUI();
    }

    private void Start()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (VolumeSlider == null) VolumeSlider = GetComponent<Slider>();
        if (VolumeSlider == null) return;

        float currentVolume = AudioController.Instance != null ? AudioController.Instance.GetMasterVolume() : 1f;

        isInitializing = true;
        VolumeSlider.minValue = 0f;
        VolumeSlider.maxValue = 1f;
        VolumeSlider.SetValueWithoutNotify(currentVolume);
        UpdateLabel(currentVolume);
        isInitializing = false;
    }

    private void OnVolumeChanged(float newValue)
    {
        if (isInitializing) return;

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
            Canvas.ForceUpdateCanvases();
        }
    }
}