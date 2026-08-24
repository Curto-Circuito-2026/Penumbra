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

    [Header("Barra de Vida no Mundo (Opcional)")]
    [Tooltip("Se desmarcado, este inimigo/prefab NÃO terá barra de vida no mundo.")]
    [SerializeField] private bool useWorldHealthBar = true;
    [SerializeField] private float healthBarYOffset = 0.85f;
    [SerializeField] private bool hideHealthBarWhenFull = true;
    [SerializeField] private EnemyWorldHealthBar worldHealthBar;

    public bool UseWorldHealthBar
    {
        get => useWorldHealthBar;
        set
        {
            useWorldHealthBar = value;
            UpdateWorldHealthBarState();
        }
    }

    [Header("Configuração de Drop de Item")]
    [Tooltip("Se verdadeiro, o inimigo poderá dropar itens ao morrer.")]
    [SerializeField] private bool dropsItem = true;

    [Tooltip("Prefab do item a ser dropado (ex: Fragmento de Estrela).")]
    [SerializeField] private GameObject itemToDropPrefab;

    [Tooltip("Chance de drop (0 a 1). Ex: 0.7 = 70% de chance.")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 1f;

    [Tooltip("Quantidade mínima de itens a dropar.")]
    [SerializeField] private int minDropCount = 2;

    [Tooltip("Quantidade máxima de itens a dropar.")]
    [SerializeField] private int maxDropCount = 7;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool DropsItem => dropsItem;

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

    private void Start()
    {
        UpdateWorldHealthBarState();
    }

    public void UpdateWorldHealthBarState()
    {
        if (useWorldHealthBar)
        {
            if (worldHealthBar == null)
            {
                worldHealthBar = GetComponentInChildren<EnemyWorldHealthBar>();
                if (worldHealthBar == null)
                {
                    worldHealthBar = gameObject.AddComponent<EnemyWorldHealthBar>();
                }
            }
            worldHealthBar.enabled = true;
            worldHealthBar.SetVisible(true);
            worldHealthBar.Configure(healthBarYOffset, hideHealthBarWhenFull);
        }
        else if (worldHealthBar != null)
        {
            worldHealthBar.enabled = false;
            worldHealthBar.SetVisible(false);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && worldHealthBar != null)
        {
            worldHealthBar.enabled = useWorldHealthBar;
            worldHealthBar.SetVisible(useWorldHealthBar);
        }
    }
#endif

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

        if (TryGetComponent(out StatusEffectReceiver receiver) && receiver.IsHunterMarked)
        {
            float mult = receiver.HunterMarkMultiplier;
            amount *= mult;
            Debug.Log($"[EnemyStats] Marca do Caçador amplificou o dano para {amount:F1} (x{mult:F1})!");
        }

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

    [SerializeField] private float deathDespawnDelay = 0.55f; // Tempo para a animação de morte terminar antes de sumir

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

        // Se for um inimigo suicida/creeper, deixa o ExplodeState gerenciar a contagem do pavio, animação e destruição
        EnemyAIController ai = GetComponent<EnemyAIController>();
        if (ai != null && ai.CanExplode)
        {
            return;
        }

        // Tenta dropar loot (fragmentos de estrela)
        TryDropLoot();

        // Desativa colisões
        Collider2D[] cols = GetComponentsInChildren<Collider2D>();
        foreach (var c in cols)
        {
            if (c != null) c.enabled = false;
        }

        // Desativa NavMeshAgent imediatamente
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Oculta/Destrói o objeto logo após o término da animação de morte
        StartCoroutine(DeathDespawnRoutine());
    }

    private IEnumerator DeathDespawnRoutine()
    {
        yield return new WaitForSeconds(deathDespawnDelay);

        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            Color startColor = spriteRenderer.color;
            while (elapsed < 0.12f)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(startColor.a, 0f, elapsed / 0.12f);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, a);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    private bool lootDropped = false;

    /// <summary>
    /// Elimina o inimigo imediatamente (ex: explosão suicida / creeper),
    /// ocultando o sprite e todos os colisores no mesmo frame sem delay.
    /// </summary>
    public void KillImmediate(bool dropLoot = true)
    {
        if (dropLoot && !lootDropped)
        {
            lootDropped = true;
            TryDropLoot();
        }

        if (!isDead)
        {
            isDead = true;
            OnEnemyDied?.Invoke();
        }

        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
        if (worldHealthBar != null) worldHealthBar.gameObject.SetActive(false);

        Destroy(gameObject);
    }

    private void TryDropLoot()
    {
        if (!dropsItem) return;

        GameObject prefabToDrop = itemToDropPrefab;
        if (prefabToDrop == null)
        {
            // Fallback seguro carregando o fragmento de estrela comum
            prefabToDrop = Resources.Load<GameObject>("Items/StarFragment_Pickup");
        }

        if (prefabToDrop == null) return;

        float roll = UnityEngine.Random.value;
        if (roll <= dropChance)
        {
            int count = UnityEngine.Random.Range(minDropCount, maxDropCount + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnOffset = (Vector3)(UnityEngine.Random.insideUnitCircle * 0.3f);
                GameObject dropped = Instantiate(prefabToDrop, transform.position + spawnOffset, Quaternion.identity);

                Rigidbody2D rb = dropped.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
                    rb.AddForce(randomDir * UnityEngine.Random.Range(1.5f, 3f) + Vector2.up * 1f, ForceMode2D.Impulse);
                }
            }

            Debug.Log($"[EnemyStats] Inimigo '{gameObject.name}' dropou {count} item(ns)!");
        }
    }

    private IEnumerator FlashColor()
    {
        spriteRenderer.color = damageFlashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}
