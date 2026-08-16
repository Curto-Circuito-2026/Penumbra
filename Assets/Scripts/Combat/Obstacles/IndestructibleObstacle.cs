using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Obstáculo Indestrutível.
/// Bloqueia habilidades, projéteis e ataques sem sofrer dano.
/// Afeta o pathfinding do NavMesh (via NavMeshObstacle) contornando o objeto.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IndestructibleObstacle : MonoBehaviour, IDamageable
{
    [Header("Componentes de Colisão e Pathfinding")]
    [SerializeField] private Collider2D obstacleCollider;
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    private void Awake()
    {
        if (obstacleCollider == null) obstacleCollider = GetComponent<Collider2D>();
        if (navMeshObstacle == null) navMeshObstacle = GetComponent<NavMeshObstacle>();

        ConfigureNavMeshObstacle();
    }

    private void ConfigureNavMeshObstacle()
    {
        if (navMeshObstacle == null)
        {
            navMeshObstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        navMeshObstacle.carving = true;

        if (obstacleCollider != null)
        {
            Bounds bounds = obstacleCollider.bounds;
            navMeshObstacle.shape = NavMeshObstacleShape.Box;
            navMeshObstacle.size = new Vector3(bounds.size.x, bounds.size.y, 1f);
            navMeshObstacle.center = obstacleCollider.offset;
        }
    }

    /// <summary>
    /// Chamado quando uma habilidade, disparo ou ataque atinge este obstáculo.
    /// O obstáculo indestrutível ignora o dano e exibe feedback de bloqueio.
    /// </summary>
    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        Debug.Log($"[IndestructibleObstacle] Ataque de {amount:F0} de dano foi BLOQUEADO por '{gameObject.name}'!");

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.5f, "BLOQUEADO!", new Color(0.7f, 0.7f, 0.8f), 3.8f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.6f, 0.7f, 0.9f), 1f);
        }
    }
}
