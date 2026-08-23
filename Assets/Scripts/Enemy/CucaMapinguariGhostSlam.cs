using System.Collections;
using UnityEngine;

/// <summary>
/// Helper do Poder do Mapinguari invocado pela Cuca.
/// Telegrafa uma sombra e anel de perigo no chão sob o jogador, cai do céu em alta velocidade,
/// desfere um impacto/slam devastador em grande área com tremor de tela e desaparece.
/// </summary>
public class CucaMapinguariGhostSlam : MonoBehaviour
{
    [Header("Configurações do Slam")]
    [SerializeField] private float telegraphDuration = 1.4f;
    [SerializeField] private float fallSpeed = 24f;
    [SerializeField] private float slamRadius = 4.8f;
    [SerializeField] private float slamDamage = 35f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Componentes")]
    [SerializeField] private SpriteRenderer ghostRenderer;
    [SerializeField] private Transform shadowTransform;
    [SerializeField] private SpriteRenderer shadowRenderer;

    private Vector3 targetGroundPosition;
    private GameObject dangerRingObj;

    public void Initialize(Vector3 targetPos, LayerMask playerMask)
    {
        targetGroundPosition = targetPos;
        playerLayerMask = playerMask;
        transform.position = targetGroundPosition;

        StartCoroutine(SlamRoutine());
    }

    private static Sprite circleSpriteCache;
    private static Sprite GetOrCreateCircleSprite()
    {
        if (circleSpriteCache != null) return circleSpriteCache;
        int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float radius = res * 0.48f;
        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }
        tex.Apply();
        circleSpriteCache = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 32f);
        return circleSpriteCache;
    }

    private IEnumerator SlamRoutine()
    {
        // 1. Posiciona o fantasma bem no alto fora da tela
        if (ghostRenderer != null)
        {
            ghostRenderer.transform.position = targetGroundPosition + Vector3.up * 16f;
            ghostRenderer.color = new Color(0.85f, 0.4f, 1f, 0.85f); // Tom espiritual roxo/mágico
        }

        // 2. Garante sprite circular suave na sombra
        if (shadowRenderer != null)
        {
            shadowRenderer.sprite = GetOrCreateCircleSprite();
        }

        // 3. Cria anel pulsante de perigo na área de impacto
        dangerRingObj = new GameObject("Mapinguari_DangerRing");
        dangerRingObj.transform.position = targetGroundPosition;
        SpriteRenderer ringSr = dangerRingObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = GetOrCreateCircleSprite();
        ringSr.color = new Color(0.8f, 0.1f, 0.3f, 0.25f);
        ringSr.sortingOrder = -1;

        // 4. Telegrafa a sombra e a área no chão crescendo
        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / telegraphDuration;

            if (shadowTransform != null)
            {
                // Sombra oval grande crescente sob a queda
                float scaleX = Mathf.Lerp(2.0f, slamRadius * 2.2f, t);
                float scaleY = Mathf.Lerp(1.2f, slamRadius * 1.3f, t);
                shadowTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
            if (shadowRenderer != null)
            {
                shadowRenderer.color = new Color(0.12f, 0.01f, 0.18f, Mathf.Lerp(0.35f, 0.95f, t));
            }

            if (dangerRingObj != null)
            {
                float ringScale = Mathf.Lerp(1.5f, slamRadius * 2.1f, t);
                dangerRingObj.transform.localScale = new Vector3(ringScale, ringScale * 0.6f, 1f);
                float pulse = 0.2f + 0.15f * Mathf.Sin(Time.time * 16f);
                ringSr.color = new Color(0.9f, 0.15f, 0.4f, pulse);
            }

            yield return null;
        }

        if (dangerRingObj != null)
        {
            Destroy(dangerRingObj);
        }

        // 5. Queda vertical rápida
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

        // 6. Impacto estrondoso do Slam no chão!
        DoImpact();

        // 7. Fade out do fantasma
        if (ghostRenderer != null)
        {
            float fadeElapsed = 0f;
            Color initialColor = ghostRenderer.color;
            while (fadeElapsed < 0.4f)
            {
                fadeElapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, fadeElapsed / 0.4f);
                ghostRenderer.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                if (shadowRenderer != null) shadowRenderer.color = new Color(0f, 0f, 0f, alpha * 0.5f);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    private void DoImpact()
    {
        // Efeito de impacto massivo e explosão mágica
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(targetGroundPosition, new Color(0.7f, 0.15f, 1f), 4.5f);
        }

        // Camera Shake no impacto
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraManager cm))
        {
            cm.Shake(0.35f, 0.4f);
        }

        // Dano no player dentro da grande área
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
