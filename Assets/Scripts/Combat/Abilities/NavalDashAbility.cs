using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Habilidade da Iara: Investida Naval
/// Dash aquático veloz que atravessa e fere inimigos no caminho causando 40 de dano e empurrão.
/// Possui proteção física para não ultrapassar colisores sólidos nem sair do mapa.
/// </summary>
[CreateAssetMenu(fileName = "Ability_InvestidaNaval", menuName = "Praia Games/Habilidades/Iara/Investida Naval")]
public class NavalDashAbility : Ability
{
    [Header("Configurações da Investida Naval")]
    [Tooltip("Distância máxima percorrida pelo dash.")]
    [SerializeField] private float dashDistance = 5.5f;

    [Tooltip("Duração do dash em segundos.")]
    [SerializeField] private float dashDuration = 0.22f;

    [Tooltip("Dano causado aos inimigos atravessados.")]
    [SerializeField] private float dashDamage = 40f;

    [Tooltip("Força de empurrão aplicada nos inimigos atingidos.")]
    [SerializeField] private float knockbackForce = 6.0f;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        MonoBehaviour runner = caster.GetComponent<MonoBehaviour>();
        if (runner != null)
        {
            runner.StartCoroutine(PerformNavalDashRoutine(caster, targetPosition));
            return true;
        }

        return false;
    }

    private IEnumerator PerformNavalDashRoutine(GameObject caster, Vector3 targetPosition)
    {
        Vector3 origin = caster.transform.position;
        Vector2 direction = ((Vector2)targetPosition - (Vector2)origin).normalized;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;

        Rigidbody2D rb = caster.GetComponent<Rigidbody2D>();
        Collider2D playerCol = caster.GetComponent<Collider2D>();

        // 1. Validação de Colisão Física contra Paredes e Bordas
        float maxAllowedDistance = dashDistance;

        if (playerCol != null)
        {
            ContactFilter2D solidFilter = new ContactFilter2D();
            solidFilter.useTriggers = false;
            solidFilter.useLayerMask = true;
            solidFilter.layerMask = ~(1 << caster.layer); // Colide com tudo exceto o próprio player

            RaycastHit2D[] hits = new RaycastHit2D[8];
            int hitCount = playerCol.Cast(direction, solidFilter, hits, dashDistance);

            for (int i = 0; i < hitCount; i++)
            {
                if (hits[i].collider != null && !hits[i].collider.isTrigger && !hits[i].collider.CompareTag("Enemy"))
                {
                    float safeDist = Mathf.Max(0f, hits[i].distance - 0.1f);
                    if (safeDist < maxAllowedDistance)
                    {
                        maxAllowedDistance = safeDist;
                    }
                }
            }
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayWaterBurst(origin, 1.6f);
        }

        float elapsed = 0f;
        float speed = maxAllowedDistance / Mathf.Max(0.01f, dashDuration);
        HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

        while (elapsed < dashDuration && maxAllowedDistance > 0.05f)
        {
            elapsed += Time.deltaTime;
            float step = speed * Time.deltaTime;

            // Verificação de segurança a cada frame
            if (playerCol != null)
            {
                ContactFilter2D solidFilter = new ContactFilter2D();
                solidFilter.useTriggers = false;
                solidFilter.useLayerMask = true;
                solidFilter.layerMask = ~(1 << caster.layer);

                RaycastHit2D[] hits = new RaycastHit2D[4];
                int hitCount = playerCol.Cast(direction, solidFilter, hits, step);
                for (int i = 0; i < hitCount; i++)
                {
                    if (hits[i].collider != null && !hits[i].collider.isTrigger && !hits[i].collider.CompareTag("Enemy"))
                    {
                        step = Mathf.Max(0f, hits[i].distance - 0.05f);
                        elapsed = dashDuration; // Encerra o avanço contra a parede
                        break;
                    }
                }
            }

            caster.transform.position += (Vector3)(direction * step);

            // Causa dano e empurrão nos inimigos interceptados durante o dash
            Collider2D[] enemyHits = Physics2D.OverlapCircleAll(caster.transform.position, 1.2f);
            foreach (var col in enemyHits)
            {
                if (col == null || col.gameObject == caster) continue;
                if (col.CompareTag("Enemy") || col.GetComponent<IDamageable>() != null)
                {
                    if (!hitEnemies.Contains(col.gameObject))
                    {
                        hitEnemies.Add(col.gameObject);
                        if (col.TryGetComponent(out IDamageable dmg))
                        {
                            dmg.TakeDamage(dashDamage, direction);
                        }

                        if (col.TryGetComponent(out StatusEffectReceiver status))
                        {
                            status.ApplyKnockback(direction, knockbackForce, 0.25f);
                        }

                        if (CombatVisualEffects.Instance != null)
                        {
                            CombatVisualEffects.Instance.PlayImpactBurst(col.transform.position, new Color(0.2f, 0.7f, 1f), 1.8f);
                        }
                    }
                }
            }

            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayWaterBurst(caster.transform.position, 1.8f);
            CombatVisualEffects.Instance.SpawnFloatingText(caster.transform.position + Vector3.up * 1f, "Investida Naval!", new Color(0.3f, 0.85f, 1f), 3.5f);
        }

        Debug.Log($"[NavalDashAbility] Investida Naval concluída! Inimigos atingidos: {hitEnemies.Count}");
    }
}
