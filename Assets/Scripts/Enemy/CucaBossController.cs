using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador Principal do Boss Final: Cuca (A Bruxa da Floresta).
/// Possui 2 Fases com transição completa e junção dos poderes de todos os bosses.
/// </summary>
public class CucaBossController : MonoBehaviour, IDamageable
{
    [Header("Identificação do Chefe")]
    [SerializeField] private string bossName = "CUCA";
    [SerializeField] private int currentPhase = 1;

    [Header("Atributos de Vida")]
    [SerializeField] private float phase1MaxHealth = 350f;
    [SerializeField] private float phase2MaxHealth = 500f;
    [SerializeField] private float currentHealth = 350f;

    [Header("Configurações da Arena")]
    [SerializeField] private Vector3 arenaCenter = Vector3.zero;
    [SerializeField] private float arenaRadius = 9.5f;
    [SerializeField] private bool useSpawnPositionAsArenaCenter = true;

    [Header("Fase 1: Configurações")]
    [SerializeField] private float phase1AttackCooldown = 2.2f;
    [SerializeField] private int phase1OrbCount = 14;
    [SerializeField] private float phase1OrbDamage = 16f;
    [SerializeField] private float orbSpawnCenterOffsetY = 1.15f;

    [Header("Fase 2: Configurações")]
    [SerializeField] private float phase2MoveSpeed = 3.2f;
    [SerializeField] private float meleeRange = 2.2f;
    [SerializeField] private float meleeDamage = 26f;
    [SerializeField] private float meleeCooldown = 2.0f;
    [SerializeField] private float spellCooldown = 3.2f;

    [Header("Prefabs de Poderes & Habilidades")]
    [SerializeField] private GameObject purpleOrbPrefab;
    [SerializeField] private GameObject corpoSecoPrefab;
    [SerializeField] private GameObject mapinguariGhostPrefab;
    [SerializeField] private GameObject starPickupPrefab;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Efeitos Visuais")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Header("Ativação de Combate")]
    [SerializeField] private bool autoStartCombat = false;
    [SerializeField] private float playerDetectionRadius = 9.0f;
    //Audio SFX
    [Header("SFX do Personagem")]
    [SerializeField] private AudioClip magicBallSFX;
    [SerializeField] private AudioClip magicCastSFX;
    [SerializeField] private AudioClip LandingSFX;
    [SerializeField] private AudioClip MeleeSFX;
    [SerializeField] private AudioClip RoarSFX;
    [SerializeField] private AudioClip arrastandoSFX;
    [Tooltip("AudioSource dedicado a esse som. Se deixado vazio, um é criado automaticamente.")]
    [SerializeField] private AudioSource draggingAudioSource;

    // Componentes
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Transform playerTransform;
    private Material defaultMaterial;

    // Estados
    private bool isCombatActive = false;
    private bool isExecutingAction = false;
    private bool isTransitioning = false;
    private bool isDead = false;

    private float attackTimer = 1.5f;
    private float meleeTimer = 0f;
    private int spellCycleIndex = 0;
    private List<GameObject> activeMinions = new List<GameObject>();

    // Hashes do Animator
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackP1Hash = Animator.StringToHash("AttackP1");
    private static readonly int TransformP2Hash = Animator.StringToHash("TransformP2");
    private static readonly int MeleeP2Hash = Animator.StringToHash("MeleeP2");
    private static readonly int MagicP2Hash = Animator.StringToHash("MagicP2");
    private static readonly int DeathHash = Animator.StringToHash("Death");

