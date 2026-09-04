using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Componente anexado a inimigos para gerenciar Efeitos de Status:
/// - Enraizamento (Root / Imobilização)
/// - Veneno (Poison DoT periódico)
/// - Encantamento (Charm / Confusão que faz atacar outros inimigos)
/// - Empurrão (Knockback Direcional)
/// </summary>
public class StatusEffectReceiver : MonoBehaviour
{
    private EnemyAIController aiController;
    private NavMeshAgent agent;
    private EnemyStats stats;
    private Rigidbody2D rb;
    private SpriteRenderer sr;

    private Coroutine rootCoroutine;
    private Coroutine poisonCoroutine;
    private Coroutine charmCoroutine;
    private Coroutine knockbackCoroutine;
    private Coroutine hunterMarkCoroutine;

    private bool isRooted = false;
    private bool isCharmed = false;
    private bool isHunterMarked = false;
    private float hunterMarkMultiplier = 1.5f;
    private Transform charmedTarget;
    private GameObject activeMarkVisual;

    public bool IsRooted => isRooted;
    public bool IsCharmed => isCharmed;
    public bool IsHunterMarked => isHunterMarked;
    public float HunterMarkMultiplier => isHunterMarked ? hunterMarkMultiplier : 1.0f;
    public Transform CharmedTarget => charmedTarget;

    private void Awake()
    {
        aiController = GetComponent<EnemyAIController>();
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Imobiliza o inimigo no lugar pela duração informada (não consegue andar).
    /// </summary>
    public void ApplyRoot(float duration)
    {
        if (rootCoroutine != null) StopCoroutine(rootCoroutine);
        rootCoroutine = StartCoroutine(RootRoutine(duration));
    }

    private IEnumerator RootRoutine(float duration)
    {
        isRooted = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Feedback visual verde musgo
        if (sr != null) sr.color = new Color(0.6f, 0.9f, 0.5f, 1f);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.2f, 0.8f, 0.2f), 1.5f);
        }

        yield return new WaitForSeconds(duration);

        isRooted = false;
        if (sr != null) sr.color = Color.white;
        if (agent != null && agent.enabled && agent.isOnNavMesh && !isCharmed)
        {
            agent.isStopped = false;
        }
        rootCoroutine = null;
    }

    /// <summary>
    /// Aplica Veneno causando dano periódico por ticks.
    /// </summary>
    public void ApplyPoison(float damagePerTick, float interval, int ticks)
    {
        if (poisonCoroutine != null) StopCoroutine(poisonCoroutine);
        poisonCoroutine = StartCoroutine(PoisonRoutine(damagePerTick, interval, ticks));
    }

    private IEnumerator PoisonRoutine(float damagePerTick, float interval, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            yield return new WaitForSeconds(interval);

            if (stats != null && !stats.IsDead)
            {
                stats.TakeDamage(damagePerTick, Vector3.zero);
            }
            else if (TryGetComponent(out IDamageable dmg))
            {
                dmg.TakeDamage(damagePerTick, Vector3.zero);
            }

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 0.4f, new Color(0.5f, 0.1f, 0.8f), 0.8f);
            }
        }
        poisonCoroutine = null;
    }

    /// <summary>
    /// Encanta o inimigo fazendo-o focar e atacar outros inimigos por uma duração.
    /// </summary>
    public void ApplyCharm(float duration)
    {
        if (charmCoroutine != null) StopCoroutine(charmCoroutine);
        charmCoroutine = StartCoroutine(CharmRoutine(duration));
    }

    private IEnumerator CharmRoutine(float duration)
    {
        isCharmed = true;

        // Feedback visual rosa/magenta
        if (sr != null) sr.color = new Color(1f, 0.5f, 0.85f, 1f);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 0.8f, new Color(1f, 0.4f, 0.8f), 1.8f);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += 0.4f;
            FindNearestEnemyTarget();
            yield return new WaitForSeconds(0.4f);
        }

        isCharmed = false;
        charmedTarget = null;
        if (sr != null) sr.color = Color.white;
        charmCoroutine = null;
    }

    private void FindNearestEnemyTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 12f);
        float closestDist = float.MaxValue;
        Transform closest = null;

        foreach (var h in hits)
        {
            if (h.gameObject == gameObject) continue;
            if (h.CompareTag("Enemy") && h.TryGetComponent(out EnemyStats otherStats) && !otherStats.IsDead)
            {
                float dist = Vector3.Distance(transform.position, h.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = h.transform;
                }
            }
        }

        charmedTarget = closest;
    }

    /// <summary>
    /// Aplica empurrão direcional com desaceleração suave.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force, float duration = 0.25f)
    {
        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction.normalized, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration)
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }

        Collider2D col = GetComponent<Collider2D>();
        float elapsed = 0f;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.useLayerMask = true;
        filter.layerMask = ~(1 << gameObject.layer);

        RaycastHit2D[] hits = new RaycastHit2D[6];

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float currentForce = Mathf.Lerp(force, 0f, elapsed / duration);
            float stepDist = currentForce * Time.deltaTime;

            if (col != null)
            {
                int hitCount = col.Cast(dir, filter, hits, stepDist);
                for (int i = 0; i < hitCount; i++)
                {
                    if (hits[i].collider != null && !hits[i].collider.isTrigger && hits[i].collider.gameObject != gameObject)
                    {
                        stepDist = Mathf.Max(0f, hits[i].distance - 0.05f);
                        transform.position += dir * stepDist;
                        elapsed = duration; // Para o knockback ao atingir a parede
                        break;
                    }
                }
            }

            if (elapsed < duration)
            {
                transform.position += dir * stepDist;
            }
            yield return null;
        }

        if (agent != null && agent.enabled && !isRooted)
        {
            agent.isStopped = false;
        }
        knockbackCoroutine = null;
    }

    /// <summary>
    /// Marca o inimigo com a Marca do Caçador, fazendo-o receber dano aumentado por uma duração.
    /// </summary>
    public void ApplyHunterMark(float duration, float bonusMultiplier = 1.5f, GameObject markVisualPrefab = null)
    {
        if (hunterMarkCoroutine != null) StopCoroutine(hunterMarkCoroutine);
        hunterMarkCoroutine = StartCoroutine(HunterMarkRoutine(duration, bonusMultiplier, markVisualPrefab));
    }

    private IEnumerator HunterMarkRoutine(float duration, float bonusMultiplier, GameObject markVisualPrefab)
    {
        isHunterMarked = true;
        hunterMarkMultiplier = bonusMultiplier;
        Debug.Log($"[StatusEffectReceiver] '{gameObject.name}' marcado pela Marca do Caçador (+{(bonusMultiplier - 1f) * 100:F0}% de dano recebido por {duration:F1}s)!");

        if (activeMarkVisual != null) Destroy(activeMarkVisual);
        if (markVisualPrefab != null)
        {
            activeMarkVisual = Instantiate(markVisualPrefab, transform);
            activeMarkVisual.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 0.8f, new Color(1f, 0.4f, 0.1f), 1.8f);
        }

        yield return new WaitForSeconds(duration);

        isHunterMarked = false;
        hunterMarkMultiplier = 1.0f;
        if (activeMarkVisual != null)
        {
            Destroy(activeMarkVisual);
            activeMarkVisual = null;
        }
        hunterMarkCoroutine = null;
        Debug.Log($"[StatusEffectReceiver] Marca do Caçador em '{gameObject.name}' expirou.");
    }
}
