using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gerencia a Vida, Mana e Estado de Morte do Jogador.
/// Implementa IDamageable para receber dano de inimigos.
/// </summary>
public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Atributos de Vida")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Atributos de Mana")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float currentMana = 100f;
    [SerializeField] private float manaRegenRate = 8f;

    [Header("Feedback de Dano")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float flashDuration = 0.15f;

    private Color originalColor = Color.white;
    private Coroutine flashCoroutine;
    private CharacterController2D characterController;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;
    public bool IsDead { get; private set; }

    // Eventos para atualização da UI
    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action<float, float> OnManaChanged;   // (current, max)
    public event Action<float, float> OnStaminaChanged; // (current, max)
    public event Action OnPlayerDied;
    public event Action OnPlayerRespawned;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        characterController = GetComponent<CharacterController2D>();
        currentHealth = maxHealth;
        currentMana = maxMana;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
    }

    private void Update()
    {
        if (IsDead) return;

        // Regeneração contínua de Mana
        if (currentMana < maxMana)
        {
            currentMana += manaRegenRate * Time.deltaTime;
            if (currentMana > maxMana) currentMana = maxMana;
            OnManaChanged?.Invoke(currentMana, maxMana);
        }

        // Transmite atualização de Stamina para a UI
        if (characterController != null)
        {
            OnStaminaChanged?.Invoke(characterController.CurrentStamina, characterController.MaxStamina);
        }
    }

    /// <summary>
    /// Aplica dano ao jogador e trata morte quando a vida chega a 0.
    /// </summary>
    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        if (IsDead) return;

        currentHealth -= amount;
        if (currentHealth < 0f) currentHealth = 0f;

        Debug.Log($"[PlayerStats] Jogador recebeu {amount} de dano! Vida restante: {currentHealth}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashDamageColor());
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Verifica se o jogador possui Mana suficiente para conjurar uma habilidade.
    /// </summary>
    public bool HasEnoughMana(float amount)
    {
        return currentMana >= amount;
    }

    /// <summary>
    /// Consome Mana do jogador se houver quantidade suficiente.
    /// </summary>
    public bool UseMana(float amount)
    {
        if (amount <= 0f) return true;

        if (currentMana >= amount)
        {
            currentMana -= amount;
            OnManaChanged?.Invoke(currentMana, maxMana);
            return true;
        }

        return false;
    }

    private void Die()
    {
        if (IsDead) return;

        IsDead = true;
        Debug.Log("[PlayerStats] O jogador morreu!");

        // Notifica evento de Morte
        OnPlayerDied?.Invoke();

        // Altera o estado do jogo para Dead no GameStateManager
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetDead();
        }
    }

    /// <summary>
    /// Reinicia a vida, mana e estado do jogador, alterando o estado do jogo de volta para Playing.
    /// </summary>
    public void RestartPlayer()
    {
        IsDead = false;
        currentHealth = maxHealth;
        currentMana = maxMana;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // Altera o estado do jogo para Playing no GameStateManager PRIMEIRO
        // para que a UI de Recursos fique ativa e receba as atualizações de Vida/Mana
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetPlaying();
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
        OnPlayerRespawned?.Invoke();

        Debug.Log("[PlayerStats] Jogador reiniciado com sucesso! Status alterado para JOGANDO.");
    }

    private IEnumerator FlashDamageColor()
    {
        spriteRenderer.color = damageFlashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}
