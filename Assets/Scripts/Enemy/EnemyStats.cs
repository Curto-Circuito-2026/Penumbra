using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gerencia a Vida e Morte do Inimigo.
/// Implementa IDamageable para receber dano de todas as armas e habilidades do Player (faca, tiros, projéteis).
/// </summary>
public class EnemyStats : MonoBehaviour, IDamageable
{
    [Header("Atributos de Vida")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float currentHealth = 50f;

    [Header("Feedback Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float flashDuration = 0.12f;

    private Color originalColor = Color.white;
    private Coroutine flashCoroutine;
    private bool isDead = false;
    private float baseMaxHealth = -1f;

    private static readonly int DeathHash = Animator.StringToHash("Death");

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    public event Action<float, float> OnHealthChanged;
    public event Action OnEnemyDied;

    private void Awake()
    {
        if (baseMaxHealth <= 0f) baseMaxHealth = maxHealth;
        currentHealth = maxHealth;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    /// <summary>
    /// Escala a vida máxima e atual do inimigo com base no multiplicador da fase.
    /// </summary>
    public void ApplyLevelScaling(float healthMultiplier)
    {
        if (baseMaxHealth <= 0f) baseMaxHealth = maxHealth;

        maxHealth = Mathf.Round(baseMaxHealth * healthMultiplier);
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"[EnemyStats] '{gameObject.name}' escalado para Fase (Vida Máxima: {maxHealth:F0}, Multiplicador: {healthMultiplier:F2}x)");
    }

    /// <summary>
    /// Método da interface IDamageable para receber dano da faca, tiros e habilidades do Player.
    /// </summary>
    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth < 0f) currentHealth = 0f;

        Debug.Log($"[EnemyStats] '{gameObject.name}' recebeu {amount:F0} de dano. Vida restante: {currentHealth:F0}/{maxHealth:F0}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Feedback Visual de Dano Flutuante e Partículas
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.6f, $"-{amount:F0}", new Color(1f, 0.2f, 0.2f), 4.2f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(1f, 0.3f, 0.2f), 1f);
        }

        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashColor());
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[EnemyStats] Inimigo '{gameObject.name}' morreu!");

        // Dispara parâmetro de animação Death no Animator
        if (animator != null)
        {
            animator.SetTrigger(DeathHash);
        }

        // Notifica evento de morte para a IA e o gerenciador
        OnEnemyDied?.Invoke();

        // Desativa colisão
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Oculta/Destrói o objeto após animação de morte
        Destroy(gameObject, 2f);
    }

    private IEnumerator FlashColor()
    {
        spriteRenderer.color = damageFlashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}
