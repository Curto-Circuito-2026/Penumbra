using System.Collections;
using UnityEngine;

/// <summary>
/// Helper do Poder do Boitatá invocado pela Cuca.
/// Telegrafa uma trajetória de perigo em linha reta e dispara uma investida
/// fantasma flamejante cruzando a arena em alta velocidade.
/// </summary>
public class CucaBoitataGhostDash : MonoBehaviour
{
    [Header("Configurações da Investida")]
    [SerializeField] private float telegraphDuration = 0.65f;
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDamage = 22f;

    [Header("Componentes")]
    [SerializeField] private SpriteRenderer ghostRenderer;
    [SerializeField] private LineRenderer telegraphLine;
    [SerializeField] private TrailRenderer fireTrail;
    [SerializeField] private Collider2D hitCollider;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private bool isDashing = false;
    private bool hasDamagedPlayer = false;

    public void Initialize(Vector3 start, Vector3 end)
    {
        startPosition = start;
        endPosition = end;
        transform.position = startPosition;

        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        // 1. Telegrafia de trajetória com linha de perigo
        if (telegraphLine != null)
        {
            telegraphLine.enabled = true;
            telegraphLine.positionCount = 2;
            telegraphLine.SetPosition(0, startPosition);
            telegraphLine.SetPosition(1, endPosition);
        }

        if (ghostRenderer != null) ghostRenderer.enabled = false;
        if (fireTrail != null) fireTrail.emitting = false;
        if (hitCollider != null) hitCollider.enabled = false;

        yield return new WaitForSeconds(telegraphDuration);

        if (telegraphLine != null) telegraphLine.enabled = false;

        // 2. Inicia o dash com a Serpente Boitatá visível e colorida
        if (ghostRenderer != null)
        {
            ghostRenderer.enabled = true;
            ghostRenderer.color = Color.white; // Mostra a serpente com cores plenas e rastro de fogo
        }
        if (fireTrail != null) fireTrail.emitting = true;
        if (hitCollider != null) hitCollider.enabled = true;

        isDashing = true;
        Vector3 dir = (endPosition - startPosition).normalized;
        float totalDist = Vector3.Distance(startPosition, endPosition);
        float duration = totalDist / dashSpeed;
        float elapsed = 0f;

        // Rotação em direção à investida
        if (dir != Vector3.zero)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, endPosition, progress);

            yield return null;
        }

        transform.position = endPosition;
        isDashing = false;

        // 3. Efeito de impacto no final
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(endPosition, new Color(1f, 0.35f, 0.1f), 2.5f);
        }

        yield return new WaitForSeconds(0.2f);

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isDashing || hasDamagedPlayer) return;

        if (collision.CompareTag("Player") && collision.TryGetComponent(out IDamageable dmg))
        {
            hasDamagedPlayer = true;
            Vector3 pushDir = (endPosition - startPosition).normalized;
            dmg.TakeDamage(dashDamage, pushDir);
        }
    }
}
