using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Obstáculo Destrutível.
/// Possui vida, recebe dano, reproduz animação/efeito de quebra,
/// afeta o pathfinding do NavMesh (via NavMeshObstacle) e opcionalmente dropa um item ao ser destruído.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DestructibleObstacle : MonoBehaviour, IDamageable
{
    [Header("Atributos de Vida")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float currentHealth = 50f;

    [Header("Configuração de Drop de Item")]
    [Tooltip("Se verdadeiro, o obstáculo instanciará um item ao ser destruído.")]
    [SerializeField] private bool dropsItem = false;

    [Tooltip("Prefab do objeto/item a ser dropado.")]
    [SerializeField] private GameObject itemToDropPrefab;

    [Tooltip("Chance de drop (0 a 1). Ex: 1 = 100% de chance.")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 1f;

    [Tooltip("Quantidade mínima de itens a dropar.")]
    [SerializeField] private int minDropCount = 1;

    [Tooltip("Quantidade máxima de itens a dropar.")]
    [SerializeField] private int maxDropCount = 1;

    [Header("Animação e Efeitos Visuais")]
    [Tooltip("Animator do obstáculo (opcional). Se atribuído, disparará a trigger 'Break' ao ser destruído.")]
    [SerializeField] private Animator animator;

    [Tooltip("Prefab de partículas de quebra/destruição (opcional).")]
    [SerializeField] private ParticleSystem breakParticlePrefab;

    [Tooltip("Cor do flash quando o obstáculo recebe dano.")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.4f, 0.4f);

    [Tooltip("Tempo de atraso antes de destruir o GameObject para permitir a execução da animação de quebra.")]
    [SerializeField] private float destroyDelay = 0.5f;

    [Header("Componentes de Colisão e Pathfinding")]
    [SerializeField] private Collider2D obstacleCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    private bool isBroken = false;
    private Color originalColor = Color.white;
    private Coroutine flashCoroutine;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool DropsItem => dropsItem;
    public bool IsBroken => isBroken;

    public event Action<DestructibleObstacle> OnObstacleDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (obstacleCollider == null) obstacleCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (navMeshObstacle == null) navMeshObstacle = GetComponent<NavMeshObstacle>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // Garante que o NavMeshObstacle está configurado para esculpir o NavMesh
        ConfigureNavMeshObstacle();
    }

    private void ConfigureNavMeshObstacle()
    {
        if (navMeshObstacle == null)
        {
            navMeshObstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        navMeshObstacle.carving = true;

        if (obstacleCollider != null)
        {
            Bounds bounds = obstacleCollider.bounds;
            navMeshObstacle.shape = NavMeshObstacleShape.Box;
            navMeshObstacle.size = new Vector3(bounds.size.x, bounds.size.y, 1f);
            navMeshObstacle.center = obstacleCollider.offset;
        }
    }

    /// <summary>
    /// Aplica dano ao obstáculo destrutível.
    /// </summary>
    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        if (isBroken) return;

        currentHealth -= amount;
        if (currentHealth < 0f) currentHealth = 0f;

        Debug.Log($"[DestructibleObstacle] '{gameObject.name}' recebeu {amount:F0} de dano. Vida restante: {currentHealth:F0}/{maxHealth:F0}");

        // Feedback Visual de Dano
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.5f, $"-{amount:F0}", new Color(1f, 0.6f, 0.2f), 4f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.8f, 0.5f, 0.2f), 0.8f);
        }

        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashColor());
        }

        if (currentHealth <= 0f)
        {
            BreakObstacle();
        }
    }

    /// <summary>
    /// Processa a quebra do obstáculo, disparando animações, liberando o NavMesh e realizando o drop de item.
    /// </summary>
    private void BreakObstacle()
    {
        if (isBroken) return;
        isBroken = true;

        Debug.Log($"[DestructibleObstacle] Obstáculo '{gameObject.name}' foi DESTRUÍDO!");

        // 1. Libera o caminho no NavMesh desativando o NavMeshObstacle e a colisão
        if (obstacleCollider != null)
        {
            obstacleCollider.enabled = false;
        }

        if (navMeshObstacle != null)
        {
            navMeshObstacle.enabled = false;
        }

        // 2. Dispara animação de quebra no Animator (se configurado)
        if (animator != null)
        {
            animator.SetTrigger("Break");
            animator.SetBool("IsBroken", true);
        }

        // 3. Efeitos Visuais de Destruição
        if (breakParticlePrefab != null)
        {
            ParticleSystem particles = Instantiate(breakParticlePrefab, transform.position, Quaternion.identity);
            Destroy(particles.gameObject, particles.main.duration + particles.main.startLifetime.constantMax);
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.7f, "DESTRUÍDO!", new Color(1f, 0.3f, 0.1f), 5f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.9f, 0.4f, 0.1f), 2f);
        }

        // 4. Lógica de Drop de Item
        TryDropItem();

        OnObstacleDestroyed?.Invoke(this);

        // 5. Oculta Sprite ou destrói o objeto após o tempo de animação
        if (destroyDelay > 0f)
        {
            StartCoroutine(DestroyAfterDelay());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void TryDropItem()
    {
        if (dropsItem && itemToDropPrefab != null)
        {
            float randomRoll = UnityEngine.Random.value;
            if (randomRoll <= dropChance)
            {
                int count = UnityEngine.Random.Range(minDropCount, maxDropCount + 1);
                for (int i = 0; i < count; i++)
                {
                    Vector3 spawnOffset = (Vector3)(UnityEngine.Random.insideUnitCircle * 0.25f);
                    GameObject droppedItem = Instantiate(itemToDropPrefab, transform.position + spawnOffset, Quaternion.identity);

                    // Se o item tiver Rigidbody2D, aplica um pequeno impulso de salto ao cair
                    Rigidbody2D itemRb = droppedItem.GetComponent<Rigidbody2D>();
                    if (itemRb != null)
                    {
                        Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
                        itemRb.AddForce(randomDir * UnityEngine.Random.Range(1.5f, 2.5f) + Vector2.up * 1.5f, ForceMode2D.Impulse);
                    }
                }

                Debug.Log($"[DestructibleObstacle] {count} item(ns) dropado(s) por '{gameObject.name}'!");
            }
        }
    }

    private IEnumerator FlashColor()
    {
        spriteRenderer.color = damageFlashColor;
        yield return new WaitForSeconds(0.12f);
        spriteRenderer.color = originalColor;
    }

    private IEnumerator DestroyAfterDelay()
    {
        if (spriteRenderer != null && animator == null)
        {
            // Se não houver Animator, torna o sprite transparente/escondido imediatamente
            Color c = spriteRenderer.color;
            c.a = 0.3f;
            spriteRenderer.color = c;
        }

        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}
