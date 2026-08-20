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

    [Header("Configurações da Arena")]
    [SerializeField] private Vector2 arenaCenter = Vector2.zero;
    [SerializeField] private Vector2 arenaSize = new Vector2(14f, 10f);

    [Header("Componentes")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private TrailRenderer fireTrail;

    private Transform playerTransform;
    private bool isDead = false;
    private bool isExecutingAttack = false;
    private float attackCooldownTimer = 2f;
    private int attackCycleIndex = 0;

    private Animator animator;
    private static readonly int RoarTrigger = Animator.StringToHash("Roar");

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
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

    private void Start()
    {
        ScreenBounds bounds = GetScreenBounds();
        homePosition = bounds.center;
        transform.position = homePosition;
        transform.rotation = Quaternion.identity;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        // Inicia a barra de vida no topo da tela
        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.ShowBoss(bossName, currentHealth, maxHealth);
        }

        StartCoroutine(BossLoopRoutine());
    }

    private void Update()
    {
        if (isDead) return;

        // O Boitatá permanece 100% fixo, ereto e estável no centro da fase/tela
        transform.position = homePosition;
        transform.rotation = Quaternion.identity;
    }

    #region Loop de IA e Habilidades do Chefe
    private IEnumerator BossLoopRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Pausa inicial de entrada épica

        while (!isDead)
        {
            if (!isExecutingAttack)
            {
                attackCooldownTimer -= Time.deltaTime;
                if (attackCooldownTimer <= 0f)
                {
                    // Executa os 4 ataques em ordem sequencial para teste:
                    // 0: Investidas Modulares (Dash com curvas)
                    // 1: Chuva de Meteoros com Sombras
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

    private struct ScreenBounds
    {
        public float minX;
        public float maxX;
        public float minY;
        public float maxY;
        public Vector3 center;
    }

    private ScreenBounds GetScreenBounds(float padding = 0.08f)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Vector3 c = Vector3.zero;
            return new ScreenBounds
            {
                minX = c.x - 7.5f,
                maxX = c.x + 7.5f,
                minY = c.y - 4.5f,
                maxY = c.y + 4.5f,
                center = c
            };
        }

        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(padding, padding, -cam.transform.position.z));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f - padding, 1f - padding, -cam.transform.position.z));

        return new ScreenBounds
        {
            minX = Mathf.Min(bl.x, tr.x),
            maxX = Mathf.Max(bl.x, tr.x),
            minY = Mathf.Min(bl.y, tr.y),
            maxY = Mathf.Max(bl.y, tr.y),
            center = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f)
        };
    }

    #region Ataque 1: Investidas de Fogo Cruzando a Tela
    /// <summary>
    /// Spawna linhas de aviso em direções aleatórias cruzando a tela com curvas.
    /// O número de investidas escala com a vida perdida (2 a 100% de vida até 5 a <= 20% de vida).
    /// </summary>
    private IEnumerator PerformHashtagGridAttack()
    {
        isExecutingAttack = true;

        ScreenBounds bounds = GetScreenBounds(0.06f);

        // Calcula a quantidade de investidas com base na vida
        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
        float healthRange = Mathf.Max(0.01f, 1f - lowHealthThreshold);
        float t = Mathf.Clamp01((1f - healthPercent) / healthRange); // 0 em 100% até 1 em lowHealthThreshold
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

    private Vector3[] GenerateRandomDashPath(ScreenBounds bounds)
    {
        int pattern = UnityEngine.Random.Range(0, 4);
        float cornerX = UnityEngine.Random.Range(bounds.minX + 2.5f, bounds.maxX - 2.5f);
        float cornerY = UnityEngine.Random.Range(bounds.minY + 2f, bounds.maxY - 2f);
        Vector3 corner = new Vector3(cornerX, cornerY, 0f);

        switch (pattern)
        {
            case 0: // Esquerda -> Corner -> (Baixo ou Cima)
                bool exitDown = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(bounds.minX, cornerY, 0f),
                    corner,
                    new Vector3(cornerX, exitDown ? bounds.minY : bounds.maxY, 0f)
                };
            case 1: // Topo -> Corner -> (Direita ou Esquerda)
                bool exitRight = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(cornerX, bounds.maxY, 0f),
                    corner,
                    new Vector3(exitRight ? bounds.maxX : bounds.minX, cornerY, 0f)
                };
            case 2: // Direita -> Corner -> (Cima ou Baixo)
                bool exitUp = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(bounds.maxX, cornerY, 0f),
                    corner,
                    new Vector3(cornerX, exitUp ? bounds.maxY : bounds.minY, 0f)
                };
            default: // Baixo -> Corner -> (Esquerda ou Direita)
                bool exitLeft = UnityEngine.Random.value > 0.5f;
                return new Vector3[] {
                    new Vector3(cornerX, bounds.minY, 0f),
                    corner,
                    new Vector3(exitLeft ? bounds.minX : bounds.maxX, cornerY, 0f)
                };
        }
    }
    #endregion

    #region Ataque 2: Chuva de Bolas de Fogo na Tela com Sombras
    /// <summary>
    /// O Boitatá cospe fogo para o céu e sombras circulares aparecem na tela anunciando a queda dos meteoros.
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

        ScreenBounds bounds = GetScreenBounds(0.08f);

        // 2. Determina 5 a 6 posições de impacto distribuídas estritamente dentro da tela
        List<Vector3> targetPositions = new List<Vector3>();

        if (playerTransform != null)
        {
            // Mira onde o jogador está, limitado à tela
            float px = Mathf.Clamp(playerTransform.position.x, bounds.minX + 0.5f, bounds.maxX - 0.5f);
            float py = Mathf.Clamp(playerTransform.position.y, bounds.minY + 0.5f, bounds.maxY - 0.5f);
            targetPositions.Add(new Vector3(px, py, 0f));

            // Posição próxima à previsão de movimento
            Vector3 offset = (Vector3)UnityEngine.Random.insideUnitCircle * 2.2f;
            targetPositions.Add(new Vector3(
                Mathf.Clamp(px + offset.x, bounds.minX + 0.5f, bounds.maxX - 0.5f),
                Mathf.Clamp(py + offset.y, bounds.minY + 0.5f, bounds.maxY - 0.5f),
                0f
            ));
        }

        // Preenche com mais 4 posições aleatórias espalhadas pela tela visível
        for (int i = 0; i < 4; i++)
        {
            Vector3 randomPos = new Vector3(
                UnityEngine.Random.Range(bounds.minX + 0.5f, bounds.maxX - 0.5f),
                UnityEngine.Random.Range(bounds.minY + 0.5f, bounds.maxY - 0.5f),
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

    #region Posicionamento Dinâmico de Partes do Corpo por Sprite Ativo
    /// <summary>
    /// Calcula a posição exata da chama na ponta do rabo do Boitatá com base no sprite atualmente renderizado pelo Animator.
    /// </summary>
    public Vector3 GetCurrentTailFlamePosition()
    {
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

    /// <summary>
    /// Calcula e interpola suavemente a posição da chama do rabo do Boitatá para movimentos contínuos e orgânicos.
    /// </summary>
    public Vector3 GetSmoothTailFlamePosition()
    {
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

        // Telegrafia inicial: linhas em cruz rápida saindo exatamente do fogo da cauda
        float beamLength = 11f;
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
        StopAllCoroutines();

        Debug.Log("[Boitatá] O Chefe foi Derrotado! Concedendo estrelas forjadas ao jogador...");

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.HideBoss(true);
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

        Destroy(gameObject, 0.4f);
    }

    private void SpawnStarDrop(Vector3 position)
    {
        if (starPickupPrefab != null)
        {
            Instantiate(starPickupPrefab, position, Quaternion.identity);
        }
        else
        {
            // Criação procedural da Estrela Forjada caso prefab não esteja linkado
            GameObject starObj = new GameObject("Star_Forged_Pickup");
            starObj.transform.position = position;

            SpriteRenderer sr = starObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateStarSprite();
            sr.color = new Color(1f, 0.9f, 0.2f, 1f);
            sr.sortingOrder = 5;
            starObj.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            CircleCollider2D col = starObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.6f;

            starObj.AddComponent<StarPickup>();
        }
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
}
