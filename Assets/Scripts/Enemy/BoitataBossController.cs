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

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
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

        // O Boitatá permanece 100% fixo e imóvel no centro da fase/tela
        transform.position = homePosition;

        // Gira suavemente a cabeça na direção da Naia
        if (playerTransform != null)
        {
            Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
            float angle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(transform.eulerAngles.z, angle, Time.deltaTime * 6f));
        }
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
                    // Alterna entre os 4 ataques:
                    // 0 = Investidas Aleatórias Cruzando a Tela (Avatares de fogo)
                    // 1 = Chuva de Meteoros na Tela
                    // 2 = Super 360 de Bolas de Fogo (com esquiva de Dash)
                    // 3 = Catavento Giratório 360° de Chamas (Grade giratória)
                    switch (attackCycleIndex % 4)
                    {
                        case 0:
                            yield return StartCoroutine(PerformHashtagGridAttack());
                            attackCooldownTimer = UnityEngine.Random.Range(3f, 4f);
                            break;
                        case 1:
                            yield return StartCoroutine(PerformMeteorRainAttack());
                            attackCooldownTimer = UnityEngine.Random.Range(3f, 4f);
                            break;
                        case 2:
                            yield return StartCoroutine(PerformSuper360FireRingAttack());
                            attackCooldownTimer = UnityEngine.Random.Range(3f, 4f);
                            break;
                        case 3:
                            yield return StartCoroutine(PerformSpinningFireBeamsAttack());
                            attackCooldownTimer = UnityEngine.Random.Range(3.5f, 4.5f);
                            break;
                    }
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
    /// Spawna linhas de aviso em direções aleatórias cruzando a tela de ponta a ponta.
    /// O Boitatá permanece imóvel no centro e dispara avatares de fogo que rasgam a tela pelas linhas.
    /// </summary>
    private IEnumerator PerformHashtagGridAttack()
    {
        isExecutingAttack = true;

        if (spriteRenderer != null)
        {
            // Flash laranja anunciando o ataque pesado
            Tween.Color(spriteRenderer, new Color(1f, 0.6f, 0f, 1f), 0.3f);
        }

        ScreenBounds bounds = GetScreenBounds(0.06f);

        // Gera 4 a 5 linhas de corte cruzadas em direções aleatórias cobrindo a tela
        List<(Vector3 start, Vector3 end)> paths = new List<(Vector3 start, Vector3 end)>();

        // 1. Corte horizontal inclinado de um lado a outro
        float y1 = UnityEngine.Random.Range(bounds.minY + 0.5f, bounds.maxY - 0.5f);
        float y2 = UnityEngine.Random.Range(bounds.minY + 0.5f, bounds.maxY - 0.5f);
        paths.Add((new Vector3(bounds.minX, y1, 0f), new Vector3(bounds.maxX, y2, 0f)));

        // 2. Corte vertical inclinado de cima a baixo
        float x1 = UnityEngine.Random.Range(bounds.minX + 0.5f, bounds.maxX - 0.5f);
        float x2 = UnityEngine.Random.Range(bounds.minX + 0.5f, bounds.maxX - 0.5f);
        paths.Add((new Vector3(x1, bounds.maxY, 0f), new Vector3(x2, bounds.minY, 0f)));

        // 3. Corte diagonal aleatório de um canto a outro
        if (UnityEngine.Random.value > 0.5f)
        {
            paths.Add((new Vector3(bounds.minX, bounds.minY, 0f), new Vector3(bounds.maxX, bounds.maxY, 0f)));
        }
        else
        {
            paths.Add((new Vector3(bounds.minX, bounds.maxY, 0f), new Vector3(bounds.maxX, bounds.minY, 0f)));
        }

        // 4. Corte que passa através da posição atual da Naia de um lado da tela ao outro
        if (playerTransform != null)
        {
            Vector3 playerPos = playerTransform.position;
            bool fromHorizontal = UnityEngine.Random.value > 0.5f;
            if (fromHorizontal)
            {
                paths.Add((new Vector3(bounds.minX, playerPos.y, 0f), new Vector3(bounds.maxX, playerPos.y + UnityEngine.Random.Range(-1.5f, 1.5f), 0f)));
            }
            else
            {
                paths.Add((new Vector3(playerPos.x, bounds.maxY, 0f), new Vector3(playerPos.x + UnityEngine.Random.Range(-1.5f, 1.5f), bounds.minY, 0f)));
            }
        }

        // 1. Spawna as linhas telegrafadas de perigo na tela
        Color dangerColor = new Color(1f, 0.2f, 0.05f, 0.5f);
        float telegraphDuration = 1.3f;

        foreach (var path in paths)
        {
            BossTelegraphVisuals.Instance.CreateDangerLine(path.start, path.end, 1.3f, telegraphDuration, dangerColor);
        }

        yield return new WaitForSeconds(telegraphDuration);

        // 2. O Boitatá fica PARADO no centro e dispara avatares de fogo da serpente ao longo de cada faixa
        foreach (var path in paths)
        {
            if (isDead) yield break;

            Vector3 moveDir = (path.end - path.start).normalized;
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Spawna avatar de fogo que cruza a tela em alta velocidade e causa dano
            BossTelegraphVisuals.Instance.SpawnFireSerpentDash(path.start, path.end, 0.35f, dashDamage, playerLayerMask);

            yield return new WaitForSeconds(0.25f);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        isExecutingAttack = false;
    }
    #endregion

    #region Ataque 2: Chuva de Bolas de Fogo na Tela com Sombras
    /// <summary>
    /// O Boitatá cospe fogo para o céu e sombras circulares aparecem na tela anunciando a queda dos meteoros.
    /// </summary>
    private IEnumerator PerformMeteorRainAttack()
    {
        isExecutingAttack = true;

        // 1. Animação de erguer a cabeça e cuspir para o alto
        if (spriteRenderer != null)
        {
            Tween.Color(spriteRenderer, new Color(1f, 0.4f, 0.1f, 1f), 0.4f);
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 1f, new Color(1f, 0.5f, 0.1f), 2f);
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

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        isExecutingAttack = false;
    }
    #endregion

    #region Ataque 3: Super 360 de Bolas de Fogo (com esquiva de Dash)
    /// <summary>
    /// Dispara 2 ondas de bolas de fogo em 360 graus que viajam até o final da tela.
    /// O jogador pode usar o Dash por baixo delas sem tomar dano!
    /// </summary>
    private IEnumerator PerformSuper360FireRingAttack()
    {
        isExecutingAttack = true;

        if (spriteRenderer != null)
        {
            Tween.Color(spriteRenderer, new Color(1f, 0.35f, 0.05f, 1f), 0.35f);
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(1f, 0.6f, 0.1f), 1.8f);
            CombatVisualEffects.Instance.TriggerCameraShake(0.2f, 0.18f);
        }

        yield return new WaitForSeconds(0.4f);

        // 1ª Onda: 16 bolas de fogo expandindo em anel
        BossTelegraphVisuals.Instance.Spawn360FireballRing(transform.position, 16, 6.5f, fireRingDamage, playerLayerMask);

        yield return new WaitForSeconds(0.35f);

        // 2ª Onda intercalada: 16 bolas de fogo mais velozes
        BossTelegraphVisuals.Instance.Spawn360FireballRing(transform.position, 16, 7.5f, fireRingDamage, playerLayerMask);

        yield return new WaitForSeconds(1.8f);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        isExecutingAttack = false;
    }
    #endregion

    #region Ataque 4: Catavento de Chamas Giratório 360°
    /// <summary>
    /// Projeta 4 feixes de fogo contínuos em cruz (#) e gira 360 graus na arena, obrigando o jogador a correr ao redor.
    /// </summary>
    private IEnumerator PerformSpinningFireBeamsAttack()
    {
        isExecutingAttack = true;

        if (spriteRenderer != null)
        {
            Tween.Color(spriteRenderer, new Color(1f, 0.85f, 0.2f, 1f), 0.35f);
        }

        // Telegrafia inicial: linhas em cruz rápida
        float beamLength = 11f;
        Color dangerColor = new Color(1f, 0.3f, 0.1f, 0.45f);
        BossTelegraphVisuals.Instance.CreateDangerLine(transform.position, transform.position + Vector3.right * beamLength, 0.8f, 0.8f, dangerColor);
        BossTelegraphVisuals.Instance.CreateDangerLine(transform.position, transform.position + Vector3.left * beamLength, 0.8f, 0.8f, dangerColor);
        BossTelegraphVisuals.Instance.CreateDangerLine(transform.position, transform.position + Vector3.up * beamLength, 0.8f, 0.8f, dangerColor);
        BossTelegraphVisuals.Instance.CreateDangerLine(transform.position, transform.position + Vector3.down * beamLength, 0.8f, 0.8f, dangerColor);

        yield return new WaitForSeconds(0.8f);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.TriggerCameraShake(0.35f, 0.2f);
        }

        // Gira 360 graus completos durante 4.2 segundos
        yield return StartCoroutine(BossTelegraphVisuals.Instance.AnimateSpinningFireBeamsRoutine(
            transform,
            4,                  // 4 feixes (cruz / grade #)
            beamLength,         // Alcance cobrindo a tela inteira
            4.2f,               // Duração da rotação
            1f,                 // 360 graus (1 volta completa)
            spinningBeamDamage, // Dano por tick
            playerLayerMask
        ));

        yield return new WaitForSeconds(0.4f);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        isExecutingAttack = false;
    }
    #endregion

    #region Dano, Vida e Derrota
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

        // Feedback Visual
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 1f, $"-{amount:F0}", new Color(1f, 0.85f, 0.2f), 5f);
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, Color.white, 1.2f);
        }

        // Flash de dano
        if (spriteRenderer != null)
        {
            Tween.Color(spriteRenderer, new Color(1f, 0.3f, 0.3f, 1f), 0.1f).OnComplete(() =>
            {
                if (spriteRenderer != null && !isExecutingAttack) spriteRenderer.color = Color.white;
            });
        }

        if (currentHealth <= 0f)
        {
            Die();
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
