using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla o comportamento da Tela de Morte e a ação do botão Reiniciar.
/// </summary>
public class DeathUI : MonoBehaviour
{
    [Header("Referências de UI")]
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private PlayerStats playerStats;

    private void Awake()
    {
        FindPlayerStats();
        SetupButtonListener();
    }

    private void OnEnable()
    {
        FindPlayerStats();
        SetupButtonListener();

        if (statusText != null)
        {
            statusText.text = "<color=#FF0000>Status: MORTO</color>";
        }
    }

    private void FindPlayerStats()
    {
        if (playerStats == null)
        {
            playerStats = Object.FindAnyObjectByType<PlayerStats>();
        }
    }

    private void SetupButtonListener()
    {
        if (restartButton == null)
        {
            restartButton = GetComponentInChildren<Button>(true);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
        }
    }

    /// <summary>
    /// Ação do botão de reiniciar. Restaura o jogador e altera o status para JOGANDO.
    /// </summary>
    public void OnRestartClicked()
    {
        FindPlayerStats();

        if (playerStats != null)
        {
            playerStats.RestartPlayer();
        }
        else if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetPlaying();
        }

        Debug.Log("[DeathUI] Botão Reiniciar clicado. O jogador renasceu e o estado voltou para JOGANDO!");
    }

    public void SetRestartButton(Button btn)
    {
        restartButton = btn;
        SetupButtonListener();
    }

    public void SetStatusText(TextMeshProUGUI text)
    {
        statusText = text;
    }
}
