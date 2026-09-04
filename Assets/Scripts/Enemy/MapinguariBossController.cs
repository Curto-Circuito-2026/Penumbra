using System;
using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador Principal do Boss Mapinguari (Guardião da Floresta).
/// Mecânicas:
/// - Anda em direção ao Player usando NavMeshAgent e animações de corrida/passos pesados.
/// - Ataque 1: Soco / Porradona Melee frontal com alto impacto e camera shake.
/// - Ataque 2: Pulo Alto com Sombra Telegrafada no chão e Esmagamento em Área (Ground Slam AoE).
/// - Ataque 3: Arremesso de Pedra que gira 360° no ar até colidir com o Player.
/// - Barra de Vida de Chefe no Topo da Tela (BossHealthBarUI).
/// </summary>
public class MapinguariBossController : MonoBehaviour, IDamageable
{
    [Header("Identificação do Chefe")]
    [SerializeField] private string bossName = "MAPINGUARI";

    [Header("Atributos de Vida")]
    [SerializeField] private float maxHealth = 450f;
    [SerializeField] private float currentHealth = 450f;

    [Header("Danos e Camadas")]
    [SerializeField] private float punchDamage = 25f;
    [SerializeField] private float slamDamage = 35f;
    [SerializeField] private float rockDamage = 22f;
    [SerializeField] private float slamRadius = 3.2f;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Movimentação")]
    [SerializeField] private float moveSpeed = 2.6f;
    [SerializeField] private float detectionRadius = 16f;
    [SerializeField] private float meleeRange = 2.2f;

    [Header("Cooldowns & Timers")]
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private float slamWindupTime = 0.85f;

    [Header("Prefabs e VFX")]
    [SerializeField] private GameObject rockProjectilePrefab;
    [SerializeField] private GameObject starPickupPrefab;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Componentes")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private Rigidbody2D rb;

    [Header("Ativação de Combate")]
    [SerializeField] private bool autoStartCombat = false;
    [SerializeField] private BossTrigger bossIntro;

    [Header("SFX do Personagem")]
    [Tooltip("Nomeie cada clipe de acordo com a ação que ele representa")]
    [SerializeField] private AudioClip JumpingSFX;
    [SerializeField] private AudioClip LandingSFX;
    [SerializeField] private AudioClip ThrowingSFX;
    [SerializeField] private AudioClip PunchSFX;

    private Transform playerTransform;
    private bool isDead = false;
    private bool isExecutingAttack = false;
    private bool isCombatActive = false;
    private float attackTimer = 0f;
    private int attackCycleIndex = 0;
    private Material defaultMaterial;

