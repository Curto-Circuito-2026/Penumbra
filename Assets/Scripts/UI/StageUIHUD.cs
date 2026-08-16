using UnityEngine;
using TMPro;

/// <summary>
/// Exibe a Fase / Nível atual do Jogador na Interface (HUD).
/// Atualiza automaticamente via eventos do StageManager.
/// </summary>
public class StageUIHUD : MonoBehaviour
{
    [Header("Componentes de UI")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private string prefixText = "FASE ";

    private void Awake()
    {
        if (stageText == null) stageText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged += UpdateStageDisplay;
            UpdateStageDisplay(StageManager.Instance.CurrentStage);
        }
    }

    private void OnDisable()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged -= UpdateStageDisplay;
        }
    }

    private void Start()
    {
        if (StageManager.Instance != null)
        {
            UpdateStageDisplay(StageManager.Instance.CurrentStage);
        }
    }

    public void UpdateStageDisplay(int stageNumber)
    {
        if (stageText != null)
        {
            stageText.text = $"{prefixText}{stageNumber}";
        }
    }
}
