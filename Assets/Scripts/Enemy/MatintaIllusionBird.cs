using System.Collections;
using UnityEngine;

/// <summary>
/// Representa os pássaros ilusórios que zanzam pela arena quando a Matinta entra no casulo.
/// Na conclusão da ilusão, todos os pássaros mergulham em alta velocidade em direção ao Player.
/// </summary>
public class MatintaIllusionBird : MonoBehaviour, IDamageable
{
    public enum BirdState { Wandering, Diving }

    [Header("Configurações")]
    [SerializeField] private float wanderSpeed = 5.2f;
    [SerializeField] private float diveSpeed = 11.5f;
    [SerializeField] private float diveDamage = 16f;
    [SerializeField] private LayerMask playerLayerMask;

    private BirdState currentState = BirdState.Wandering;
    private Transform playerTransform;
    private SpriteRenderer spriteRenderer;
    private Vector3 wanderCenter;
    private Vector3 currentWanderTarget;
    private float wanderChangeTimer = 0f;
    private Vector3 diveDirection;
    private bool hasHit = false;

    public float CurrentHealth => 1f;
    public float MaxHealth => 1f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (playerLayerMask == 0)
        {
            int pLayer = LayerMask.NameToLayer("Player");
            playerLayerMask = pLayer != -1 ? (1 << pLayer) : (1 << 0);
        }
    }

    private float wanderRadius = 8.0f;

    public void InitializeWander(Vector3 center, Transform player, float maxRadius = 8.0f)
    {
        wanderCenter = center;
        playerTransform = player;
        wanderRadius = maxRadius;
        currentState = BirdState.Wandering;
        PickNewWanderTarget();
    }

    private void Update()
    {
        if (currentState == BirdState.Wandering)
        {
            UpdateWander();
        }
        else if (currentState == BirdState.Diving)
        {
            UpdateDive();
        }
    }

    private void UpdateWander()
    {
        wanderChangeTimer -= Time.deltaTime;
        if (wanderChangeTimer <= 0f || Vector3.Distance(transform.position, currentWanderTarget) < 0.5f)
        {
            PickNewWanderTarget();
        }

        Vector3 moveDir = (currentWanderTarget - transform.position).normalized;
        transform.position += moveDir * (wanderSpeed * Time.deltaTime);

        // Flip no sprite dependendo da direção horizontal
        if (spriteRenderer != null && Mathf.Abs(moveDir.x) > 0.05f)
        {
            spriteRenderer.flipX = moveDir.x < 0f;
        }
    }

    private void PickNewWanderTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * Random.Range(1.5f, wanderRadius);
        currentWanderTarget = wanderCenter + new Vector3(randomCircle.x, randomCircle.y, 0f);
        wanderChangeTimer = Random.Range(0.6f, 1.4f);
    }

    public void LaunchDiveAttack(Transform player)
    {
        playerTransform = player;
        currentState = BirdState.Diving;
        Vector3 targetPos = (playerTransform != null) ? playerTransform.position : (transform.position + Vector3.down * 5f);
        diveDirection = (targetPos - transform.position).normalized;

        if (spriteRenderer != null && Mathf.Abs(diveDirection.x) > 0.05f)
        {
            spriteRenderer.flipX = diveDirection.x < 0f;
        }

        Destroy(gameObject, 3.5f);
    }

    private void UpdateDive()
    {
        if (hasHit) return;

        transform.position += diveDirection * (diveSpeed * Time.deltaTime);

        // Detecção de colisão no Player
        Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.45f, playerLayerMask);
        if (hit != null && !hit.isTrigger)
        {
            IDamageable dmg = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (dmg != null && !(dmg is MatintaBossController))
            {
                hasHit = true;
                dmg.TakeDamage(diveDamage, diveDirection);
                ExplodeBird();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || currentState != BirdState.Diving) return;

        if (((1 << other.gameObject.layer) & playerLayerMask) != 0 && !other.isTrigger)
        {
            IDamageable dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (dmg != null && !(dmg is MatintaBossController))
            {
                hasHit = true;
                dmg.TakeDamage(diveDamage, diveDirection);
                ExplodeBird();
            }
        }
    }

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        ExplodeBird();
    }

    private void ExplodeBird()
    {
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.2f, 0.1f, 0.3f), 0.8f);
        }
        Destroy(gameObject);
    }
}
