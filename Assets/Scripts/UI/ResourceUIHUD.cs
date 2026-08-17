using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla as barras de recursos do jogador na UI (Vida, Mana e Stamina).
/// Inscreve-se nos eventos do PlayerStats para atualização dinâmica em tempo real.
/// </summary>
public class ResourceUIHUD : MonoBehaviour
{
    [Header("Referência do Jogador")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Barra de Vida (Vermelha)")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Barra de Mana (Azul)")]
    [SerializeField] private Image manaBarFill;
    [SerializeField] private TextMeshProUGUI manaText;

    [Header("Barra de Stamina (Verde/Amarela)")]
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private TextMeshProUGUI staminaText;

    private void OnEnable()
    {
        FindPlayerStats();
        SubscribeEvents();
        InitializeUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Start()
    {
        FindPlayerStats();
        SubscribeEvents();
        InitializeUI();
    }

    private void FindPlayerStats()
    {
        if (playerStats == null)
        {
            playerStats = Object.FindAnyObjectByType<PlayerStats>();
        }
    }

    private void SubscribeEvents()
    {
        if (playerStats == null) return;

        UnsubscribeEvents();
        playerStats.OnHealthChanged += UpdateHealthBar;
        playerStats.OnManaChanged += UpdateManaBar;
        playerStats.OnStaminaChanged += UpdateStaminaBar;
    }

    private void UnsubscribeEvents()
    {
        if (playerStats == null) return;

        playerStats.OnHealthChanged -= UpdateHealthBar;
        playerStats.OnManaChanged -= UpdateManaBar;
        playerStats.OnStaminaChanged -= UpdateStaminaBar;
    }

    private void InitializeUI()
    {
        if (playerStats != null)
        {
            UpdateHealthBar(playerStats.CurrentHealth, playerStats.MaxHealth);
            UpdateManaBar(playerStats.CurrentMana, playerStats.MaxMana);
        }
    }

    public void UpdateHealthBar(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (healthBarFill != null) healthBarFill.fillAmount = ratio;
        if (healthText != null) healthText.text = $"{current:F0} / {max:F0}";
    }

    public void UpdateManaBar(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (manaBarFill != null) manaBarFill.fillAmount = ratio;
        if (manaText != null) manaText.text = $"{current:F0} / {max:F0}";
    }

    public void UpdateStaminaBar(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (staminaBarFill != null) staminaBarFill.fillAmount = ratio;
        if (staminaText != null) staminaText.text = $"{current:F0} / {max:F0}";
    }

    #region Setter API Dinâmica
    public void SetHealthReferences(Image fill, TextMeshProUGUI text)
    {
        healthBarFill = fill;
        healthText = text;
    }

    public void SetManaReferences(Image fill, TextMeshProUGUI text)
    {
        manaBarFill = fill;
        manaText = text;
    }

    public void SetStaminaReferences(Image fill, TextMeshProUGUI text)
    {
        staminaBarFill = fill;
        staminaText = text;
    }
    #endregion
}
