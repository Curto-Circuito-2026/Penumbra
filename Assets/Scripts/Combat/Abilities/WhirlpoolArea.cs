using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Área do Turbilhão (Vórtex d'Água) invocado pela Iara.
/// Puxa continuamente os inimigos para o centro e causa pulsos de dano periódico.
/// </summary>
public class WhirlpoolArea : MonoBehaviour
{
    [Header("Configurações do Vórtex")]
    [SerializeField] private float radius = 4.0f;
    [SerializeField] private float pullForce = 6.5f;
    [SerializeField] private float damagePerTick = 16f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private float duration = 4.0f;

    private GameObject owner;
    private float tickTimer = 0f;
    private float lifeTimer = 0f;

    public void Initialize(GameObject caster, float customDamage, float customRadius, float customDuration)
    {
        owner = caster;
        if (customDamage > 0f) damagePerTick = customDamage;
        if (customRadius > 0f) radius = customRadius;
        if (customDuration > 0f) duration = customDuration;
    }

    private void Start()
    {
        lifeTimer = duration;
        tickTimer = 0.1f; // Primeiro tick logo no início

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayWaterBurst(transform.position, radius * 0.9f);
        }
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayWaterBurst(transform.position, radius * 1.2f);
            }
            Destroy(gameObject);
            return;
        }

        // Rotação visual contínua
        transform.Rotate(0f, 0f, -240f * Time.deltaTime);

        // Atração Contínua dos Inimigos
        PullEnemies();

        // Dano Periódico por Tick
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            ApplyDamageTick();
        }
    }

    private void PullEnemies()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var col in colliders)
        {
            if (col == null || col.gameObject == owner) continue;

            if (col.CompareTag("Enemy") || col.GetComponent<IDamageable>() != null)
            {
                // Não puxa o player
                if (col.CompareTag("Player")) continue;

                Vector2 pullDir = ((Vector2)transform.position - (Vector2)col.transform.position).normalized;
                float dist = Vector2.Distance(transform.position, col.transform.position);
                float strength = Mathf.Lerp(pullForce, pullForce * 0.3f, dist / radius);

                if (col.TryGetComponent(out Rigidbody2D rb))
                {
                    rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, pullDir * strength, Time.deltaTime * 6f);
                }
                else
                {
                    col.transform.position = Vector2.MoveTowards(col.transform.position, transform.position, strength * Time.deltaTime);
                }
            }
        }
    }

    private void ApplyDamageTick()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var col in colliders)
        {
            if (col == null || col.gameObject == owner) continue;

            if (col.CompareTag("Enemy") || col.GetComponent<IDamageable>() != null)
            {
                if (col.CompareTag("Player")) continue;

                if (col.TryGetComponent(out IDamageable dmg))
                {
                    Vector2 pullDir = ((Vector2)transform.position - (Vector2)col.transform.position).normalized;
                    dmg.TakeDamage(damagePerTick, pullDir);

                    if (CombatVisualEffects.Instance != null)
                    {
                        CombatVisualEffects.Instance.PlayImpactBurst(col.transform.position, new Color(0.2f, 0.6f, 1f), 1.0f);
                    }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
