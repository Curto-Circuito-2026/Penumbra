using System.Collections;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Controla o comportamento do Fragmento de Estrela no cenário.
/// Ao passar por cima, o jogador coleta o fragmento, dispara feedback visual (fade out)
/// e incrementa o saldo de fragmentos no PlayerCurrency.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StarFragmentPickup : MonoBehaviour
{
    [Header("Configurações do Fragmento")]
    [Tooltip("Quantidade de fragmentos que este item concede ao ser coletado.")]
    [SerializeField] private int fragmentValue = 1;

    [Tooltip("Duração da animação de fade out ao ser coletado.")]
    [SerializeField] private float fadeDuration = 0.35f;

    [Tooltip("Cor do texto flutuante ao coletar.")]
    [SerializeField] private Color feedbackColor = new Color(1f, 0.88f, 0.2f);

    [Header("Movimento Automático / Trajetória até o Player")]
    [Tooltip("Se verdadeiro, viaja suavemente até o jogador independente da distância.")]
    [SerializeField] private bool seekPlayer = false;

    [Tooltip("Tempo de espera inicial antes de começar a voar em direção ao jogador.")]
    [SerializeField] private float initialDelay = 0.6f;

    [Tooltip("Velocidade inicial lenta.")]
    [SerializeField] private float startSpeed = 1.6f;

    [Tooltip("Velocidade máxima ao se aproximar.")]
    [SerializeField] private float maxSpeed = 10.0f;

    [Tooltip("Aceleração gradual do movimento.")]
    [SerializeField] private float acceleration = 3.8f;

    [Tooltip("Distância mínima para absorção automática.")]
    [SerializeField] private float autoCollectDistance = 0.85f;

    [Header("Efeito de Flutuação (Opcional)")]
    [Tooltip("Habilita uma leve animação de flutuação vertical contínua.")]
    [SerializeField] private bool enableBobbing = true;
    [SerializeField] private float bobbingSpeed = 3f;
    [SerializeField] private float bobbingHeight = 0.08f;

    [Header("Atração Magnética (Opcional - Usado se seekPlayer for falso)")]
    [Tooltip("Distância na qual o fragmento é atraído suavemente em direção ao jogador.")]
    [SerializeField] private float magnetRadius = 1.5f;
    [SerializeField] private float magnetSpeed = 6f;

    [Header("Animação de Piscar/Troca")]
    [SerializeField] private bool enableTwinkle = true;
    [SerializeField] private float twinkleInterval = 0.4f;
    [SerializeField] private Color twinkleColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Áudio de Coleta")]
    [SerializeField] private AudioClip collectSFX;
    [SerializeField] private float collectVolume = 0.35f;

    private SpriteRenderer spriteRenderer;
    private Collider2D pickupCollider;
    private Rigidbody2D rb;
    private bool isCollected = false;
    private bool isSeeking = false;
    private float currentSpeed;
    private float spawnTime;
    private Vector3 initialLocalPos;
    private Transform playerTransform;
    private Vector2 popVelocity;

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

        spawnTime = Time.time;
        currentSpeed = startSpeed;

        // Dispersão inicial suave
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        popVelocity = randomDir * Random.Range(1.2f, 2.4f);
    }

    private void Start()
    {
        FindPlayer();
        StartCoroutine(SeekSequenceRoutine());
        if (enableTwinkle)
        {
            StartCoroutine(TwinkleRoutine());
        }
    }

    private void FindPlayer()
    {
        if (playerTransform != null) return;
        GameObject playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private IEnumerator SeekSequenceRoutine()
    {
        float popDuration = initialDelay;
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            if (isCollected) yield break;

            elapsed += Time.deltaTime;
            float factor = 1f - (elapsed / popDuration);
            transform.position += (Vector3)(popVelocity * factor * Time.deltaTime);
            yield return null;
        }

        isSeeking = true;
    }

    private IEnumerator TwinkleRoutine()
    {
        while (!isCollected)
        {
            yield return new WaitForSeconds(twinkleInterval);
            if (isCollected) yield break;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = spriteRenderer.color == Color.white ? twinkleColor : Color.white;
            }
        }
    }

    private void Update()
    {
        if (isCollected) return;

        if (playerTransform == null)
        {
            FindPlayer();
        }

        if (seekPlayer && isSeeking && playerTransform != null)
        {
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.deltaTime, maxSpeed);
            Vector3 targetPos = playerTransform.position + new Vector3(0f, 0.5f, 0f);

            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentSpeed * Time.deltaTime);

            float dist = Vector2.Distance(transform.position, targetPos);
            if (dist <= autoCollectDistance)
            {
                PlayerCurrency currency = playerTransform.GetComponent<PlayerCurrency>() 
                                          ?? playerTransform.GetComponentInParent<PlayerCurrency>()
                                          ?? PlayerCurrency.Instance;
                if (currency != null)
                {
                    Collect(currency);
                }
            }
        }
        else
        {
            // Magnetismo clássico apenas se não estiver buscando
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
                float newY = Mathf.Sin((Time.time - spawnTime) * bobbingSpeed) * bobbingHeight;
                transform.position = new Vector3(transform.position.x, initialLocalPos.y + newY, transform.position.z);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        // Verifica se a colisão é com o Jogador
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

    /// <summary>
    /// Executa a coleta do fragmento, adicionando ao saldo do jogador e disparando a animação de fade out.
    /// </summary>
    public void Collect(PlayerCurrency currency)
    {
        if (isCollected) return;
        isCollected = true;

        // 1. Desativa colisor e física para evitar coletas múltiplas
        if (pickupCollider != null) pickupCollider.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // Restaura a cor normal antes de animar
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        // Toca áudio de coleta
        AudioClip sfx = collectSFX != null ? collectSFX : Resources.Load<AudioClip>("Audio/PegandoEstrela");
        if (sfx != null && AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(sfx, collectVolume);
        }
        else
        {
            Debug.LogWarning($"[StarFragmentPickup] Falha ao tocar áudio. Clip: {(sfx != null ? sfx.name : "null")}, AudioController: {(AudioController.Instance != null ? "ok" : "null")}");
        }

        // 2. Adiciona os fragmentos à carteira do jogador
        currency.AddStarFragments(fragmentValue);

        // 3. Feedback visual (Texto Flutuante e Partículas de impacto)
        if (CombatVisualEffects.Instance != null)
        {
            string label = fragmentValue > 1 ? $"+{fragmentValue} Fragmentos!" : "+1 Fragmento!";
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.4f, label, feedbackColor, 3.8f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, feedbackColor, 0.7f);
        }

        // 4. Executa a animação de Fade Out
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * 1.3f; // Leve expansão ao desaparecer

        if (spriteRenderer != null)
        {
            Color startColor = spriteRenderer.color;

            // Tenta usar animação de tween suave
            Tween.Alpha(spriteRenderer, 0f, fadeDuration, Ease.OutQuad);
            Tween.Scale(transform, targetScale, fadeDuration, Ease.OutQuad);

            yield return new WaitForSeconds(fadeDuration);
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        Destroy(gameObject);
    }
}
