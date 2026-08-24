using UnityEngine;

/// <summary>
/// Habilidade da Caipora: Marca do Caçador
/// Marca um inimigo na mira por 8 segundos. O alvo marcado recebe 50% a mais de dano de todos os ataques.
/// </summary>
[CreateAssetMenu(fileName = "Ability_MarcaCacador", menuName = "Praia Games/Habilidades/Caipora/Marca do Caçador")]
public class HunterMarkAbility : Ability
{
    [Header("Configurações da Marca do Caçador")]
    [Tooltip("Duração da marca no inimigo em segundos.")]
    [SerializeField] private float markDuration = 8.0f;

    [Tooltip("Multiplicador de dano bônus aplicado contra o alvo (1.5 = +50% de dano).")]
    [SerializeField] private float damageBonusMultiplier = 1.5f;

    [Tooltip("Raio de busca/impacto da marcação.")]
    [SerializeField] private float castRadius = 1.5f;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        Vector3 origin = caster.transform.position + Vector3.up * 0.5f;
        Vector3 direction = (targetPosition - origin).normalized;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.right;

        // Tenta encontrar o inimigo diretamente ou na linha de visada
        GameObject targetEnemy = targetEntity;

        if (targetEnemy == null)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, castRadius, direction, range);
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                GameObject go = hit.collider.gameObject;
                if (go == caster) continue;

                if (go.CompareTag("Enemy") || go.GetComponent<IDamageable>() != null)
                {
                    float dist = Vector2.Distance(origin, hit.point);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        targetEnemy = go;
                    }
                }
            }
        }

        // Se ainda não encontrou, busca inimigo mais próximo do ponto alvo
        if (targetEnemy == null)
        {
            Collider2D[] overlap = Physics2D.OverlapCircleAll(targetPosition, 2.5f);
            foreach (var col in overlap)
            {
                if (col.gameObject == caster) continue;
                if (col.CompareTag("Enemy") || col.GetComponent<IDamageable>() != null)
                {
                    targetEnemy = col.gameObject;
                    break;
                }
            }
        }

        if (targetEnemy != null)
        {
            StatusEffectReceiver receiver = targetEnemy.GetComponent<StatusEffectReceiver>();
            if (receiver == null) receiver = targetEnemy.AddComponent<StatusEffectReceiver>();

            receiver.ApplyHunterMark(markDuration, damageBonusMultiplier);

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayImpactBurst(targetEnemy.transform.position + Vector3.up * 0.6f, new Color(1f, 0.45f, 0.1f), 2.2f);
                CombatVisualEffects.Instance.SpawnFloatingText(targetEnemy.transform.position + Vector3.up * 1.2f, "Marcado (+50% Dano)!", new Color(1f, 0.6f, 0.2f), 3.8f);
            }

            Debug.Log($"[HunterMarkAbility] Inimigo '{targetEnemy.name}' foi marcado pelo Caçador por {markDuration}s!");
            return true;
        }
        else
        {
            // Se disparou sem alvo direto, solta efeito visual na direção
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayImpactBurst(origin + direction * Mathf.Min(range, 4f), new Color(1f, 0.45f, 0.1f), 1.5f);
                CombatVisualEffects.Instance.SpawnFloatingText(origin + Vector3.up * 1f, "Nenhum alvo na mira!", new Color(1f, 0.8f, 0.4f), 3f);
            }
            return true;
        }
    }
}
