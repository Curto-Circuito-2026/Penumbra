using UnityEngine;

/// <summary>
/// Habilidade da Iara: Turbilhão
/// Conjura um vórtex d'água violento que puxa os inimigos para o centro e causa dano contínuo por 4 segundos.
/// </summary>
[CreateAssetMenu(fileName = "Ability_Turbilhao", menuName = "Praia Games/Habilidades/Iara/Turbilhão")]
public class WhirlpoolAbility : Ability
{
    [Header("Configurações do Turbilhão")]
    [Tooltip("Raio de atração do vórtex em unidades.")]
    [SerializeField] private float aoeRadius = 4.2f;

    [Tooltip("Dano por tick periódico.")]
    [SerializeField] private float damageTick = 18f;

    [Tooltip("Duração do vórtex no chão em segundos.")]
    [SerializeField] private float vortexDuration = 4.0f;

    [Tooltip("Prefab da área do vórtex.")]
    [SerializeField] private GameObject whirlpoolPrefab;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        Vector3 spawnPos = targetPosition;

        // Limita o alcance de invocação a partir do caster
        float dist = Vector2.Distance(caster.transform.position, targetPosition);
        if (dist > range && range > 0f)
        {
            Vector3 dir = (targetPosition - caster.transform.position).normalized;
            spawnPos = caster.transform.position + dir * range;
        }

        GameObject vObj = null;
        if (whirlpoolPrefab != null)
        {
            vObj = Instantiate(whirlpoolPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            GameObject loaded = Resources.Load<GameObject>("Prefabs/WhirlpoolArea");
            if (loaded == null)
            {
#if UNITY_EDITOR
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/WhirlpoolArea.prefab");
#endif
            }

            if (loaded != null)
            {
                vObj = Instantiate(loaded, spawnPos, Quaternion.identity);
            }
            else
            {
                // Criação procedural em runtime se prefab não existir
                vObj = new GameObject("WhirlpoolArea_Dynamic");
                vObj.transform.position = spawnPos;
                var sr = vObj.AddComponent<SpriteRenderer>();
                sr.sprite = Resources.Load<Sprite>("Square");
                sr.color = new Color(0.2f, 0.6f, 1f, 0.5f);
                vObj.AddComponent<WhirlpoolArea>();
            }
        }

        if (vObj != null)
        {
            if (vObj.TryGetComponent(out WhirlpoolArea wp))
            {
                wp.Initialize(caster, damageTick, aoeRadius, vortexDuration);
            }

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayWaterBurst(spawnPos, aoeRadius);
                CombatVisualEffects.Instance.SpawnFloatingText(spawnPos + Vector3.up * 1f, "Turbilhão!", new Color(0.3f, 0.7f, 1f), 3.5f);
            }

            Debug.Log($"[WhirlpoolAbility] Turbilhão conjurado com sucesso em {spawnPos}!");
            return true;
        }

        return false;
    }
}
