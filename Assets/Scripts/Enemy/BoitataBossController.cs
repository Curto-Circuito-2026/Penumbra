using System;
using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Controlador Principal do Boss 1: Boitatá (A Serpente de Fogo).
/// Mecânicas:
/// - Barra de Vida no Topo da Tela (Top-Center HUD).
/// - Ataque 1: Investida em Grade Hashtag (#) com linhas de telegrafia e rastro de chamas.
/// - Ataque 2: Chuva de Bolas de Fogo com sombras telegrafadas no chão.
/// - Ataque 3: Cusparada de Fogo Direta.
/// - Morte: Drop de 3 a 5 Estrelas Forjadas completas (StarPickup).
/// </summary>
public class BoitataBossController : MonoBehaviour, IDamageable
{
    [Header("Identificação do Chefe")]
    [SerializeField] private string bossName = "BOITATÁ - Serpente de Fogo";

    [Header("Atributos de Vida")]
    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private float currentHealth = 500f;

    [Header("Ataques & Dano")]
    [SerializeField] private float contactDamage = 15f;
    [SerializeField] private float dashDamage = 25f;
    [SerializeField] private float meteorDamage = 30f;
    [SerializeField] private float fireRingDamage = 20f;
    [SerializeField] private float spinningBeamDamage = 25f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Configurações da Investida (Dash)")]
    [Tooltip("Velocidade com que a serpente cruza a tela.")]
    [SerializeField] private float dashSpeed = 15.5f;
    [Tooltip("Tempo de exibição do aviso de perigo antes da investida.")]
    [SerializeField] private float dashTelegraphDuration = 0.65f;
    [Tooltip("Intervalo entre cada investida consecutiva.")]
    [SerializeField] private float dashInterval = 0.95f;
    [Tooltip("Quantidade mínima de investidas (em 100% de vida).")]
    [SerializeField] private int minDashCount = 2;
    [Tooltip("Quantidade máxima de investidas (ao atingir vida baixa).")]
    [SerializeField] private int maxDashCount = 5;
    [Tooltip("Porcentagem de vida onde atinge a quantidade máxima de investidas (0.20 = 20%).")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float lowHealthThreshold = 0.20f;
    [Tooltip("Cor do aviso telegrafado (sombra).")]
    [SerializeField] private Color dashTelegraphColor = new Color(0.08f, 0.06f, 0.12f, 0.65f);

    [Header("Recompensas de Morte")]
    [Tooltip("Quantidade de estrelas inteiras forjadas a dropar ao morrer.")]
    [SerializeField] private int minStarDrop = 3;
    [SerializeField] private int maxStarDrop = 5;
    [SerializeField] private GameObject starPickupPrefab;

    [Header("Configurações da Arena & FightZone")]
    [Tooltip("Collider2D da área de combate (Fightzone). Se for nulo, busca automaticamente no pai Boss_room ou na cena.")]
    [SerializeField] private Collider2D fightZoneCollider;
    [Tooltip("Se verdadeiro, inicia o combate automaticamente no Start (útil para testes isolados sem trigger).")]
    [SerializeField] private bool autoStartCombat = false;
    [SerializeField] private Vector2 arenaSize = new Vector2(14f, 10f);

    [Header("Pontos de Origem de Habilidades")]
    [Tooltip("Transform do objeto 'Circle' (ou cauda animada) que serve de centro do Catavento Giratório de Fogo.")]
    [SerializeField] private Transform spinningCenterTransform;

    [Header("Componentes")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private TrailRenderer fireTrail;

    private Transform playerTransform;
    private bool isDead = false;
    private bool isExecutingAttack = false;
    private bool isCombatActive = false;
    private float attackCooldownTimer = 2f;
    private int attackCycleIndex = 0;

    private Animator animator;
    private static readonly int RoarTrigger = Animator.StringToHash("Roar");
    private static readonly int DeathTrigger = Animator.StringToHash("Death");

    [Header("Eventos de Morte")]
    [Tooltip("Evento Unity chamado no Inspector ao derrotar o Boss (ex: abrir portas, tocar cutscene, liberar passagem).")]
    [SerializeField] private UnityEngine.Events.UnityEvent onBossDeath;

    [Tooltip("GameEvent ScriptableObject opcional disparado na morte do Boss.")]
    [SerializeField] private GameEvent onBossDefeatedEvent;

    [SerializeField] BossTrigger bossIntro;

    public event Action OnBossDied;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsCombatActive => isCombatActive;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (spinningCenterTransform == null) spinningCenterTransform = transform.Find("Circle");
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (fireTrail == null) fireTrail = GetComponentInChildren<TrailRenderer>();

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
        {
            gameObject.layer = enemyLayer;
            foreach (Transform child in transform)
            {
                child.gameObject.layer = enemyLayer;
            }
        }
        gameObject.tag = "Enemy";

        // Garante que o Boss possua colisor para receber ataques Melee e Ranged
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 1.4f;
        col.isTrigger = false;

        if (playerLayerMask.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            playerLayerMask = playerLayer != -1 ? (1 << playerLayer) : ~0;
        }
    }

    private Vector3 homePosition;

    private void OnEnable()
    {
        PlayerStats.OnAnyPlayerDied += HandlePlayerDied;
        PlayerStats.OnAnyPlayerRespawned += HandlePlayerRespawned;
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }
    }

