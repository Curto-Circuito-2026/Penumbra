using System.Collections;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Controla o comportamento de uma Estrela Forjada dropada (concedida por Chefes).
/// Ao nascer, realiza um pop/dispersão suave e viaja automaticamente na direção da Naia,
/// garantindo absorção cinematográfica para cutscenes e progressão.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StarPickup : MonoBehaviour
{
    [Header("Configurações da Estrela")]
    [Tooltip("Quantidade de estrelas inteiras que este item concede ao ser coletado.")]
    [SerializeField] private int starValue = 1;

    [Tooltip("Duração da animação de fade out ao ser coletado.")]
    [SerializeField] private float fadeDuration = 0.35f;

    [Tooltip("Cor do feedback visual e rastro dourado.")]
    [SerializeField] private Color starGlowColor = new Color(1f, 0.88f, 0.2f, 1f);

    [Header("Movimento Automático / Trajetória até o Player")]
    [Tooltip("Se verdadeiro, viaja suavemente até o jogador independente da distância.")]
    [SerializeField] private bool seekPlayer = true;

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

    [Header("Efeito de Flutuação e Rotação")]
    [SerializeField] private bool enableSpin = true;
    [SerializeField] private float spinSpeed = 120f;
    [SerializeField] private bool enableBobbing = true;
    [SerializeField] private float bobbingSpeed = 4f;
    [SerializeField] private float bobbingHeight = 0.15f;

    public static int ActiveStarsCount { get; private set; } = 0;
    public static event System.Action OnAllStarsCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        ActiveStarsCount = 0;
        OnAllStarsCollected = null;
    }

    private SpriteRenderer spriteRenderer;
    private Collider2D pickupCollider;
    private TrailRenderer trailRenderer;
    private bool isCollected = false;
    private bool isSeeking = false;
    private bool countedForTracking = false;
    private float currentSpeed;
    private float spawnTime;
    private Transform playerTransform;
    private Vector3 initialPos;
    private Vector2 popVelocity;

    private void Awake()
    {
        ActiveStarsCount++;
        countedForTracking = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null) pickupCollider.isTrigger = true;

        spawnTime = Time.time;
        currentSpeed = startSpeed;
        initialPos = transform.position;

        // Dispersão inicial suave em arco (Pop)
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        popVelocity = randomDir * Random.Range(1.2f, 2.4f);

        EnsureVisuals();
    }

    private void Start()
    {
        FindPlayer();
        StartCoroutine(SeekSequenceRoutine());
    }

    private void FindPlayer()
    {
        if (playerTransform != null) return;

        GameObject playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            if (stats != null) playerTransform = stats.transform;
        }
    }

    private void EnsureVisuals()
    {
        if (trailRenderer == null)
        {
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
                trailRenderer.time = 0.4f;
                trailRenderer.startWidth = 0.3f;
                trailRenderer.endWidth = 0.02f;
                trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
                trailRenderer.startColor = new Color(1f, 0.9f, 0.3f, 0.85f);
                trailRenderer.endColor = new Color(1f, 0.6f, 0.1f, 0f);
                trailRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 4;
            }
        }
    }

    private IEnumerator SeekSequenceRoutine()
    {
        // Fase 1: Pop e amortecimento suave
        float popDuration = initialDelay;
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            if (isCollected) yield break;

            elapsed += Time.deltaTime;
            float factor = 1f - (elapsed / popDuration);
            transform.position += (Vector3)(popVelocity * factor * Time.deltaTime);

            if (enableSpin)
            {
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }

            yield return null;
        }

        // Fase 2: Ativa perseguição ao jogador
        isSeeking = true;
    }

    private void Update()
    {
        if (isCollected) return;

        if (playerTransform == null)
        {
            FindPlayer();
        }

        if (enableSpin)
        {
            transform.Rotate(0f, 0f, (isSeeking ? spinSpeed * 1.5f : spinSpeed) * Time.deltaTime);
        }

        if (isSeeking && seekPlayer && playerTransform != null)
        {
            // Movimento suave e acelerado em direção ao jogador
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.deltaTime, maxSpeed);
            Vector3 targetPos = playerTransform.position + new Vector3(0f, 0.5f, 0f); // Mira no peito da Naia

            // Interpolação elegante
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
        else if (!isSeeking && enableBobbing)
        {
            float bob = Mathf.Sin((Time.time - spawnTime) * bobbingSpeed) * bobbingHeight;
            transform.position = new Vector3(transform.position.x, initialPos.y + bob, transform.position.z);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        PlayerCurrency currency = other.GetComponent<PlayerCurrency>();
        if (currency == null && other.CompareTag("Player"))
        {
            currency = other.GetComponentInParent<PlayerCurrency>() ?? PlayerCurrency.Instance;
        }

        if (currency != null)
        {
            Collect(currency);
        }
    }

    private void Collect(PlayerCurrency currency)
    {
        if (isCollected) return;
        isCollected = true;

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        currency.AddStars(starValue);

        // Feedback Visual e Efeitos
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.8f, $"+{starValue} Estrela!", starGlowColor, 4.5f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, starGlowColor, 1.8f);
        }

        StartCoroutine(CollectAnimationRoutine());
    }

    private IEnumerator CollectAnimationRoutine()
    {
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * 1.6f;

        if (spriteRenderer != null)
        {
            Tween.Scale(transform, targetScale, fadeDuration, Ease.OutBack);
            Tween.Alpha(spriteRenderer, 0f, fadeDuration, Ease.InQuad);
        }

        yield return new WaitForSeconds(fadeDuration);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (countedForTracking)
        {
            countedForTracking = false;
            ActiveStarsCount = Mathf.Max(0, ActiveStarsCount - 1);
            if (ActiveStarsCount == 0)
            {
                OnAllStarsCollected?.Invoke();
            }
        }
    }

    /// <summary>
    /// Helper estático para instanciar ou forjar uma Estrela Forjada no mundo com segurança.
    /// </summary>
    public static GameObject SpawnStar(Vector3 position, GameObject prefab = null)
    {
        if (prefab != null)
        {
            return Instantiate(prefab, position, Quaternion.identity);
        }

        GameObject starObj = new GameObject("Star_Forged_Pickup");
        starObj.transform.position = position;

        SpriteRenderer sr = starObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateDefaultStarSprite();
        sr.color = new Color(1f, 0.9f, 0.2f, 1f);
        sr.sortingOrder = 8;
        starObj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        CircleCollider2D col = starObj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.55f;

        starObj.AddComponent<StarPickup>();
        return starObj;
    }

    private static Sprite CreateDefaultStarSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] cols = new Color[32 * 32];
        Vector2 center = new Vector2(16f, 16f);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float armLength = (Mathf.Abs(dx) * Mathf.Abs(dy)) / 16f;
                if (dist < 14f && armLength < 4.2f)
                {
                    cols[y * 32 + x] = new Color(1f, 0.92f, 0.35f, 1f);
                }
                else
                {
                    cols[y * 32 + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
    }
}
