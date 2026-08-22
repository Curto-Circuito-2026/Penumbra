using UnityEngine;

/// <summary>
/// Habilidade da Iara: Escudo Bolha
/// Cria uma bolha d'água protetora sobre a Naia que absorve até 80 de dano por 8 segundos.
/// </summary>
[CreateAssetMenu(fileName = "Ability_EscudoBolha", menuName = "Praia Games/Habilidades/Iara/Escudo Bolha")]
public class BubbleShieldAbility : Ability
{
    [Header("Configurações do Escudo Bolha")]
    [Tooltip("Quantidade de dano que o escudo absorve antes de estourar.")]
    [SerializeField] private float shieldAmount = 80f;

    [Tooltip("Duração máxima do escudo em segundos.")]
    [SerializeField] private float shieldDuration = 8.0f;

    [Tooltip("Prefab visual opcional da bolha envolvendo o jogador.")]
    [SerializeField] private GameObject bubbleVisualPrefab;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        PlayerStats stats = caster.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.ApplyShield(shieldAmount, shieldDuration, bubbleVisualPrefab);

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayWaterBurst(caster.transform.position, 2.0f);
                CombatVisualEffects.Instance.SpawnFloatingText(caster.transform.position + Vector3.up * 1.1f, $"🛡️ Escudo Bolha ({shieldAmount:F0} HP)!", new Color(0.3f, 0.8f, 1f), 3.5f);
            }

            Debug.Log($"[BubbleShieldAbility] Escudo Bolha aplicado: {shieldAmount} de absorção por {shieldDuration}s!");
            return true;
        }

        return false;
    }
}
