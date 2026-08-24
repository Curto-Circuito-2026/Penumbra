using Unity.VisualScripting;
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

    [SerializeField] private Sprite image;

    [Header("Efeitos Visuais")]
    [SerializeField] private Color impactColor = new Color(0.2f, 0.8f, 1f, 1f);

    private Vector3 moveDirection;
    private GameObject caster;
    private bool isInitialized = false;

    public float Damage => damage;

    /// <summary>
    /// Inicializa o projétil com a direção travada no instante do disparo, o conjurador e as layers alvo.
    /// </summary>
    public void Initialize(Vector3 direction, GameObject casterObject, float projectileDamage = -1f, LayerMask targetLayers = default, Sprite image = null)
    {
        moveDirection = direction.normalized;
        if (moveDirection.sqrMagnitude < 0.001f) moveDirection = Vector3.right;

        caster = casterObject;
        if (projectileDamage > 0f) damage = projectileDamage;

        if (image != null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
            {
                sr = this.AddComponent<SpriteRenderer>();
            }

            sr.sprite = image;
        }

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

        // Ignora colisão física direta com o próprio conjurador e suas partes/filhos
        if (caster != null)
        {
            Collider2D myCol = GetComponent<Collider2D>();
            Collider2D[] casterCols = caster.GetComponentsInChildren<Collider2D>(true);
            if (myCol != null)
            {
                foreach (var c in casterCols)
                {
                    if (c != null) Physics2D.IgnoreCollision(myCol, c, true);
                }
            }
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

    private bool IsInvalidTarget(Collider2D col)
    {
        if (col == null) return true;
        if (col.gameObject == gameObject || col.transform.IsChildOf(transform)) return true;

        // Ignora o próprio conjurador, seus filhos e sua raiz
        if (caster != null)
        {
            if (col.gameObject == caster || col.transform.IsChildOf(caster.transform) || col.transform.root == caster.transform.root) return true;

            // Jogador não acerta a si mesmo
            if (caster.CompareTag("Player") && (col.CompareTag("Player") || col.GetComponentInParent<CharacterController2D>() != null || col.GetComponentInParent<PlayerStats>() != null)) return true;

            // Inimigos não acertam outros inimigos
            if (caster.CompareTag("Enemy") && (col.CompareTag("Enemy") || col.GetComponentInParent<EnemyStats>() != null)) return true;
        }

        // Ignora explicitamente Fightzone, triggers de arena, boundaries e áreas de transição sem IDamageable
        string colName = col.gameObject.name;
        if (colName.IndexOf("Fightzone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Fighzone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Trigger", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Zone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Bounds", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            IDamageable d = col.GetComponent<IDamageable>();
            if (d == null) return true;
        }

        // Ignora triggers que não possuem IDamageable
        if (col.isTrigger)
        {
            IDamageable dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (dmg == null) return true;
        }

        return false;
    }

    private void Update()
    {
        if (!isInitialized) return;

        Vector3 deltaMove = moveDirection * (speed * Time.deltaTime);
        float moveDistance = deltaMove.magnitude;

        // 1. RaycastAll na direção do movimento do frame
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, moveDirection, moveDistance + 0.2f, targetHitLayers);
        foreach (var hit in hits)
        {
            if (IsInvalidTarget(hit.collider)) continue;

            HandleHit(hit.collider.gameObject, hit.point);
            return;
        }

        // 2. OverlapCircleAll no centro atual do projétil
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, 0.35f, targetHitLayers);
        foreach (var overlap in overlaps)
        {
            if (IsInvalidTarget(overlap)) continue;

            HandleHit(overlap.gameObject, transform.position);
            return;
        }

        transform.position += deltaMove;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isInitialized) return;
        if (IsInvalidTarget(collision)) return;

        if (((1 << collision.gameObject.layer) & targetHitLayers) != 0)
        {
            HandleHit(collision.gameObject, transform.position);
        }
    }

    private void HandleHit(GameObject hitObject, Vector3 impactPoint)
    {
        // Aplica dano ao Player, Inimigo ou Obstáculo Destrutível que implementa IDamageable
        IDamageable damageable = hitObject.GetComponent<IDamageable>() ?? hitObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
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
