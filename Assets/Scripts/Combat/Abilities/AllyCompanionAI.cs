using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador de IA para o Besta-Fera aliado espectral invocado pela Caipora.
/// Segue o jogador, ataca inimigos e chefes com mordidas e garras, e expira após uma duração.
/// </summary>
public class AllyCompanionAI : MonoBehaviour, IDamageable
{
    [Header("Atributos do Companheiro")]
    [SerializeField] private float maxHealth = 60f;
    [SerializeField] private float currentHealth = 60f;
    [SerializeField] private float attackDamage = 22f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float detectionRadius = 11f;
    [SerializeField] private float followDistance = 2.2f;
    [SerializeField] private float lifetime = 15f;

    private Transform playerTransform;
    private NavMeshAgent agent;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Animator animator;
    private Transform currentTarget;
    private float attackTimer = 0f;
    private bool isDead = false;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = 4.5f;
            agent.stoppingDistance = attackRange * 0.8f;
        }

        // Visual Espectral Brilhante e Visível (Verde-Esmeralda / Ciano da Caipora)
        if (sr != null)
        {
            sr.color = new Color(0.45f, 1f, 0.88f, 0.95f);
            sr.sortingOrder = 15; // Sempre acima do chão, grama e sombras
        }

        // Aura Mística nos pés
        CreateAuraUnderFeet();
    }

    private void CreateAuraUnderFeet()
    {
        Transform existing = transform.Find("Companion_Aura");
        if (existing != null) return;

        GameObject aura = new GameObject("Companion_Aura");
        aura.transform.SetParent(transform, false);
        aura.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        aura.transform.localScale = new Vector3(1.4f, 0.65f, 1f);
        SpriteRenderer auraSr = aura.AddComponent<SpriteRenderer>();
        auraSr.sprite = GetOrCreateAuraSprite();
        auraSr.color = new Color(0.25f, 1f, 0.70f, 0.50f);
        auraSr.sortingOrder = 14;
    }

    private static Sprite auraCachedSprite;
    private static Sprite GetOrCreateAuraSprite()
    {
        if (auraCachedSprite != null) return auraCachedSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float radius = (size - 1) / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        auraCachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return auraCachedSprite;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isDead)
        {
            Debug.Log("[AllyCompanionAI] O Besta-Fera espectral cumpriu seu tempo e retornou à floresta.");
            DespawnSpectral();
        }
    }

    private void Update()
    {
        if (isDead) return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            return;
        }

        attackTimer -= Time.deltaTime;

        // Procura alvo inimigo mais próximo
        FindClosestEnemy();

        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.position);
            FlipTowards(currentTarget.position);

            if (dist <= attackRange)
            {
                StopMovement();
                if (attackTimer <= 0f)
                {
                    PerformAttack(currentTarget.gameObject);
                }
            }
            else
            {
                MoveTowards(currentTarget.position);
            }
        }
        else
        {
            // Segue o jogador
            float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            FlipTowards(playerTransform.position);

            if (distToPlayer > followDistance)
            {
                MoveTowards(playerTransform.position);
            }
            else
            {
                StopMovement();
            }
        }

        if (animator != null)
        {
            bool isMoving = (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > 0.05f);
            animator.SetBool("IsMoving", isMoving);
        }
    }

    private void FindClosestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
        float closestDist = float.MaxValue;
        Transform bestTarget = null;

        foreach (var col in hits)
        {
            if (col == null || col.gameObject == gameObject) continue;
            if (playerTransform != null && col.gameObject == playerTransform.gameObject) continue;

            if (col.CompareTag("Enemy") || col.GetComponent<IDamageable>() != null)
            {
                // Ignora outros aliados
                if (col.GetComponent<AllyCompanionAI>() != null) continue;

                float d = Vector2.Distance(transform.position, col.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    bestTarget = col.transform;
                }
            }
        }

        currentTarget = bestTarget;
    }

    private void PerformAttack(GameObject target)
    {
        attackTimer = attackCooldown;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        if (target != null && target.TryGetComponent(out IDamageable dmg))
        {
            Vector3 push = (target.transform.position - transform.position).normalized;
            dmg.TakeDamage(attackDamage, push);

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayImpactBurst(target.transform.position + Vector3.up * 0.5f, new Color(0.2f, 1f, 0.6f), 1.6f);
                CombatVisualEffects.Instance.SpawnFloatingText(target.transform.position + Vector3.up * 0.9f, $"🐾 -{attackDamage:F0}", new Color(0.3f, 1f, 0.7f), 3.2f);
            }
            Debug.Log($"[AllyCompanionAI] Besta-Fera mordeu '{target.name}' causando {attackDamage} de dano!");
        }
    }

    private void MoveTowards(Vector3 destination)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
        else
        {
            Vector3 dir = (destination - transform.position).normalized;
            transform.position += dir * (3.8f * Time.deltaTime);
        }
    }

    private void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void FlipTowards(Vector3 pos)
    {
        if (sr == null) return;
        sr.flipX = (pos.x < transform.position.x);
    }

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"[AllyCompanionAI] Besta-Fera espectral recebeu {damage:F1} de dano! Vida: {currentHealth:F1}/{maxHealth}");

        StartCoroutine(FlashColor());

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private IEnumerator FlashColor()
    {
        if (sr != null)
        {
            Color orig = sr.color;
            sr.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            sr.color = orig;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        DespawnSpectral();
    }

    private void DespawnSpectral()
    {
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 0.5f, new Color(0.3f, 1f, 0.8f), 2.2f);
        }
        Destroy(gameObject);
    }
}
