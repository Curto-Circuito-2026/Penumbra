using UnityEngine;

/// <summary>
/// Habilidade da Caipora: Companheiro de Caça
/// Invoca um Besta-Fera espectral aliado com pouca vida que luta ao lado da Naia por 15 segundos.
/// </summary>
[CreateAssetMenu(fileName = "Ability_CompanheiroCaca", menuName = "Praia Games/Habilidades/Caipora/Companheiro de Caça")]
public class HuntingCompanionAbility : Ability
{
    [Header("Configurações do Companheiro")]
    [Tooltip("Prefab do Besta-Fera aliado espectral.")]
    [SerializeField] private GameObject companionPrefab;

    [Tooltip("Duração da invocação em segundos.")]
    [SerializeField] private float duration = 15.0f;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        Vector3 spawnPos = caster.transform.position + new Vector3(1.2f, 0f, 0f);

        GameObject prefabToUse = companionPrefab;
        if (prefabToUse == null)
        {
#if UNITY_EDITOR
            prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Ally_BestaFera.prefab")
                ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Enemy_BestaFera.prefab");
#endif
        }

        GameObject allyObj = null;
        if (prefabToUse != null)
        {
            allyObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
            allyObj.name = "Ally_BestaFera";
            allyObj.tag = "Player";

            // Limpa qualquer componente hostil restante
            var enemyAI = allyObj.GetComponent<EnemyAIController>();
            if (enemyAI != null) Destroy(enemyAI);
            var enemyCombat = allyObj.GetComponent<EnemyCombatController>();
            if (enemyCombat != null) Destroy(enemyCombat);
            var enemyStats = allyObj.GetComponent<EnemyStats>();
            if (enemyStats != null) Destroy(enemyStats);

            if (allyObj.GetComponent<AllyCompanionAI>() == null)
            {
                allyObj.AddComponent<AllyCompanionAI>();
            }

            SpriteRenderer sr = allyObj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.45f, 1f, 0.88f, 0.95f);
                sr.sortingOrder = 15;
            }
        }

        if (allyObj != null)
        {
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayImpactBurst(spawnPos, new Color(0.2f, 1f, 0.7f), 3.0f);
                CombatVisualEffects.Instance.SpawnFloatingText(spawnPos + Vector3.up * 1.2f, "🐺 Companheiro Invocado!", new Color(0.3f, 1f, 0.8f), 3.8f);
            }

            Debug.Log("[HuntingCompanionAbility] Companheiro de Caça Besta-Fera invocado com sucesso!");
            return true;
        }

        return false;
    }
}
