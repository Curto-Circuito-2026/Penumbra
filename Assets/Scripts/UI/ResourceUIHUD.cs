using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla as barras de recursos do jogador na UI (Vida, Mana e Escudo).
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

    [Header("Barra de Escudo / Vigor (Opcional)")]
    [SerializeField] private Image shieldBarFill;
    [SerializeField] private TextMeshProUGUI shieldText;

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

    private void Update()
    {
        if (playerStats == null)
        {
            FindPlayerStats();
            if (playerStats != null)
            {
                SubscribeEvents();
                InitializeUI();
            }
        }
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
        playerStats.OnShieldChanged += UpdateShieldBar;
        playerStats.OnPlayerRespawned += InitializeUI;
    }

    private void UnsubscribeEvents()
    {
        if (playerStats == null) return;

        playerStats.OnHealthChanged -= UpdateHealthBar;
        playerStats.OnManaChanged -= UpdateManaBar;
        playerStats.OnShieldChanged -= UpdateShieldBar;
        playerStats.OnPlayerRespawned -= InitializeUI;
    }

    public void InitializeUI()
    {
        if (manaBarFill != null)
        {
            if (manaBarFill.transform.parent != null && manaBarFill.transform.parent != transform)
            {
                manaBarFill.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                manaBarFill.gameObject.SetActive(false);
            }
        }
        if (manaText != null) manaText.gameObject.SetActive(false);

        if (playerStats != null)
        {
            UpdateHealthBar(playerStats.CurrentHealth, playerStats.MaxHealth);
            UpdateShieldBar(playerStats.CurrentShield);
        }
    }

    public void UpdateHealthBar(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (healthBarFill != null) healthBarFill.fillAmount = ratio;
        if (healthText != null)
        {
            if (playerStats != null && playerStats.HasShield)
            {
                healthText.text = $"{current:F0} <color=#64B5F6>(+{playerStats.CurrentShield:F0})</color> / {max:F0}";
            }
            else
            {
                healthText.text = $"{current:F0} / {max:F0}";
            }
        }
    }

    public void UpdateManaBar(float current, float max)
    {
        // Mana removida da interface
        if (manaBarFill != null) manaBarFill.gameObject.SetActive(false);
        if (manaText != null) manaText.gameObject.SetActive(false);
    }

    public void UpdateShieldBar(float currentShield)
    {
        if (shieldBarFill != null)
        {
            shieldBarFill.gameObject.SetActive(currentShield > 0f);
            if (playerStats != null && playerStats.MaxHealth > 0f)
            {
                shieldBarFill.fillAmount = Mathf.Clamp01(currentShield / playerStats.MaxHealth);
            }
        }

        if (shieldText != null)
        {
            shieldText.gameObject.SetActive(currentShield > 0f);
            shieldText.text = $"{currentShield:F0}";
        }

        if (playerStats != null)
        {
            UpdateHealthBar(playerStats.CurrentHealth, playerStats.MaxHealth);
        }
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

    public void SetShieldReferences(Image fill, TextMeshProUGUI text)
    {
        shieldBarFill = fill;
        shieldText = text;
    }
    #endregion
}
