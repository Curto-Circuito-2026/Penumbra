using UnityEngine;

/// <summary>
/// Controlador de Combate do Inimigo.
/// Reutiliza e replica EXATAMENTE as mecânicas de ataque Melee (Faca) e Ranged (Tiro de Projétil) do Player.
/// </summary>
public class EnemyCombatController : MonoBehaviour
{
    [Header("Máscara de Alvos do Inimigo")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Configurações Melee (Faca)")]
    [SerializeField] private float meleeDamage = 15f;
    [SerializeField] private float meleeRange = 1.8f;

    [Header("Configurações Ranged (Tiro em Linha Reta)")]
    [SerializeField] private float rangedDamage = 10f;
    [SerializeField] private GameObject defaultProjectilePrefab;

    private float baseMeleeDamage = -1f;
    private float baseRangedDamage = -1f;

    private void Awake()
    {
        if (baseMeleeDamage <= 0f) baseMeleeDamage = meleeDamage;
        if (baseRangedDamage <= 0f) baseRangedDamage = rangedDamage;

        if (targetLayers == 0)
        {
            // Ataca o Player (Default) e Obstáculos por padrão
            targetLayers = (1 << LayerMask.NameToLayer("Default")) | (1 << LayerMask.NameToLayer("Obstacle"));
        }
    }

    /// <summary>
    /// Escala os danos Melee e Ranged do inimigo com base no multiplicador da fase.
    /// </summary>
    public void ApplyLevelScaling(float damageMultiplier)
    {
        if (baseMeleeDamage <= 0f) baseMeleeDamage = meleeDamage;
        if (baseRangedDamage <= 0f) baseRangedDamage = rangedDamage;

        meleeDamage = Mathf.Round(baseMeleeDamage * damageMultiplier);
        rangedDamage = Mathf.Round(baseRangedDamage * damageMultiplier);

        Debug.Log($"[EnemyCombatController] '{gameObject.name}' escalado para Fase (Dano Melee: {meleeDamage:F0}, Dano Ranged: {rangedDamage:F0}, Multiplicador: {damageMultiplier:F2}x)");
    }

    /// <summary>
    /// Configura dinamicamente os valores de combate com base em um EnemyConfigSO.
    /// </summary>
    public void Configure(EnemyConfigSO config)
    {
        if (config == null) return;
        meleeDamage = config.meleeDamage;
        meleeRange = config.meleeRange;
        rangedDamage = config.rangedDamage;

        if (config.projectilePrefab != null)
        {
            defaultProjectilePrefab = config.projectilePrefab;
        }
    }

    /// <summary>
    /// Executa o ataque Melee de Faca replicando o mesmo corte e colisão do Player.
    /// </summary>
    public void PerformMeleeAttack(Vector3 direction)
    {
        Vector3 dir = direction.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = transform.right;

        Debug.Log($"[EnemyCombatController] '{gameObject.name}' executou Ataque Melee (Faca).");

        // Retorno Visual: Arco de corte da Faca idêntico ao do Player
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayMeleeSlash(transform.position, dir);
        }

        // RaycastAll para ignorar triggers e atingir o Player ou Obstáculos
        bool hitTarget = false;
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, dir, meleeRange, targetLayers);
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform) || hit.collider.transform.root == transform.root) continue;
            if (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyStats>() != null) continue;

            IDamageable damageable = hit.collider.GetComponent<IDamageable>() ?? hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && !(damageable is EnemyStats))
            {
                damageable.TakeDamage(meleeDamage, dir);
                hitTarget = true;
                break;
            }
        }

        if (!hitTarget)
        {
            // OverlapCircleAll no arco curto em frente como fallback de colisão da faca
            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position + dir * (meleeRange * 0.5f), meleeRange * 0.6f, targetLayers);
            foreach (var col in cols)
            {
                if (col == null) continue;
                if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.transform.root == transform.root) continue;
                if (col.CompareTag("Enemy") || col.GetComponentInParent<EnemyStats>() != null) continue;

                IDamageable hitDmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (hitDmg != null && !(hitDmg is EnemyStats))
                {
                    hitDmg.TakeDamage(meleeDamage, dir);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Executa o disparo Ranged lançando o mesmo Projétil em linha reta constante na direção capturada do Player.
    /// O tiro NÃO persegue o player.
    /// </summary>
    public void PerformRangedAttack(Vector3 direction, GameObject overrideProjectilePrefab = null)
    {
        Vector3 dir = direction.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = transform.right;

        GameObject prefabToUse = overrideProjectilePrefab != null ? overrideProjectilePrefab : defaultProjectilePrefab;
        if (prefabToUse == null)
        {
            var ai = GetComponent<EnemyAIController>();
            if (ai != null) prefabToUse = ai.ProjectilePrefab;
        }

        Debug.Log($"[EnemyCombatController] '{gameObject.name}' disparou Tiro Ranged em Linha Reta na direção {dir}.");

        // Offset de spawn de 0.9 unidades para sair limpo da frente do colisor do inimigo
        Vector3 spawnPos = transform.position + dir * 0.9f;

        // Muzzle flash de disparo na boca da arma
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(spawnPos, new Color(1f, 0.5f, 0.1f), 0.8f);
        }

        if (prefabToUse != null)
        {
            // Instancia o Prefab de Projétil do Inimigo
            GameObject projObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);

            if (projObj.TryGetComponent(out Projectile projectile))
            {
                // Inicializa o projétil na direção travada do disparo
                projectile.Initialize(dir, gameObject, rangedDamage, targetLayers);
            }
        }
        else if (CombatVisualEffects.Instance != null)
        {
            // Fallback visual com Raycast se nenhum prefab foi atribuído
            Vector3 targetImpactPos = transform.position + dir * 8f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, dir, 8f, targetLayers);
            GameObject hitTargetObj = null;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.gameObject == gameObject || h.collider.transform.IsChildOf(transform) || h.collider.transform.root == transform.root) continue;
                if (h.collider.CompareTag("Enemy") || h.collider.GetComponentInParent<EnemyStats>() != null) continue;

                hitTargetObj = h.collider.gameObject;
                targetImpactPos = h.point;
                break;
            }

            CombatVisualEffects.Instance.PlayRangedProjectile(spawnPos, targetImpactPos, () =>
            {
                if (hitTargetObj != null)
                {
                    IDamageable dmg = hitTargetObj.GetComponent<IDamageable>() ?? hitTargetObj.GetComponentInParent<IDamageable>();
                    if (dmg != null && !(dmg is EnemyStats))
                    {
                        dmg.TakeDamage(rangedDamage, dir);
                    }
                }
            });
        }
    }
}
