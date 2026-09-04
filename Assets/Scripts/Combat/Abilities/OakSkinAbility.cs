using UnityEngine;

/// <summary>
/// Habilidade da Caipora: Pele de Carvalho
/// Fortalece a pele de Naia como tronco de árvore, reduzindo o dano recebido em 50% por 6 segundos.
/// </summary>
[CreateAssetMenu(fileName = "Ability_PeleCarvalho", menuName = "Praia Games/Habilidades/Caipora/Pele de Carvalho")]
public class OakSkinAbility : Ability
{
    [Header("Configurações da Pele de Carvalho")]
    [Tooltip("Percentual de redução de dano (ex: 0.5 = 50% de redução).")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float damageReductionPercent = 0.5f;

    [Tooltip("Duração do buff defensivo em segundos.")]
    [SerializeField] private float buffDuration = 6.0f;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        PlayerStats stats = caster.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.ApplyDefenseBuff(damageReductionPercent, buffDuration);

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayImpactBurst(caster.transform.position + Vector3.up * 0.5f, new Color(0.2f, 0.8f, 0.3f), 2.5f);
                CombatVisualEffects.Instance.SpawnFloatingText(caster.transform.position + Vector3.up * 1.0f, "Pele de Carvalho!", new Color(0.4f, 1f, 0.4f), 3.5f);
            }

            Debug.Log($"[OakSkinAbility] Pele de Carvalho ativada: -{damageReductionPercent * 100:F0}% dano por {buffDuration}s!");
            return true;
        }

        return false;
    }
}
