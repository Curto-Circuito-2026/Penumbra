using System.Collections;
using UnityEngine;

/// <summary>
/// Helper do Poder do Mapinguari invocado pela Cuca.
/// Telegrafa uma sombra no chão sob o jogador, cai do céu em alta velocidade,
/// desfere um impacto/slam em área e desaparece.
/// </summary>
public class CucaMapinguariGhostSlam : MonoBehaviour
{
    [Header("Configurações do Slam")]
    [SerializeField] private float telegraphDuration = 0.75f;
    [SerializeField] private float fallSpeed = 35f;
    [SerializeField] private float slamRadius = 2.8f;
    [SerializeField] private float slamDamage = 25f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Componentes")]
    [SerializeField] private SpriteRenderer ghostRenderer;
    [SerializeField] private Transform shadowTransform;
    [SerializeField] private SpriteRenderer shadowRenderer;

    private Vector3 targetGroundPosition;
    private bool isSlamming = false;

    public void Initialize(Vector3 targetPos, LayerMask playerMask)
    {
        targetGroundPosition = targetPos;
        playerLayerMask = playerMask;
        transform.position = targetGroundPosition;

        StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        // 1. Posiciona o fantasma bem no alto fora da tela
        if (ghostRenderer != null)
        {
            ghostRenderer.transform.position = targetGroundPosition + Vector3.up * 14f;
            ghostRenderer.color = new Color(0.85f, 0.4f, 1f, 0.85f); // Tom espiritual roxo/mágico
        }

        // 2. Telegrafa a sombra no chão crescendo
        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / telegraphDuration;

            if (shadowTransform != null)
            {
                float scale = Mathf.Lerp(0.3f, slamRadius * 0.9f, t);
                shadowTransform.localScale = new Vector3(scale, scale * 0.5f, 1f);
            }
            if (shadowRenderer != null)
            {
                shadowRenderer.color = new Color(0.1f, 0.02f, 0.2f, Mathf.Lerp(0.2f, 0.75f, t));
            }

            yield return null;
        }

        // 3. Queda vertical ultra rápida
        if (ghostRenderer != null)
        {
            Vector3 startFallPos = ghostRenderer.transform.position;
            Vector3 groundPos = targetGroundPosition;
            float fallDuration = Vector3.Distance(startFallPos, groundPos) / fallSpeed;
            float fallElapsed = 0f;

            while (fallElapsed < fallDuration)
            {
                fallElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(fallElapsed / fallDuration);
                ghostRenderer.transform.position = Vector3.Lerp(startFallPos, groundPos, progress);
                yield return null;
            }

            ghostRenderer.transform.position = groundPos;
        }

        // 4. Impacto do Slam no chão!
        DoImpact();

        // 5. Fade out do fantasma
        if (ghostRenderer != null)
        {
            float fadeElapsed = 0f;
            Color initialColor = ghostRenderer.color;
            while (fadeElapsed < 0.35f)
            {
                fadeElapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, fadeElapsed / 0.35f);
                ghostRenderer.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                if (shadowRenderer != null) shadowRenderer.color = new Color(0f, 0f, 0f, alpha * 0.5f);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    private void DoImpact()
    {
        // Efeito de impacto e poeira mágica
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(targetGroundPosition, new Color(0.6f, 0.1f, 0.9f), 3.2f);
        }

        // Dano no player dentro do raio
        Collider2D[] hits = Physics2D.OverlapCircleAll(targetGroundPosition, slamRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") && hit.TryGetComponent(out IDamageable damageable))
            {
                Vector3 pushDir = (hit.transform.position - targetGroundPosition).normalized;
                if (pushDir == Vector3.zero) pushDir = Vector3.up;
                damageable.TakeDamage(slamDamage, pushDir);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.2f, 0.9f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
