using UnityEngine;

/// <summary>
/// Habilidade da Caipora: Pé de Vento
/// Aumenta a velocidade de movimento da Naia em +60% temporariamente por 5 segundos.
/// </summary>
[CreateAssetMenu(fileName = "Ability_PeVento", menuName = "Praia Games/Habilidades/Caipora/Pé de Vento")]
public class WindFootAbility : Ability
{
    [Header("Configurações do Pé de Vento")]
    [Tooltip("Multiplicador de velocidade (ex: 1.6 = +60% de velocidade).")]
    [SerializeField] private float speedMultiplier = 1.6f;

    [Tooltip("Duração da aceleração em segundos.")]
    [SerializeField] private float buffDuration = 5.0f;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        CharacterController2D controller = caster.GetComponent<CharacterController2D>();
        if (controller != null)
        {
            controller.ApplySpeedBuff(speedMultiplier, buffDuration);

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayImpactBurst(caster.transform.position, new Color(0.3f, 0.9f, 0.9f), 2.2f);
                CombatVisualEffects.Instance.SpawnFloatingText(caster.transform.position + Vector3.up * 1.0f, "💨 Pé de Vento (+60% Vel)!", new Color(0.4f, 1f, 1f), 3.5f);
            }

            Debug.Log($"[WindFootAbility] Pé de Vento ativado: x{speedMultiplier:F1} velocidade por {buffDuration}s!");
            return true;
        }

        return false;
    }
}