    public float CurrentHealth => currentHealth;
    public float MaxHealth => (currentPhase == 1) ? phase1MaxHealth : phase2MaxHealth;
    public int CurrentPhase => currentPhase;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentPhase = 1;
        currentHealth = phase1MaxHealth;

        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }
        if (animator.runtimeAnimatorController == null)
        {
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/Enemies/Cuca/Cuca")
                                               ?? Resources.Load<RuntimeAnimatorController>("Cuca");
#if UNITY_EDITOR
            if (animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Enemies/Cuca/Cuca.controller");
            }
#endif
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;
            rb.linearVelocity = Vector2.zero;
        }

        agent = GetComponent<NavMeshAgent>();
        bodyCollider = GetComponent<Collider2D>();

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = phase2MoveSpeed;
            agent.enabled = false; // Desabilitado na Fase 1 (ela é estática)
        }
    }

    private void Start()
    {
        if (useSpawnPositionAsArenaCenter)
        {
            arenaCenter = transform.position;
        }

        FindPlayer();
        if (autoStartCombat)
        {
            StartCombat();
        }
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    [Header("Ativação e Cutscene")]
    [SerializeField] private BossTrigger bossIntro;

    public void StartCombat()
    {
        if (isCombatActive) return;

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

        isCombatActive = true;
        isDead = false;
        isTransitioning = false;
        currentPhase = 1;
        currentHealth = phase1MaxHealth;
        attackTimer = 2.0f;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }
        if (agent != null) agent.enabled = false;

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.ShowBoss($"{bossName} (Fase 1)", currentHealth, phase1MaxHealth);
        }

        Debug.Log($"[CucaBoss] Combate iniciado contra a {bossName} (Fase 1)! Primeiro ataque em {attackTimer}s.");
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
        }

        // Se o combate ainda não começou, inicia automaticamente quando o jogador se aproxima
        if (!isCombatActive)
        {
            if (playerTransform != null)
            {
                FlipTowards(playerTransform.position);
                float dist = Vector3.Distance(transform.position, playerTransform.position);
                if (dist <= playerDetectionRadius)
                {
                    StartCombat();
                }
            }
            return;
        }

        bool isCutscene = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Playing;
        if (isDead || isTransitioning || isCutscene)
        {
            StopMovement();
            return;
        }

        if (playerTransform != null)
        {
            // Mantém a orientação visual olhando para o jogador
            FlipTowards(playerTransform.position);
        }

        if (currentPhase == 1)
        {
            UpdatePhase1();
        }
        else
        {
            UpdatePhase2();
        }
    }

    #region Fase 1: Estática & Disparos 360°
    private void UpdatePhase1()
    {
        // Na Fase 1 a Cuca é 100% imóvel e o jogador não consegue empurrá-la
        transform.position = arenaCenter;
        if (rb != null && rb.bodyType != RigidbodyType2D.Kinematic)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && !isExecutingAction)
        {
            StartCoroutine(PerformPhase1AttackRoutine());
        }
    }

    private IEnumerator PerformPhase1AttackRoutine()
    {
        isExecutingAction = true;
        Debug.Log("[CucaBoss] Fase 1: Conjuração de Esferas Roxas 360°!");

        if (animator != null) animator.SetTrigger(AttackP1Hash);

        yield return new WaitForSeconds(0.28f);

        // Dispara uma onda de esferas roxas místicas em 360 graus
        SpawnRadialOrbs(phase1OrbCount, phase1OrbDamage, 7.5f, 0f);

        // Se estiver abaixo de 50% de vida na fase 1, dispara uma segunda onda com offset
        if (currentHealth / phase1MaxHealth < 0.50f)
        {
            yield return new WaitForSeconds(0.35f);
            SpawnRadialOrbs(phase1OrbCount, phase1OrbDamage, 8.5f, 180f / phase1OrbCount);
        }

        yield return new WaitForSeconds(0.4f);

        attackTimer = phase1AttackCooldown;
        isExecutingAction = false;
    }

    public Vector3 GetBodyCenterPosition()
    {
        return transform.position + Vector3.up * orbSpawnCenterOffsetY;
    }

    private void SpawnRadialOrbs(int count, float damage, float speed, float startAngleOffset)
    {
        if (purpleOrbPrefab == null) return;

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(magicBallSFX);
        }

        Vector3 centerPos = GetBodyCenterPosition();
        float angleStep = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = startAngleOffset + (i * angleStep);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;

            Vector3 spawnPos = centerPos + dir * 0.8f;
            GameObject orbObj = Instantiate(purpleOrbPrefab, spawnPos, Quaternion.identity);

            if (orbObj.TryGetComponent(out Projectile proj))
            {
                proj.Initialize(dir, gameObject, damage);
            }
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(centerPos, new Color(0.7f, 0.1f, 0.9f), 2.2f);
        }
    }
    #endregion

    #region Transição: Fase 1 ➔ Fase 2
    private IEnumerator PhaseTransitionRoutine()
    {
        isTransitioning = true;
        isExecutingAction = true;
        StopMovement();

        Debug.Log("[CucaBoss] Fase 1 DERROTADA! Iniciando transformação para Fase 2...");

        // Toca animação de transformação da Coluna 3
        if (animator != null)
        {
            animator.SetTrigger(TransformP2Hash);
        }

        // Efeito de energia sombria crescente
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(GetBodyCenterPosition(), new Color(0.85f, 0.1f, 1f), 3.0f);
        }

        yield return new WaitForSeconds(0.85f);

        // Conclui transformação e habilita física dinâmica e movimentação para Fase 2
        currentPhase = 2;
        currentHealth = phase2MaxHealth;
        isTransitioning = false;
        isExecutingAction = false;
        attackTimer = 1.2f;
        meleeTimer = 0f;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.mass = 500f;
        }

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            agent.speed = phase2MoveSpeed;
        }

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.ShowBoss($"{bossName} (Fase Final)", currentHealth, phase2MaxHealth);
        }

        Debug.Log("[CucaBoss] FASE 2 INICIADA! Cuca liberou todos os poderes dos bosses e começou a se mover!");
    }
    #endregion

    #region Fase 2: Móvel, Melee & Junção dos Bosses
    private void UpdatePhase2()
    {
        meleeTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;

        if (isExecutingAction) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Se estiver muito perto do jogador, desfere ataque Melee
        if (distToPlayer <= meleeRange && meleeTimer <= 0f)
        {
            StartCoroutine(PerformPhase2MeleeRoutine());
            return;
        }

        // Se o timer de magia estiver pronto, conjura um dos poderes dos bosses
        if (attackTimer <= 0f)
        {
            ExecuteNextBossSpell();
            return;
        }

        // Caso contrário, persegue o jogador pela arena
        MoveToPosition(playerTransform.position);
    }

    private IEnumerator PerformPhase2MeleeRoutine()
    {
        isExecutingAction = true;
        StopMovement();

        Debug.Log("[CucaBoss] Fase 2: Ataque Melee de Garras!");
        if (animator != null) animator.SetTrigger(MeleeP2Hash);

        yield return new WaitForSeconds(0.22f);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(MeleeSFX);
        }

        // Dano Melee em cone/arco frontal
        Vector3 bodyCenter = GetBodyCenterPosition();
        Vector3 forward = (playerTransform.position - bodyCenter).normalized;
        Collider2D[] hits = Physics2D.OverlapCircleAll(bodyCenter + forward * 1.2f, 1.4f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") && hit.TryGetComponent(out IDamageable dmg))
            {
                dmg.TakeDamage(meleeDamage, forward);
            }
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(bodyCenter + forward * 1.2f, new Color(0.9f, 0.1f, 0.3f), 1.8f);
        }

        yield return new WaitForSeconds(0.35f);

        meleeTimer = meleeCooldown;
        isExecutingAction = false;
    }

    private void ExecuteNextBossSpell()
    {
        isExecutingAction = true;
        StopMovement();

        // Limpa lacaios mortos
        activeMinions.RemoveAll(item => item == null || !item.activeInHierarchy || (item.TryGetComponent(out EnemyStats es) && es.CurrentHealth <= 0));

        int spellIndex = spellCycleIndex % 4;
        spellCycleIndex++;

        // Se a magia for invocação mas já houver lacaios vivos, pula para a próxima magia
        if (spellIndex == 1 && activeMinions.Count > 0)
        {
            spellIndex = 2; // Usa Mapinguari Slam
        }

        switch (spellIndex)
        {
            case 0:
                StartCoroutine(PerformCucaSpiralOrbsRoutine());
                break;
            case 1:
                StartCoroutine(PerformMatintaSummonRoutine());
                break;
            case 2:
                StartCoroutine(PerformMapinguariSlamRoutine());
                break;
            case 3:
                StartCoroutine(PerformBoitataDashRoutine());
                break;
        }
    }

    // Poder 1: Cuca 360° Espiral Duplo
    private IEnumerator PerformCucaSpiralOrbsRoutine()
    {
        Debug.Log("[CucaBoss] Poder Cuca: Salva Dupla em Espiral de Esferas!");
        if (animator != null) animator.SetTrigger(MagicP2Hash);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(magicBallSFX);
        }

        yield return new WaitForSeconds(0.25f);
        SpawnRadialOrbs(16, phase1OrbDamage * 1.15f, 8.5f, 0f);

        yield return new WaitForSeconds(0.3f);
        SpawnRadialOrbs(16, phase1OrbDamage * 1.15f, 9.0f, 11.25f);

        yield return new WaitForSeconds(0.35f);
        attackTimer = spellCooldown;
        isExecutingAction = false;
    }

    // Poder 2: Matinta Invocação de Corpo-Seco
    private IEnumerator PerformMatintaSummonRoutine()
    {
        Debug.Log("[CucaBoss] Poder Matinta: Invocação de Lacaios Corpo-Seco!");
        if (animator != null) animator.SetTrigger(MagicP2Hash);

        yield return new WaitForSeconds(0.28f);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(magicCastSFX);
        }

        if (corpoSecoPrefab != null)
        {
            int summonCount = 2;
            for (int i = 0; i < summonCount; i++)
            {
                float angle = (i == 0 ? -45f : 45f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 2.2f, Mathf.Sin(angle) * 1.5f, 0f);
                Vector3 spawnPos = ClampToArena(transform.position + offset);

                if (CombatVisualEffects.Instance != null)
                {
                    CombatVisualEffects.Instance.PlayImpactBurst(spawnPos, new Color(0.2f, 0.05f, 0.35f), 1.6f);
                }

                GameObject minion = Instantiate(corpoSecoPrefab, spawnPos, Quaternion.identity);
                activeMinions.Add(minion);
            }
        }

        yield return new WaitForSeconds(0.4f);
        attackTimer = spellCooldown;
        isExecutingAction = false;
    }

    // Poder 3: Mapinguari Slam Queda do Céu
    private IEnumerator PerformMapinguariSlamRoutine()
    {
        Debug.Log("[CucaBoss] Poder Mapinguari: Queda do Céu com Impacto!");
        if (animator != null) animator.SetTrigger(MagicP2Hash);

        yield return new WaitForSeconds(0.25f);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(LandingSFX);
        }

        if (mapinguariGhostPrefab != null && playerTransform != null)
        {
            Vector3 targetPos = ClampToArena(playerTransform.position);
            GameObject slamObj = Instantiate(mapinguariGhostPrefab, targetPos, Quaternion.identity);
            if (slamObj.TryGetComponent(out CucaMapinguariGhostSlam slamComp))
            {
                slamComp.Initialize(targetPos, playerLayerMask);
            }
        }

        yield return new WaitForSeconds(0.5f);
        attackTimer = spellCooldown;
        isExecutingAction = false;
    }

    // Poder 4: Boitatá Dash Modular (Sombra telegrafada + corpo modular da serpente passando sobre ela)

    private void PlayDraggingSFX()
    {
        if (draggingAudioSource == null || arrastandoSFX == null) return;

        float sfxVolume = AudioController.Instance != null ? AudioController.Instance.SFXVolume : 1f;
        float masterVolume = AudioController.Instance != null ? AudioController.Instance.MasterVolume : 1f;

        draggingAudioSource.clip = arrastandoSFX;
        draggingAudioSource.volume = masterVolume * sfxVolume;
        draggingAudioSource.Play();
    }

    private void StopDraggingSFX()
    {
        if (draggingAudioSource != null && draggingAudioSource.isPlaying)
        {
            draggingAudioSource.Stop();
        }
    }

    private IEnumerator PerformBoitataDashRoutine()
    {
        Debug.Log("[CucaBoss] Poder Boitatá: Investida de Fogo Modular!");
        if (animator != null) animator.SetTrigger(MagicP2Hash);
        PlayDraggingSFX();
        yield return new WaitForSeconds(0.25f);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(RoarSFX);
        }

        if (playerTransform != null && BossTelegraphVisuals.Instance != null)
        {
            Vector3 playerPos = playerTransform.position;
            Vector3 dir = (playerPos - transform.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.right;

            Vector3 startPos = ClampToArena(playerPos - dir * (arenaRadius * 0.85f));
            Vector3 endPos = ClampToArena(playerPos + dir * (arenaRadius * 0.85f));
            Vector3[] path = new Vector3[] { startPos, endPos };

            // 1. Spawna a telegrafia de sombra/perigo no chão
            BossTelegraphVisuals.Instance.CreateDangerLine(startPos, endPos, 1.3f, 0.65f, new Color(0.12f, 0.04f, 0.18f, 0.7f));

            yield return new WaitForSeconds(0.65f);

            // 2. Dispara o corpo modular da serpente Boitatá rasgando o caminho sobre a sombra!
            BossTelegraphVisuals.Instance.SpawnFireSerpentDash(path, 16.5f, 22f, playerLayerMask);
        }

        yield return new WaitForSeconds(0.5f);
        attackTimer = spellCooldown;
        isExecutingAction = false;
        StopDraggingSFX();
    }
    #endregion

    #region Movimentação & Utilidades
    private void MoveToPosition(Vector3 destination)
    {
        destination = ClampToArena(destination);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        }
        else
        {
            Vector3 dir = (destination - transform.position).normalized;
            transform.position += dir * (phase2MoveSpeed * Time.deltaTime);
            transform.position = ClampToArena(transform.position);
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetFloat(SpeedHash, phase2MoveSpeed);
        }
    }

    private void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetFloat(SpeedHash, 0f);
    }

    private void FlipTowards(Vector3 targetPos)
    {
        if (spriteRenderer == null) return;
        if (targetPos.x < transform.position.x)
        {
            spriteRenderer.flipX = false;
        }
        else if (targetPos.x > transform.position.x)
        {
            spriteRenderer.flipX = true;
        }
    }

    private Vector3 ClampToArena(Vector3 pos)
    {
        Vector3 offset = pos - arenaCenter;
        if (offset.magnitude > arenaRadius)
        {
            return arenaCenter + offset.normalized * arenaRadius;
        }
        return pos;
    }
    #endregion

    #region IDamageable & Morte
    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead || isTransitioning) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        Debug.Log($"[CucaBoss] {bossName} (Fase {currentPhase}) recebeu {damage} de dano! Vida restante: {currentHealth}/{MaxHealth}");

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.UpdateHealth(currentHealth, MaxHealth);
        }

        if (currentHealth <= 0f)
        {
            if (currentPhase == 1)
            {
                // Transição para Fase 2
                StartCoroutine(PhaseTransitionRoutine());
                return;
            }
            else
            {
                // Morte Definitiva na Fase 2
                isDead = true;
                isCombatActive = false;
                StopAllCoroutines();
                StartCoroutine(DeathRoutine());
                return;
            }
        }

        if (damageFlashRoutine != null)
        {
            StopCoroutine(damageFlashRoutine);
        }
        damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
    }

    private Coroutine damageFlashRoutine;

    private IEnumerator DamageFlashRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = damageFlashColor;
            yield return new WaitForSeconds(0.12f);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }
        damageFlashRoutine = null;
    }

    private IEnumerator DeathRoutine()
    {
        isDead = true;
        isCombatActive = false;
        this.enabled = false; // Desativa o Update() para evitar chamar StartCombat() novamente na morte
        StopMovement();

        // Congela o jogador imediatamente definindo o estado para Cutscene
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Cutscene);
        }

        // Para a música do Boss com fade out suave
        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopBGM(fadeDuration: 1.5f);
        }

        Debug.Log($"[CucaBoss] {bossName} FOI TOTALMENTE DERROTADA!");

        if (bodyCollider != null) bodyCollider.enabled = false;
        if (agent != null) agent.enabled = false;

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.HideBoss(true);
        }

        if (animator != null)
        {
            animator.SetTrigger(DeathHash);
        }

        // Mata todos os lacaios restantes acionando a animação de morte deles
        foreach (var m in activeMinions)
        {
            if (m != null)
            {
                if (m.TryGetComponent(out IDamageable dmg))
                {
                    dmg.TakeDamage(9999f, Vector3.zero);
                }
                else
                {
                    Destroy(m, 0.5f);
                }
            }
        }
        activeMinions.Clear();

        yield return new WaitForSeconds(0.85f);

        // Efeito de explosão mágica final
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.8f, 0.1f, 1f), 3.5f);
        }

        if (spriteRenderer != null) spriteRenderer.enabled = false;

        // Aguarda o fim do fade-out completo da barra de vida do boss e da música (Total de 2.5s desde a morte)
        yield return new WaitForSeconds(1.65f);

        // Se o boss for a Cuca, reproduz a cutscene final antes de spawnar a Mãe do Ouro
        GameObject endCutscenePrefab = Resources.Load<GameObject>("Cinematic/End_Cutscene")
                                       ?? Resources.Load<GameObject>("Prefabs/Cinematic/End_Cutscene")
                                       ?? Resources.Load<GameObject>("End_Cutscene");

        if (endCutscenePrefab != null && CinematicManager.Instance != null)
        {
            bool cutsceneFinished = false;
            CinematicManager.Instance.onEnd = () =>
            {
                cutsceneFinished = true;
            };

            CinematicManager.Instance.PlayClip(endCutscenePrefab);

            // Aguarda a reprodução da cutscene terminar
            while (!cutsceneFinished)
            {
                yield return null;
            }
        }

        // Mãe do Ouro surge onde o boss foi derrotado (ela cuidará do drop e da cura)
        MaeDoOuroBossRewardNPC.SpawnAfterBoss(transform.position, BossDefeatedType.Cuca);

        yield return new WaitForSeconds(0.4f);

        Destroy(gameObject);
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.3f, 1f, 0.4f);
        Vector3 center = useSpawnPositionAsArenaCenter && !Application.isPlaying ? transform.position : arenaCenter;
        Gizmos.DrawWireSphere(center, arenaRadius);
    }
}