    // Hashes do Animator
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int PunchHash = Animator.StringToHash("Punch");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int ThrowHash = Animator.StringToHash("Throw");
    private static readonly int DeathHash = Animator.StringToHash("Death");

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsCombatActive => isCombatActive;
    public bool IsDead => isDead;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer != null)
        {
            defaultMaterial = spriteRenderer.sharedMaterial;
        }

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = moveSpeed;
            agent.stoppingDistance = 2.6f;
        }

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        if (playerLayerMask == 0)
        {
            int pLayer = LayerMask.NameToLayer("Player");
            playerLayerMask = pLayer != -1 ? (1 << pLayer) : (1 << 0);
        }

        currentHealth = maxHealth;
    }

    private void Start()
    {
        LocatePlayer();
        IgnorePlayerPhysicsCollision();
        StopMovement();

        if (autoStartCombat)
        {
            StartCombat();
        }
    }

    public void StartCombat()
    {
        if (bossIntro == null)
        {
            if (transform.parent != null) bossIntro = transform.parent.GetComponentInChildren<BossTrigger>();
            if (bossIntro == null) bossIntro = UnityEngine.Object.FindAnyObjectByType<BossTrigger>();
        }

        CinematicManager cinematicManager = GameObject.Find("CinematicManager") != null ? GameObject.Find("CinematicManager").GetComponent<CinematicManager>() : (CinematicManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<CinematicManager>());

        if (cinematicManager != null && bossIntro != null)
        {
            bossIntro.Boss = this.gameObject;
            cinematicManager.PlayClip(bossIntro.gameObject);
        }

        StartBossFight();
    }

    private void LocatePlayer()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            IgnorePlayerPhysicsCollision();
        }
    }

    private void IgnorePlayerPhysicsCollision()
    {
        if (playerTransform == null) return;
        Collider2D[] myCols = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] playerCols = playerTransform.GetComponentsInChildren<Collider2D>(true);
        foreach (var m in myCols)
        {
            if (m == null) continue;
            foreach (var p in playerCols)
            {
                if (p != null) Physics2D.IgnoreCollision(m, p, true);
            }
        }
    }

    private int plannedNextAttack = 2; // Inicia com Arremesso de Pedra à distância
    private float meleeChaseTimer = 0f;
    [SerializeField] private float maxMeleeChaseTime = 2.4f; // Tempo máximo correndo atrás do player antes de desistir e trocar pra Ranged

    private void Update()
    {
        if (isDead) return;

        if (playerTransform == null)
        {
            LocatePlayer();
            if (playerTransform == null) return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // Ativação automática apenas se autoStartCombat for verdadeiro
        if (!isCombatActive && autoStartCombat && distance <= detectionRadius)
        {
            StartCombat();
        }

        bool isCutscene = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Playing;
        if (!isCombatActive || isExecutingAttack || isCutscene)
        {
            StopMovement();
            return;
        }

        // Orientação do Sprite (Olhar para o player)
        UpdateFacingDirection();

        attackTimer -= Time.deltaTime;

        // Comportamento de Perseguição e Ataque Ranged vs Melee
        if (attackTimer <= 0f)
        {
            // Ataque 1 ou 2 são RANGED (Pulo com Sombra e Arremesso de Pedra):
            // Dispara imediatamente de onde estiver sem precisar andar até o player!
            if (plannedNextAttack == 1)
            {
                meleeChaseTimer = 0f;
                ExecuteAttack(1);
            }
            else if (plannedNextAttack == 2)
            {
                meleeChaseTimer = 0f;
                ExecuteAttack(2);
            }
            else // Ataque 0 é MELEE (Soco / Porradona):
            {
                if (distance <= meleeRange)
                {
                    meleeChaseTimer = 0f;
                    ExecuteAttack(0);
                }
                else
                {
                    // Perseguindo com soco: incrementa timer de perseguição
                    meleeChaseTimer += Time.deltaTime;

                    // Se ficou muito tempo correndo atrás sem alcançar, troca imediatamente para Ranged!
                    if (meleeChaseTimer >= maxMeleeChaseTime)
                    {
                        meleeChaseTimer = 0f;
                        plannedNextAttack = (UnityEngine.Random.value > 0.5f) ? 1 : 2;
                        Debug.Log($"[MapinguariBoss] Player manteve distância durante Melee! Trocando para Ranged: {(plannedNextAttack == 1 ? "Pulo" : "Pedra")}");
                        ExecuteAttack(plannedNextAttack);
                    }
                    else
                    {
                        MoveTowardsPlayer();
                    }
                }
            }
        }
        else
        {
            meleeChaseTimer = 0f;
            // Em cooldown entre ataques:
            if (plannedNextAttack == 0 && distance > meleeRange)
            {
                // Se o próximo ataque for Melee, vai se aproximando suavemente
                MoveTowardsPlayer();
            }
            else if (distance > 10.0f)
            {
                // Se o player fugir muito longe, reaproxima
                MoveTowardsPlayer();
            }
            else
            {
                // Se o próximo for Ranged ou já estiver no range, para no lugar
                StopMovement();
            }
        }
    }

    public void StartBossFight()
    {
        if (isCombatActive || isDead) return;
        isCombatActive = true;
        attackTimer = 2.2f; // Delay de "acordar" após a cutscene
        plannedNextAttack = 2; // Primeiro ataque: Pedra Giratória

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.ShowBoss(bossName, currentHealth, maxHealth);
        }

        Debug.Log($"[MapinguariBoss] Combate com {bossName} iniciado! Primeiro ataque planejado em {attackTimer}s.");
    }

    private void MoveTowardsPlayer()
    {
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= 2.5f)
        {
            StopMovement();
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.stoppingDistance = 2.5f;
            agent.SetDestination(playerTransform.position);
            if (animator != null) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        }
        else
        {
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            transform.position += dir * (moveSpeed * Time.deltaTime);
            if (animator != null) animator.SetFloat(SpeedHash, moveSpeed);
        }
    }

    private void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (animator != null) animator.SetFloat(SpeedHash, 0f);
    }

    private void UpdateFacingDirection()
    {
        if (playerTransform == null || spriteRenderer == null) return;
        // Se o sprite original olha para a esquerda: flipX = true quando player está à direita
        bool playerIsRight = playerTransform.position.x > transform.position.x;
        spriteRenderer.flipX = playerIsRight;
    }

    private void ExecuteAttack(int attackIndex)
    {
        isExecutingAttack = true;
        StopMovement();

        switch (attackIndex)
        {
            case 0:
                StartCoroutine(PerformPunchAttackRoutine());
                break;
            case 1:
                StartCoroutine(PerformJumpSlamAttackRoutine());
                break;
            case 2:
                StartCoroutine(PerformRockThrowAttackRoutine());
                break;
        }
    }

    private void PlanNextAttack()
    {
        attackCycleIndex++;
        // Ciclo balanceado: 2 (Pedra Ranged) -> 1 (Pulo Ranged) -> 0 (Soco Melee) -> 2 (Pedra)...
        plannedNextAttack = attackCycleIndex % 3;
        Debug.Log($"[MapinguariBoss] Próximo ataque planejado: {(plannedNextAttack == 0 ? "Soco Melee" : plannedNextAttack == 1 ? "Pulo com Sombra (Ranged)" : "Pedra Giratória (Ranged)")}");
    }

    #region Ataque 1: Soco / Porradona Melee
    private IEnumerator PerformPunchAttackRoutine()
    {
        Debug.Log("[MapinguariBoss] Executando Ataque Melee: Soco / Porradona!");
        if (animator != null) animator.SetTrigger(PunchHash);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(PunchSFX);
        }

        // Windup até o frame de impacto
        yield return new WaitForSeconds(0.25f);

        Vector3 forwardDir = spriteRenderer.flipX ? Vector3.right : Vector3.left;
        Vector3 punchPoint = transform.position + forwardDir * 1.2f + Vector3.up * 0.4f;

        // VFX de impacto
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(punchPoint, new Color(1f, 0.6f, 0.2f), 1.3f);
        }

        // Detecção de colisão no Player
        Collider2D[] hits = Physics2D.OverlapCircleAll(punchPoint, 1.4f, playerLayerMask);
        foreach (var hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            IDamageable dmg = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (dmg != null && !(dmg is MapinguariBossController))
            {
                dmg.TakeDamage(punchDamage, forwardDir);
            }
        }

        // Camera Shake
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.TriggerCameraShake(0.25f, 0.18f);
        }

        yield return new WaitForSeconds(0.25f);

        PlanNextAttack();
        attackTimer = attackCooldown;
        isExecutingAttack = false;
    }
    #endregion

    #region Ataque 2: Pulo com Sombra e Esmagamento AoE
    private IEnumerator PerformJumpSlamAttackRoutine()
    {
        Debug.Log("[MapinguariBoss] Executando Ataque 2: Pulo Alto e Esmagamento no Chão!");

        if (animator != null) animator.SetTrigger(JumpHash);
        if (bodyCollider != null) bodyCollider.enabled = false;

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(JumpingSFX);
        }

        // 1. Pulo Reto para Cima (Saindo completamente da tela)
        Vector3 startPos = transform.position;
        Vector3 offscreenUpPos = startPos + Vector3.up * 14f;

        float launchDuration = 0.38f;
        float elapsed = 0f;

        while (elapsed < launchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / launchDuration;
            transform.position = Vector3.Lerp(startPos, offscreenUpPos, t * t);
            yield return null;
        }

        // 2. Marca a Sombra no Chão onde o Player está
        Vector3 targetLandPos = playerTransform != null ? playerTransform.position : startPos;
        GameObject shadowObj = CreateTelegraphShadow(targetLandPos, slamRadius);

        // Posiciona o Boss no alto exatamente acima da sombra (fora da tela)
        Vector3 offscreenAboveTarget = targetLandPos + Vector3.up * 14f;
        transform.position = offscreenAboveTarget;

        // Tempo de telegrafia da sombra no chão para o player reagir
        yield return new WaitForSeconds(0.65f);

        // 3. Queda Reta Vertical em direção à Sombra
        float dropDuration = 0.22f;
        elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dropDuration;
            transform.position = Vector3.Lerp(offscreenAboveTarget, targetLandPos, t * t); // Queda acelerada reta
            yield return null;
        }

        transform.position = targetLandPos;
        if (bodyCollider != null) bodyCollider.enabled = true;

        // Destrói o aviso telegrafado da sombra
        if (shadowObj != null) Destroy(shadowObj);

        // 4. Impacto e Esmagamento no Chão (Slam)
        Debug.Log("[MapinguariBoss] Esmagamento no Chão realizado!");

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(LandingSFX);
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(targetLandPos, new Color(0.85f, 0.45f, 0.15f), 2.4f);
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.TriggerCameraShake(0.5f, 0.4f);
        }

        // Dano em Área (AoE)
        Collider2D[] slamHits = Physics2D.OverlapCircleAll(targetLandPos, slamRadius, playerLayerMask);
        foreach (var hit in slamHits)
        {
            if (hit == null || hit.isTrigger) continue;
            IDamageable dmg = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (dmg != null && !(dmg is MapinguariBossController))
            {
                Vector3 pushDir = (hit.transform.position - targetLandPos).normalized;
                dmg.TakeDamage(slamDamage, pushDir);
            }
        }

        yield return new WaitForSeconds(0.4f);

        PlanNextAttack();
        attackTimer = attackCooldown * 1.3f;
        isExecutingAttack = false;
    }

    private GameObject CreateTelegraphShadow(Vector3 position, float radius)
    {
        GameObject shadow = new GameObject("VFX_Mapinguari_Shadow");
        shadow.transform.position = position;
        shadow.transform.localScale = Vector3.zero;

        SpriteRenderer sr = shadow.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(0.08f, 0.05f, 0.02f, 0.55f);
        sr.sortingOrder = 2;

        // Expansão da sombra até o raio completo do slam
        Tween.Scale(shadow.transform, endValue: new Vector3(radius * 2f, radius * 1.3f, 1f), duration: slamWindupTime, ease: Ease.OutQuad);

        return shadow;
    }

    private Sprite CreateCircleSprite()
    {
        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(32, 32);
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= 30f)
                {
                    float alpha = Mathf.Clamp01((30f - dist) / 6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32f);
    }
    #endregion

    #region Ataque 3: Arremesso de Pedra Giratória
    private IEnumerator PerformRockThrowAttackRoutine()
    {
        Debug.Log("[MapinguariBoss] Executando Ataque 3: Arremesso de Pedra Giratória!");

        if (animator != null) animator.SetTrigger(ThrowHash);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(ThrowingSFX);
        }

        // Windup do arremesso
        yield return new WaitForSeconds(0.24f);

        Vector3 forwardDir = (playerTransform != null) ? (playerTransform.position - transform.position).normalized : (spriteRenderer.flipX ? Vector3.right : Vector3.left);
        Vector3 handPos = transform.position + forwardDir * 0.9f + Vector3.up * 0.8f;

        if (rockProjectilePrefab != null)
        {
            GameObject rockObj = Instantiate(rockProjectilePrefab, handPos, Quaternion.identity);

            // Adiciona rotação 360 contínua na pedra
            if (rockObj.GetComponent<SpinningObject>() == null)
            {
                rockObj.AddComponent<SpinningObject>();
            }

            if (rockObj.TryGetComponent(out Projectile proj))
            {
                proj.Initialize(forwardDir, gameObject, rockDamage, playerLayerMask);
            }
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(handPos, new Color(0.7f, 0.6f, 0.5f), 0.8f);
        }

        yield return new WaitForSeconds(0.25f);

        PlanNextAttack();
        attackTimer = attackCooldown * 1.1f;
        isExecutingAttack = false;
    }
    #endregion

    #region IDamageable & Dano
    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Debug.Log($"[MapinguariBoss] {bossName} recebeu {damage} de dano! Vida restante: {currentHealth}/{maxHealth}");

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.UpdateHealth(currentHealth, maxHealth);
        }

        StartCoroutine(DamageFlashRoutine());

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = damageFlashColor;
            yield return new WaitForSeconds(0.12f);
            spriteRenderer.color = Color.white;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        isCombatActive = false;
        StopMovement();

        Debug.Log($"[MapinguariBoss] {bossName} FOI DERROTADO!");

        if (animator != null) animator.SetTrigger(DeathHash);
        if (bodyCollider != null) bodyCollider.enabled = false;

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.HideBoss(true);
        }

        // Mãe do Ouro surge onde o boss foi derrotado (ela cuidará do drop e da cura)
        MaeDoOuroBossRewardNPC.SpawnAfterBoss(transform.position, BossDefeatedType.Mapinguari);

        Destroy(gameObject, 2.5f);
    }
    #endregion
}
