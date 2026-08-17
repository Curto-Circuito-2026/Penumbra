using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Boneco de Treino para testar o sistema de combate 2D.
/// Implementa IDamageable e pisca em vermelho ao receber dano.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TrainingDummy : MonoBehaviour, IDamageable
{
    [Header("Configurações do Boneco")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private float autoResetDelay = 3f;

    [Header("Feedback Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.15f;

    [Header("Texto de Dano / UI (Opcional)")]
    [SerializeField] private TextMeshPro healthText;

    private Color originalColor;
    private Coroutine flashCoroutine;
    private Coroutine resetCoroutine;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // Garante que a layer do Boneco esteja definida ou identificável como Inimigo
        if (gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                gameObject.layer = enemyLayer;
            }
        }

        currentHealth = maxHealth;
        UpdateHealthDisplay();
    }

    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        currentHealth -= amount;
        if (currentHealth < 0f) currentHealth = 0f;

        Debug.Log($"[TrainingDummy] {gameObject.name} recebeu {amount} de dano! Vida restante: {currentHealth}/{maxHealth}");

        UpdateHealthDisplay();

        // Efeito visual de piscar
        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashColor());
        }

        // Texto de Dano Flutuante e Efeito de Impacto
        if (CombatVisualEffects.Instance != null)
        {
            Color textCol = amount >= 40f ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.3f, 0.2f);
            float textSize = amount >= 40f ? 5.5f : 4.2f;
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.5f, $"-{amount:F0}", textCol, textSize);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, textCol, 1.2f);
        }

        // Se a vida zerar, reseta após o tempo configurado
        if (currentHealth <= 0f)
        {
            if (resetCoroutine != null) StopCoroutine(resetCoroutine);
            resetCoroutine = StartCoroutine(AutoResetHealth());
        }
    }

    private IEnumerator FlashColor()
    {
        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }

    private IEnumerator AutoResetHealth()
    {
        yield return new WaitForSeconds(autoResetDelay);
        currentHealth = maxHealth;
        UpdateHealthDisplay();
        Debug.Log($"[TrainingDummy] {gameObject.name} teve sua vida restaurada para {maxHealth}!");
    }

    private void UpdateHealthDisplay()
    {
        if (healthText != null)
        {
            healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
        }
    }
}
