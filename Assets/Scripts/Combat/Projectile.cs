using UnityEngine;

/// <summary>
/// Projétil reutilizável para Player e Inimigos.
/// Movimenta-se em linha reta constante na direção definida no instante do disparo (sem perseguir o alvo).
/// Detecta colisões com entidades e obstáculos que implementam IDamageable (Player, Inimigos, Obstáculos Destrutíveis/Indestrutíveis).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Configurações do Projétil")]
    [SerializeField] private float speed = 14f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private LayerMask targetHitLayers;

    [Header("Efeitos Visuais")]
    [SerializeField] private Color impactColor = new Color(0.2f, 0.8f, 1f, 1f);

    private Vector3 moveDirection;
    private GameObject caster;
    private bool isInitialized = false;

    public float Damage => damage;

    /// <summary>
    /// Inicializa o projétil com a direção travada no instante do disparo, o conjurador e as layers alvo.
    /// </summary>
    public void Initialize(Vector3 direction, GameObject casterObject, float projectileDamage = -1f, LayerMask targetLayers = default)
    {
        moveDirection = direction.normalized;
        if (moveDirection.sqrMagnitude < 0.001f) moveDirection = Vector3.right;

        caster = casterObject;
        if (projectileDamage > 0f) damage = projectileDamage;

        // Garante que targetHitLayers nunca fique vazio (0)
        if (targetLayers != 0)
        {
            targetHitLayers = targetLayers;
        }
        else if (targetHitLayers == 0)
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            targetHitLayers = (1 << defaultLayer) | (1 << obstacleLayer);
        }

        isInitialized = true;

        // Aponta a rotação do projétil na direção do movimento 2D
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (Application.isPlaying)
        {
            Destroy(gameObject, maxLifetime);
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        Vector3 deltaMove = moveDirection * (speed * Time.deltaTime);
        float moveDistance = deltaMove.magnitude;

        // 1. RaycastAll na direção do movimento do frame (ignora triggers sem IDamageable)
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, moveDirection, moveDistance + 0.2f, targetHitLayers);
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.isTrigger && !hit.collider.TryGetComponent<IDamageable>(out _)) continue;
            if (caster != null && (hit.collider.gameObject == caster || hit.collider.transform.IsChildOf(caster.transform))) continue;

            HandleHit(hit.collider.gameObject, hit.point);
            return;
        }

        // 2. OverlapCircleAll no centro atual do projétil (ignora triggers sem IDamageable)
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, 0.35f, targetHitLayers);
        foreach (var overlap in overlaps)
        {
            if (overlap == null) continue;
            if (overlap.isTrigger && !overlap.TryGetComponent<IDamageable>(out _)) continue;
            if (caster != null && (overlap.gameObject == caster || overlap.transform.IsChildOf(caster.transform))) continue;

            HandleHit(overlap.gameObject, transform.position);
            return;
        }

        transform.position += deltaMove;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isInitialized) return;
        if (collision == null) return;
        if (collision.isTrigger && !collision.TryGetComponent<IDamageable>(out _)) return;
        if (caster != null && (collision.gameObject == caster || collision.transform.IsChildOf(caster.transform))) return;

        if (((1 << collision.gameObject.layer) & targetHitLayers) != 0)
        {
            HandleHit(collision.gameObject, transform.position);
        }
    }

    private void HandleHit(GameObject hitObject, Vector3 impactPoint)
    {
        // Aplica dano ao Player ou Obstáculo Destrutível que implementa IDamageable
        if (hitObject.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(damage, moveDirection);
            Debug.Log($"[Projectile] Projétil de '{caster?.name}' atingiu '{hitObject.name}' e causou {damage} de dano!");
        }

        // Feedback Visual de Impacto
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(impactPoint, impactColor, 1.2f);
        }

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }
}