    private void OnDisable()
    {
        PlayerStats.OnAnyPlayerDied -= HandlePlayerDied;
        PlayerStats.OnAnyPlayerRespawned -= HandlePlayerRespawned;
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState previousState, GameState newState)
    {
        if (newState == GameState.Dead || newState == GameState.Menu)
        {
            HandlePlayerDied();
        }
    }

    private void HandlePlayerDied()
    {
        if (isDead) return;

        // Se o player morreu, interrompe os ataques do Boss, reseta seu estado e oculta a barra
        StopAllCoroutines();
        isExecutingAttack = false;
        isCombatActive = false;
        currentHealth = maxHealth;
        attackCycleIndex = 0;
        attackCooldownTimer = 2f;

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.HideImmediate();
        }
    }

    private void HandlePlayerRespawned()
    {
        if (isDead) return;

        transform.position = homePosition;
        currentHealth = maxHealth;
        isCombatActive = false;
        isExecutingAttack = false;
    }

    private void Start()
    {
        homePosition = transform.position;
        transform.rotation = Quaternion.identity;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        EnsureFightZoneReference();

        if (autoStartCombat)
        {
            StartCombat();
        }
    }

    /// <summary>
    /// Inicia o combate do Chefe (disparado pelo BossFightTrigger ao entrar na sala do Boss).
    /// </summary>
    public void StartCombat()
    {
        CinematicManager cinematicManager = GameObject.Find("CinematicManager") != null ? GameObject.Find("CinematicManager").GetComponent<CinematicManager>() : (CinematicManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<CinematicManager>());

        if (cinematicManager != null && bossIntro != null) {
            bossIntro.Boss = this.gameObject;
            cinematicManager.PlayClip(bossIntro.gameObject);
        }

        if (isCombatActive || isDead) return;
        isCombatActive = true;

        // Inicia a barra de vida no topo da tela
        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.ShowBoss(bossName, currentHealth, maxHealth);
        }

        if (animator != null) animator.SetTrigger(RoarTrigger);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.TriggerCameraShake(0.4f, 0.25f);
        }

        StartCoroutine(BossLoopRoutine());
        Debug.Log($"[Boitatá] Combate iniciado via Trigger! Barra de vida ativada.");
    }

    private void Update()
    {
        if (isDead) return;

        // O Boitatá permanece fixo na sua posição de base na sala
        transform.position = homePosition;
        transform.rotation = Quaternion.identity;
    }

    #region Loop de IA e Habilidades do Chefe
    private IEnumerator BossLoopRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Pausa inicial de entrada épica

        while (!isDead && isCombatActive)
        {
            if (!isExecutingAttack)
            {
                attackCooldownTimer -= Time.deltaTime;
                if (attackCooldownTimer <= 0f)
                {
                    // Executa os 4 ataques em ordem sequencial:
                    // 0: Investidas Modulares no FightZone (Dash com curvas)
                    // 1: Chuva de Meteoros dentro do FightZone
                    // 2: Anel 360 de Bolas de Fogo pela Boca
                    // 3: Catavento de Chamas Giratório pelo Rabo
                    int attackType = attackCycleIndex % 4;

                    switch (attackType)
                    {
                        case 0:
                            yield return StartCoroutine(PerformHashtagGridAttack());
                            break;
                        case 1:
                            yield return StartCoroutine(PerformMeteorRainAttack());
                            break;
                        case 2:
                            yield return StartCoroutine(PerformSuper360FireRingAttack());
                            break;
                        case 3:
                            yield return StartCoroutine(PerformSpinningFireBeamsAttack());
                            break;
                    }

                    attackCooldownTimer = 2.0f;
                    attackCycleIndex++;
                }
            }
            yield return null;
        }
    }
    #endregion

    public Bounds GetFightZoneBounds()
    {
        EnsureFightZoneReference();
        if (fightZoneCollider != null)
        {
            return fightZoneCollider.bounds;
        }

        return new Bounds(homePosition, new Vector3(arenaSize.x, arenaSize.y, 1f));
    }

    private void EnsureFightZoneReference()
    {
        if (fightZoneCollider != null) return;

        if (transform.parent != null)
        {
            Transform fz = transform.parent.Find("Fightzone") ?? transform.parent.Find("Fighzone") ?? transform.parent.Find("FightZone");
            if (fz != null && fz.TryGetComponent(out Collider2D col))
            {
                fightZoneCollider = col;
                return;
            }
        }

        GameObject found = GameObject.Find("Fightzone") ?? GameObject.Find("Fighzone") ?? GameObject.Find("FightZone");
        if (found != null && found.TryGetComponent(out Collider2D col2))
        {
            fightZoneCollider = col2;
        }
    }

    #region Ataque 1: Investidas de Fogo Cruzando o FightZone
    /// <summary>
    /// Spawna linhas de aviso em direções aleatórias cruzando o Fightzone com curvas.
    /// O número de investidas escala com a vida perdida (2 a 100% de vida até 5 a <= 20% de vida).
    /// </summary>
    private IEnumerator PerformHashtagGridAttack()
    {
        isExecutingAttack = true;

        Bounds bounds = GetFightZoneBounds();

        // Calcula a quantidade de investidas com base na vida
        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
        float healthRange = Mathf.Max(0.01f, 1f - lowHealthThreshold);
        float t = Mathf.Clamp01((1f - healthPercent) / healthRange);
        int baseCount = Mathf.RoundToInt(Mathf.Lerp(minDashCount, maxDashCount, t));
        int dashCount = Mathf.Clamp(baseCount + UnityEngine.Random.Range(0, 2) - UnityEngine.Random.Range(0, 1), minDashCount, maxDashCount);

        // Dispara cada investida com seu próprio aviso telegrafado de sombra
        for (int i = 0; i < dashCount; i++)
        {
            if (isDead) yield break;

            Vector3[] path = GenerateRandomDashPath(bounds);

            // 1. Spawna a telegrafia de sombra para o caminho atual
            for (int k = 0; k < path.Length - 1; k++)
            {
                BossTelegraphVisuals.Instance.CreateDangerLine(path[k], path[k + 1], 1.3f, dashTelegraphDuration, dashTelegraphColor);
            }

            yield return new WaitForSeconds(dashTelegraphDuration);

            if (isDead) yield break;

            // 2. Dispara a serpente rasgando o caminho na velocidade configurada
            BossTelegraphVisuals.Instance.SpawnFireSerpentDash(path, dashSpeed, dashDamage, playerLayerMask);

            yield return new WaitForSeconds(dashInterval);
        }

        isExecutingAttack = false;
    }

    private Vector3[] GenerateRandomDashPath(Bounds bounds)
    {
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minY = bounds.min.y;
        float maxY = bounds.max.y;

        int pattern = UnityEngine.Random.Range(0, 4);
        float cornerX = UnityEngine.Random.Range(minX + 2.0f, maxX - 2.0f);
        float cornerY = UnityEngine.Random.Range(minY + 1.5f, maxY - 1.5f);
        Vector3 corner = new Vector3(cornerX, cornerY, 0f);

        switch (pattern)
        {
            case 0: // Esquerda -> Corner -> (Baixo ou Cima)
                bool exitDown = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(minX, cornerY, 0f),
                    corner,
                    new Vector3(cornerX, exitDown ? minY : maxY, 0f)
                };
            case 1: // Topo -> Corner -> (Direita ou Esquerda)
                bool exitRight = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(cornerX, maxY, 0f),
                    corner,
                    new Vector3(exitRight ? maxX : minX, cornerY, 0f)
                };
            case 2: // Direita -> Corner -> (Cima ou Baixo)
                bool exitUp = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(maxX, cornerY, 0f),
                    corner,
                    new Vector3(cornerX, exitUp ? maxY : minY, 0f)
                };
            default: // Baixo -> Corner -> (Esquerda ou Direita)
                bool exitLeft = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(cornerX, minY, 0f),
                    corner,
                    new Vector3(exitLeft ? minX : maxX, cornerY, 0f)
                };
        }
    }
    #endregion

    #region Ataque 2: Chuva de Bolas de Fogo no Fightzone com Sombras
    /// <summary>
    /// O Boitatá cospe fogo para o céu e sombras circulares aparecem dentro do Fightzone anunciando a queda dos meteoros.
    /// </summary>
    private IEnumerator PerformMeteorRainAttack()
    {
        isExecutingAttack = true;

        if (animator != null) animator.SetTrigger(RoarTrigger);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 1.05f, new Color(1f, 0.5f, 0.1f), 2f);
            CombatVisualEffects.Instance.TriggerCameraShake(0.2f, 0.2f);
        }

        yield return new WaitForSeconds(0.5f);

        Bounds bounds = GetFightZoneBounds();
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minY = bounds.min.y;
        float maxY = bounds.max.y;

        // Determina 5 a 6 posições de impacto distribuídas estritamente dentro do Fightzone
        List<Vector3> targetPositions = new List<Vector3>();

        if (playerTransform != null)
        {
            // Mira onde o jogador está, limitado ao Fightzone
            float px = Mathf.Clamp(playerTransform.position.x, minX + 0.5f, maxX - 0.5f);
            float py = Mathf.Clamp(playerTransform.position.y, minY + 0.5f, maxY - 0.5f);
            targetPositions.Add(new Vector3(px, py, 0f));

            // Posição próxima à previsão de movimento
            Vector3 offset = (Vector3)UnityEngine.Random.insideUnitCircle * 2.2f;
            targetPositions.Add(new Vector3(
                Mathf.Clamp(px + offset.x, minX + 0.5f, maxX - 0.5f),
                Mathf.Clamp(py + offset.y, minY + 0.5f, maxY - 0.5f),
                0f
            ));
        }

        // Preenche com mais 4 posições aleatórias espalhadas pela área do Fightzone
        for (int i = 0; i < 4; i++)
        {
            Vector3 randomPos = new Vector3(
                UnityEngine.Random.Range(minX + 0.5f, maxX - 0.5f),
                UnityEngine.Random.Range(minY + 0.5f, maxY - 0.5f),
                0f
            );
            targetPositions.Add(randomPos);
        }

        // 3. Spawna cada meteoro com telegrafia de sombra no chão com pequeno delay em cascata
        foreach (var pos in targetPositions)
        {
            if (isDead) yield break;
            BossTelegraphVisuals.Instance.SpawnMeteorWithShadow(pos, 1.25f, 1.35f, meteorDamage, playerLayerMask);
            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(1.4f);

        isExecutingAttack = false;
    }
    #endregion

    #region Posicionamento Dinâmico de Partes do Corpo por Sprite Ativo ou Objeto Circle
    /// <summary>
    /// Calcula a posição exata da chama na ponta do rabo do Boitatá com base no objeto 'Circle' animado ou no sprite ativo.
    /// </summary>
    public Vector3 GetCurrentTailFlamePosition()
    {
        if (spinningCenterTransform == null)
        {
            spinningCenterTransform = transform.Find("Circle");
        }

        if (spinningCenterTransform != null)
        {
            return spinningCenterTransform.position;
        }

        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return transform.position + new Vector3(2.8f, 1.8f, 0f);
        }

        string sName = spriteRenderer.sprite.name;
        Vector3 localOffset;

        switch (sName)
        {
            case "boitata_idle_1":
                // Cauda erguida alta no ar com chama apontando para cima
                localOffset = new Vector3(2.80f, 2.41f, 0f);
                break;
            case "boitata_idle_2":
                // Cauda esticada horizontalmente para a direita
                localOffset = new Vector3(3.43f, 1.06f, 0f);
                break;
            case "boitata_idle_3":
                // Cauda ondulando perto do chão
                localOffset = new Vector3(2.85f, 0.62f, 0f);
                break;
            case "boitata_breath_prep":
            case "boitata_breath_fire":
                localOffset = new Vector3(2.80f, 2.41f, 0f);
                break;
            default:
                localOffset = new Vector3(2.9f, 1.5f, 0f);
                break;
        }

        if (spriteRenderer.flipX)
        {
            localOffset.x = -localOffset.x;
        }

        return transform.position + localOffset;
    }

    /// <summary>
    /// Calcula a posição exata da boca aberta do Boitatá com base no sprite atualmente renderizado.
    /// </summary>
    public Vector3 GetCurrentMouthPosition()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return transform.position + Vector3.up * 1.5f;
        }

        string sName = spriteRenderer.sprite.name;
        Vector3 localOffset;

        switch (sName)
        {
            case "boitata_breath_fire":
            case "boitata_breath_prep":
                // Boca aberta virada para cima
                localOffset = new Vector3(-0.58f, 2.05f, 0f);
                break;
            default:
                // Boca frontal
                localOffset = new Vector3(-0.10f, 2.15f, 0f);
                break;
        }

        if (spriteRenderer.flipX)
        {
            localOffset.x = -localOffset.x;
        }

        return transform.position + localOffset;
    }
    #endregion

    #region Ataque 3: Super 360 de Bolas de Fogo (Saindo da Boca ao Abrir)
    /// <summary>
    /// Dispara 2 ondas de bolas de fogo em 360 graus que viajam até o final da tela, saindo diretamente da boca no momento em que ela abre.
    /// O jogador pode usar o Dash por baixo delas sem tomar dano!
    /// </summary>
    private IEnumerator PerformSuper360FireRingAttack()
    {
        isExecutingAttack = true;

        // 1. Inicia o Roar e aguarda exatamente 0.25s para o momento em que a boca se abre
        if (animator != null) animator.SetTrigger(RoarTrigger);
        yield return new WaitForSeconds(0.25f);

        Vector3 mouthPos = GetCurrentMouthPosition();

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(mouthPos, new Color(1f, 0.6f, 0.1f), 1.8f);
            CombatVisualEffects.Instance.TriggerCameraShake(0.2f, 0.18f);
        }

        // 1ª Onda: 16 bolas de fogo expandindo em anel a partir da boca
        BossTelegraphVisuals.Instance.Spawn360FireballRing(mouthPos, 16, 6.5f, fireRingDamage, playerLayerMask);

        yield return new WaitForSeconds(0.35f);

        // 2ª Onda intercalada: 16 bolas de fogo mais velozes saindo da boca
        mouthPos = GetCurrentMouthPosition();
        BossTelegraphVisuals.Instance.Spawn360FireballRing(mouthPos, 16, 7.5f, fireRingDamage, playerLayerMask);

        yield return new WaitForSeconds(1.8f);

        isExecutingAttack = false;
    }
    #endregion

    private Vector3 smoothedTailFlamePos;

    public Vector3 GetSmoothTailFlamePosition()
    {
        if (spinningCenterTransform == null)
        {
            spinningCenterTransform = transform.Find("Circle");
        }

        if (spinningCenterTransform != null)
        {
            return spinningCenterTransform.position;
        }

        Vector3 target = GetCurrentTailFlamePosition();
        if (smoothedTailFlamePos == Vector3.zero)
        {
            smoothedTailFlamePos = target;
        }
        smoothedTailFlamePos = Vector3.MoveTowards(smoothedTailFlamePos, target, 6.0f * Time.deltaTime);
        return smoothedTailFlamePos;
    }

    #region Ataque 4: Catavento de Chamas Giratório 360° (Saindo do Fogo do Rabo)
    /// <summary>
    /// Projeta 4 feixes de fogo contínuos em formato de '+' a partir da chama da cauda e gira 360 graus na arena, acompanhando o rabo ativo suavemente.
    /// </summary>
    private IEnumerator PerformSpinningFireBeamsAttack()
    {
        isExecutingAttack = true;

        smoothedTailFlamePos = GetCurrentTailFlamePosition();
        Vector3 initialTailPos = smoothedTailFlamePos;

        Bounds bounds = GetFightZoneBounds();
        float beamLength = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.5f;
        if (beamLength < 8f) beamLength = 11f;

        // Telegrafia inicial: linhas em cruz rápida saindo exatamente do fogo da cauda
        Color dangerColor = new Color(1f, 0.3f, 0.1f, 0.45f);
        BossTelegraphVisuals.Instance.CreateDangerLine(initialTailPos, initialTailPos + Vector3.right * beamLength, 0.8f, 0.8f, dangerColor);
        BossTelegraphVisuals.Instance.CreateDangerLine(initialTailPos, initialTailPos + Vector3.left * beamLength, 0.8f, 0.8f, dangerColor);
        BossTelegraphVisuals.Instance.CreateDangerLine(initialTailPos, initialTailPos + Vector3.up * beamLength, 0.8f, 0.8f, dangerColor);
        BossTelegraphVisuals.Instance.CreateDangerLine(initialTailPos, initialTailPos + Vector3.down * beamLength, 0.8f, 0.8f, dangerColor);

        yield return new WaitForSeconds(0.8f);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.TriggerCameraShake(0.35f, 0.2f);
        }

        // Gira 360 graus completos durante 4.2 segundos saindo da cauda e acompanhando o rabo suavemente com MoveTowards
        yield return StartCoroutine(BossTelegraphVisuals.Instance.AnimateSpinningFireBeamsRoutine(
            transform,
            4,                  // 4 feixes em cruz (+)
            beamLength,         // Alcance cobrindo a tela inteira
            4.2f,               // Duração da rotação
            1f,                 // 360 graus (1 volta completa)
            spinningBeamDamage, // Dano por tick
            playerLayerMask,
            () => GetSmoothTailFlamePosition() // Desliza suavemente acompanhando a respiração/ondulação do rabo
        ));

        yield return new WaitForSeconds(0.4f);

        isExecutingAttack = false;
    }
    #endregion

    #region Dano, Vida e Derrota
    private Coroutine flashCoroutine;

    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth < 0f) currentHealth = 0f;

        Debug.Log($"[Boitatá] Recebeu {amount:F0} de dano! Vida restante: {currentHealth:F0}/{maxHealth:F0}");

        // Atualiza a Barra de Vida no Topo da Tela
        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.UpdateHealth(currentHealth, maxHealth);
        }

        // Feedback Visual de Dano Flutuante e Partículas
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 1.5f, $"-{amount:F0}", new Color(1f, 0.2f, 0.2f), 4.2f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 0.8f, new Color(1f, 0.3f, 0.2f), 1f);
        }

        // Flash de dano vermelho apenas ao ser atingido
        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(DamageFlashRoutine());
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        spriteRenderer.color = new Color(1f, 0.25f, 0.25f, 1f);
        yield return new WaitForSeconds(0.12f);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead || isExecutingAttack) return;
        if (other.gameObject == gameObject || other.transform.IsChildOf(transform)) return;

        if (((1 << other.gameObject.layer) & playerLayerMask.value) != 0 || other.CompareTag("Player"))
        {
            if (other.TryGetComponent(out IDamageable dmg) && !(dmg is BoitataBossController))
            {
                Vector3 pushDir = (other.transform.position - transform.position).normalized;
                dmg.TakeDamage(contactDamage * Time.deltaTime, pushDir);
            }
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        isCombatActive = false;
        StopAllCoroutines();
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        Debug.Log("[Boitatá] O Chefe foi Derrotado! Concedendo estrelas forjadas ao jogador...");

        // Dispara eventos para sistemas externos (cutscenes, portas, diálogos)
        OnBossDied?.Invoke();
        onBossDeath?.Invoke();
        if (onBossDefeatedEvent != null)
        {
            onBossDefeatedEvent.Raise();
        }

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.HideBoss(true);
        }

        // Toca animação de morte
        if (animator != null)
        {
            animator.SetTrigger(DeathTrigger);
        }

        // Explosão de Morte e Tremedeira de Câmera
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayExplosionVFX(transform.position, new Color(1f, 0.5f, 0.1f), new Color(1f, 0.9f, 0.3f), 4.5f);
            CombatVisualEffects.Instance.TriggerCameraShake(0.5f, 0.35f);
        }

        // Drop de 3 a 5 Estrelas Forjadas completas
        int starsToDrop = UnityEngine.Random.Range(minStarDrop, maxStarDrop + 1);
        for (int i = 0; i < starsToDrop; i++)
        {
            Vector3 dropPos = transform.position + (Vector3)UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(1.2f, 2.5f);
            SpawnStarDrop(dropPos);
        }

        // Mãe do Ouro surge onde o boss foi derrotado
        MaeDoOuroBossRewardNPC.SpawnAfterBoss(transform.position, BossDefeatedType.Boitata);

        yield return new WaitForSeconds(2.0f);
        Destroy(gameObject);
    }

    private void SpawnStarDrop(Vector3 position)
    {
        StarPickup.SpawnStar(position, starPickupPrefab);
    }

    private Sprite CreateStarSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] cols = new Color[32 * 32];
        Vector2 center = new Vector2(15.5f, 15.5f);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= 14f)
                {
                    float a = Mathf.Clamp01(1f - (dist / 14f));
                    cols[y * 32 + x] = new Color(1f, 0.9f, 0.2f, a);
                }
                else
                {
                    cols[y * 32 + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }
    #endregion

    #region Gizmos no Editor
    private void OnDrawGizmosSelected()
    {
        Bounds b = GetFightZoneBounds();
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
    #endregion
}
