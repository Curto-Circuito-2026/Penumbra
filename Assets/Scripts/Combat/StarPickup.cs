using System.Collections;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Controla o comportamento de uma Estrela Forjada dropada (concedida por Chefes).
/// Ao passar por cima, a Naia coleta a estrela inteira, dispara feedback visual dourado
/// e incrementa diretamente o saldo de estrelas no PlayerCurrency (+1 Estrela).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StarPickup : MonoBehaviour
{
    [Header("Configurações da Estrela")]
    [Tooltip("Quantidade de estrelas inteiras que este item concede ao ser coletado.")]
    [SerializeField] private int starValue = 1;

    [Tooltip("Duração da animação de fade out ao ser coletado.")]
    [SerializeField] private float fadeDuration = 0.4f;

    [Tooltip("Cor do texto flutuante ao coletar.")]
    [SerializeField] private Color feedbackColor = new Color(1f, 0.85f, 0.15f);

    [Header("Efeito de Flutuação")]
    [SerializeField] private bool enableBobbing = true;
    [SerializeField] private float bobbingSpeed = 3.5f;
    [SerializeField] private float bobbingHeight = 0.12f;

    [Header("Atração Magnética")]
    [SerializeField] private float magnetRadius = 3f;
    [SerializeField] private float magnetSpeed = 8f;

    private SpriteRenderer spriteRenderer;
    private Collider2D pickupCollider;
    private Rigidbody2D rb;
    private bool isCollected = false;
    private Vector3 initialLocalPos;
    private Transform playerTransform;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        pickupCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }

        initialLocalPos = transform.position;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void Update()
    {
        if (isCollected) return;

        // Efeito de atração magnética quando o jogador está próximo
        if (playerTransform != null)
        {
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist <= magnetRadius && dist > 0.1f)
            {
                transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, magnetSpeed * Time.deltaTime);
                return;
            }
        }

        // Leve oscilação vertical quando parado
        if (enableBobbing && (rb == null || rb.linearVelocity.sqrMagnitude < 0.01f))
        {
            float newY = Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
            transform.position = new Vector3(transform.position.x, initialLocalPos.y + newY, transform.position.z);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        PlayerCurrency currency = other.GetComponent<PlayerCurrency>();
        if (currency == null && other.CompareTag("Player"))
        {
            currency = other.GetComponentInParent<PlayerCurrency>();
        }

        if (currency != null)
        {
            Collect(currency);
        }
    }

    private void Collect(PlayerCurrency currency)
    {
        isCollected = true;

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        currency.AddStars(starValue);

        // Feedback Visual
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.7f, $"+{starValue} Estrela!", feedbackColor, 4.5f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, feedbackColor, 1.5f);
        }

        StartCoroutine(CollectAnimationRoutine());
    }

    private IEnumerator CollectAnimationRoutine()
    {
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * 1.5f;

        if (spriteRenderer != null)
        {
            Tween.Scale(transform, targetScale, fadeDuration, Ease.OutQuad);
            Tween.Alpha(spriteRenderer, 0f, fadeDuration, Ease.InQuad);
        }

        yield return new WaitForSeconds(fadeDuration);
        Destroy(gameObject);
    }
}
