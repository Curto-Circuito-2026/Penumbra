using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador Principal da IA do Inimigo.
/// Gerencia a Máquina de Estados (Idle, Chase, MeleeAttack, RangedAttack, Dead),
/// navegação 2D Top-Down com NavMeshAgent + NavMesh.CalculatePath fallback,
/// detecção por Linha de Visão e renderização de Gizmos em tempo de execução.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyCombatController))]
public class EnemyAIController : MonoBehaviour
{
    [Header("Configurações Reutilizáveis (Optional SO)")]
    [SerializeField] private EnemyConfigSO enemyConfig;

    [Header("Flags de Tipo de Inimigo (Inspector)")]
    [Tooltip("Permite o uso de ataques Melee de faca.")]
    [SerializeField] private bool canUseMelee = true;

    [Tooltip("Permite o uso de tiros Ranged em linha reta.")]
    [SerializeField] private bool canUseRanged = true;

    [Header("Comportamento Creeper / Explosão Suicide")]
    [Tooltip("Permite o inimigo agir como Creeper: corre até o player e explode.")]
    [SerializeField] private bool canExplode = false;

    [Tooltip("Distância do player em que inicia a contagem do pavio/animação de explosão.")]
    [SerializeField] private float explosionTriggerDistance = 1.8f;

    [Tooltip("Raio da explosão em área.")]
    [SerializeField] private float explosionRadius = 2.8f;

    [Tooltip("Dano causado pela explosão no centro.")]
    [SerializeField] private float explosionDamage = 40f;

    [Tooltip("Tempo de pavio/animação de preparação antes da detonação em segundos.")]
    [SerializeField] private float explosionFuseTime = 0.55f;

    [Header("Alcances e Detecção")]
    [Tooltip("Raio de detecção de visão do Player.")]
    [SerializeField] private float detectionRadius = 10f;

    [Tooltip("Alcance de ataque Melee (Faca).")]
    [SerializeField] private float meleeRange = 1.8f;

    [Tooltip("Alcance de ataque Ranged (Distância de tiro/segurança).")]
    [SerializeField] private float rangedRange = 7.5f;

    [Tooltip("Tempo de recarga entre ataques em segundos.")]
    [SerializeField] private float attackCooldown = 1.5f;

    [Tooltip("Tempo de espera na animação de ataque ranged até instanciar o projétil em segundos.")]
    [SerializeField] private float rangedAttackWindupDelay = 0.28f;

    [Tooltip("Duração total da animação de ataque ranged em segundos.")]
    [SerializeField] private float rangedAttackDuration = 0.45f;

    [Tooltip("Velocidade de movimentação no NavMesh.")]
    [SerializeField] private float movementSpeed = 3.5f;

    [Header("Detecção de Visão e Paredes")]
    [Tooltip("Máscara das camadas que bloqueiam a visão do inimigo (Obstáculos e Paredes).")]
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("Prefabs de Ataque")]
    [Tooltip("Prefab do Projétil em linha reta (mesmo do Player).")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Debug e Gizmos")]
    [Tooltip("Exibe os círculos de alcance (Visão, Melee, Ranged, Parada) na Scene View.")]
    [SerializeField] private bool showGizmos = true;

    [Tooltip("Exibe as legendas de texto com os valores exatos de metros de cada raio.")]
    [SerializeField] private bool showGizmoLabels = true;

    [Header("Componentes")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private EnemyStats enemyStats;
    [SerializeField] private EnemyCombatController combatController;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Estados da IA
    public IEnemyState CurrentState { get; private set; }
    public EnemyIdleState IdleState { get; private set; }
    public EnemyChaseState ChaseState { get; private set; }
    public EnemyMeleeAttackState MeleeAttackState { get; private set; }
    public EnemyRangedAttackState RangedAttackState { get; private set; }
    public EnemyExplodeState ExplodeState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    // Alvo do Player
    public Transform TargetPlayer { get; private set; }
    private PlayerStats playerStats;

    // Timers e Pathfinding
    private float attackCooldownTimer = 0f;
    private float baseMovementSpeed = -1f;
    private NavMeshPath fallbackPath;

    public bool CanUseMelee => canUseMelee;
    public bool CanUseRanged => canUseRanged;
    public bool CanExplode => canExplode;
    public float ExplosionTriggerDistance => explosionTriggerDistance;
    public float ExplosionRadius => explosionRadius;
    public float ExplosionDamage => explosionDamage;
    public float ExplosionFuseTime => explosionFuseTime;
    public float DetectionRadius => detectionRadius;
    public float MeleeRange => meleeRange;
    public float RangedRange => rangedRange;
    public float AttackCooldown => attackCooldown;
    public float RangedAttackWindupDelay => rangedAttackWindupDelay;
    public float RangedAttackDuration => rangedAttackDuration;
    public bool IsAttackOnCooldown => attackCooldownTimer > 0f;
    public GameObject ProjectilePrefab => projectilePrefab;
    public EnemyCombatController CombatController => combatController;
    public EnemyStats EnemyStats => enemyStats;
    public Animator Animator => animator;

    // Hashes do Animator idênticos ao do Player
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackMeleeHash = Animator.StringToHash("AttackMelee");
    private static readonly int AttackRangedHash = Animator.StringToHash("AttackRanged");
    private static readonly int DeathHash = Animator.StringToHash("Death");

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (enemyStats == null) enemyStats = GetComponent<EnemyStats>();
        if (combatController == null) combatController = GetComponent<EnemyCombatController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        fallbackPath = new NavMeshPath();

        // Evita conflitos entre Rigidbody2D e NavMeshAgent
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        // Configuração do NavMeshAgent para Plano 2D XY Top-Down
        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = movementSpeed;

            // Configura o stoppingDistance para o agente parar ANTES de colidir/empurrar o player
            float stopDist = canUseMelee ? meleeRange * 0.75f : rangedRange * 0.7f;
            agent.stoppingDistance = Mathf.Max(0.8f, stopDist);
        }

        if (obstacleLayerMask == 0)
        {
            obstacleLayerMask = LayerMask.GetMask("Obstacle");
        }

        // Aplica EnemyConfigSO se fornecido
        if (enemyConfig != null)
        {
            canUseMelee = enemyConfig.canUseMelee;
            canUseRanged = enemyConfig.canUseRanged;
            canExplode = enemyConfig.canExplode;
            explosionTriggerDistance = enemyConfig.explosionTriggerDistance;
            explosionRadius = enemyConfig.explosionRadius;
            explosionDamage = enemyConfig.explosionDamage;
            explosionFuseTime = enemyConfig.explosionFuseTime;
            detectionRadius = enemyConfig.detectionRadius;
            meleeRange = enemyConfig.meleeRange;
            rangedRange = enemyConfig.rangedRange;
            attackCooldown = enemyConfig.attackCooldown;
            movementSpeed = enemyConfig.moveSpeed;
            if (enemyConfig.projectilePrefab != null) projectilePrefab = enemyConfig.projectilePrefab;

            if (agent != null) agent.speed = movementSpeed;
            if (combatController != null) combatController.Configure(enemyConfig);
        }

        // Instancia os estados da Máquina de Estados
        IdleState = new EnemyIdleState(this);
        ChaseState = new EnemyChaseState(this);
        MeleeAttackState = new EnemyMeleeAttackState(this);
        RangedAttackState = new EnemyRangedAttackState(this);
        ExplodeState = new EnemyExplodeState(this);
        DeadState = new EnemyDeadState(this);
    }

    private void OnEnable()
    {
        if (enemyStats != null)
        {
            enemyStats.OnEnemyDied += HandleEnemyDied;
        }
    }

    private void OnDisable()
    {
        if (enemyStats != null)
        {
            enemyStats.OnEnemyDied -= HandleEnemyDied;
        }
    }

    private void Start()
    {
        // Garante que o inimigo e o NavMeshAgent estejam no plano Z = 0
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        if (baseMovementSpeed <= 0f) baseMovementSpeed = movementSpeed;

        // Garante o alinhamento com o NavMesh no início
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        // Aplica o escalonamento da fase atual do StageManager
        if (StageManager.Instance != null)
        {
            ApplyStageScaling(StageManager.Instance.CurrentStage);
        }

        FindPlayerTarget();
        ChangeState(IdleState);
    }

    /// <summary>
    /// Aplica o escalonamento de força da fase atual nos atributos do inimigo (Vida, Dano e Velocidade).
    /// </summary>
    public void ApplyStageScaling(int stage)
    {
        if (StageManager.Instance == null) return;

        float healthMult = StageManager.Instance.GetHealthMultiplier(stage);
        float damageMult = StageManager.Instance.GetDamageMultiplier(stage);
        float speedMult = StageManager.Instance.GetSpeedMultiplier(stage);

        if (enemyStats != null)
        {
            enemyStats.ApplyLevelScaling(healthMult);
        }

        if (combatController != null)
        {
            combatController.ApplyLevelScaling(damageMult);
        }

        if (agent != null)
        {
            if (baseMovementSpeed <= 0f) baseMovementSpeed = movementSpeed;
            movementSpeed = baseMovementSpeed * speedMult;
            agent.speed = movementSpeed;
        }

        Debug.Log($"[EnemyAIController] '{gameObject.name}' escalado para a Fase {stage}! (HP: {enemyStats?.MaxHealth}, Velocidade: {movementSpeed:F1})");
    }

    private void Update()
    {
        if (enemyStats != null && enemyStats.IsDead && CurrentState != ExplodeState) return;

        // Atualiza timer de cooldown de ataque
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer < 0f) attackCooldownTimer = 0f;
        }

        // Executa a lógica do estado atual
        if (CurrentState != null)
        {
            CurrentState.Update();
        }

        // Atualiza virada do sprite (flip)
        if (CurrentState != ExplodeState)
        {
            UpdateSpriteFacing();
        }
    }

    /// <summary>
    /// Altera o estado atual da Máquina de Estados.
    /// </summary>
    public void ChangeState(IEnemyState newState)
    {
        if (newState == null || CurrentState == newState) return;

        if (CurrentState != null)
        {
            CurrentState.Exit();
        }

        CurrentState = newState;
        CurrentState.Enter();
    }

    /// <summary>
    /// Localiza o Transform do Player na cena por Tag ou pelo componente CharacterController2D.
    /// </summary>
    public void FindPlayerTarget()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            TargetPlayer = playerObj.transform;
            playerStats = playerObj.GetComponent<PlayerStats>();
            return;
        }

        // Fallback procurando pelo CharacterController2D do jogador
        CharacterController2D playerCC = FindFirstObjectByType<CharacterController2D>();
        if (playerCC != null)
        {
            TargetPlayer = playerCC.transform;
            playerStats = playerCC.GetComponent<PlayerStats>();
        }
    }

    /// <summary>
    /// Verifica se o Player existe e está VIVO.
    /// Retorna false se o jogador morreu (PlayerStats.IsDead ou GameState.Dead),
    /// fazendo a IA parar de perseguir e de bater no cadáver.
    /// </summary>
    public bool IsTargetAlive()
    {
        if (TargetPlayer == null) return false;

        // 1. Checa estado global do jogo (GameState.Dead)
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Dead)
        {
            return false;
        }

        // 2. Checa o PlayerStats no Player
        if (playerStats == null) playerStats = TargetPlayer.GetComponent<PlayerStats>();
        if (playerStats != null && playerStats.IsDead)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Verifica se há Linha de Visão (Line of Sight) direta com o Player sem obstáculos no caminho.
    /// Ignora o próprio colisor do inimigo, do Player e colisores Trigger.
    /// </summary>
    public bool HasLineOfSightToTarget()
    {
        if (!IsTargetAlive()) return false;

        Vector3 origin = transform.position + Vector3.up * 0.35f;
        Vector3 targetPos = TargetPlayer.position + Vector3.up * 0.35f;
        Vector3 direction = (targetPos - origin).normalized;
        float distance = Vector3.Distance(origin, targetPos);

        LayerMask mask = obstacleLayerMask != 0 ? obstacleLayerMask : LayerMask.GetMask("Obstacle");
        if (mask == 0) return true;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, mask);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;

            // Ignora o próprio inimigo e o Player
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform) || hit.collider.transform.root == transform.root) continue;
            if (hit.collider.gameObject == TargetPlayer.gameObject || hit.collider.transform.IsChildOf(TargetPlayer) || hit.collider.transform.root == TargetPlayer.root) continue;
            if (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyStats>() != null) continue;

            // Se atingir um obstáculo sólido que bloqueia a visão
            return false;
        }

        return true;
    }

    /// <summary>
    /// Move o inimigo até a posição do alvo respeitando o stoppingDistance para não empurrar o player.
    /// </summary>
    public void MoveToTarget(Vector3 targetPosition)
    {
        float dist = Vector3.Distance(transform.position, targetPosition);
        float stopDist = agent != null ? agent.stoppingDistance : 1.2f;

        // Se já estiver dentro da distância de parada, para o movimento para não empurrar o player
        if (dist <= stopDist)
        {
            StopMovement();
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
            UpdateAnimatorSpeed(agent.velocity.magnitude);
        }
        else
        {
            // Fallback de movimentação por NavMesh.CalculatePath
            bool hasPath = NavMesh.CalculatePath(transform.position, targetPosition, NavMesh.AllAreas, fallbackPath);
            if (hasPath && fallbackPath.corners != null && fallbackPath.corners.Length > 1)
            {
                Vector3 nextCorner = fallbackPath.corners[1];
                Vector3 moveDir = (nextCorner - transform.position).normalized;
                transform.position += moveDir * (movementSpeed * Time.deltaTime);
                UpdateAnimatorSpeed(movementSpeed);
            }
        }
    }

    /// <summary>
    /// Para o movimento do inimigo.
    /// </summary>
    public void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        UpdateAnimatorSpeed(0f);
    }

    public void UpdateAnimatorSpeed(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, speed);
        }
    }

    public void TriggerMeleeAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger(AttackMeleeHash);
        }
        ResetAttackCooldown();
    }

    public void TriggerRangedAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger(AttackRangedHash);
        }
        ResetAttackCooldown();
    }

    private bool hasExploded = false;

    public void ResetAttackCooldown()
    {
        attackCooldownTimer = attackCooldown;
    }

    private void HandleEnemyDied()
    {
        if (canExplode)
        {
            if (CurrentState != ExplodeState && !hasExploded)
            {
                ChangeState(ExplodeState);
            }
            return;
        }

        ChangeState(DeadState);
    }

    /// <summary>
    /// Executa a detonação da explosão no local atual (seja por pavio/ataque ou por morte/HP zerado).
    /// </summary>
    public void TriggerExplosionImmediate()
    {
        if (hasExploded) return;
        hasExploded = true;

        Vector3 center = transform.position;
        float radius = explosionRadius;
        float damage = explosionDamage;

        Debug.Log($"[EnemyAIController] 💥 '{gameObject.name}' EXPLODIU! Raio: {radius}m, Dano: {damage}");

        // 1. Oculta o sprite e elimina o inimigo no exato instante da explosão
        if (enemyStats != null)
        {
            enemyStats.KillImmediate(true);
        }
        else
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            Destroy(gameObject);
        }

        // 2. Dispara a explosão visual & Tremor de Câmera
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayExplosionVFX(center, new Color(1f, 0.3f, 0.05f, 1f), new Color(1f, 0.85f, 0.2f, 1f), radius);
            CombatVisualEffects.Instance.TriggerCameraShake(0.25f, 0.22f);
        }

        // 3. Detecção e Dano em Área (AoE)
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        System.Collections.Generic.HashSet<GameObject> affectedObjects = new System.Collections.Generic.HashSet<GameObject>();

        foreach (var col in hits)
        {
            if (col == null) continue;

            GameObject rootTarget = col.transform.root.gameObject;
            if (affectedObjects.Contains(rootTarget)) continue;

            IDamageable damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (damageable != null && !(damageable is EnemyStats))
            {
                affectedObjects.Add(rootTarget);
                Vector3 knockbackDir = (col.transform.position - center).normalized;
                damageable.TakeDamage(damage, knockbackDir);
            }
        }
    }

    [SerializeField] private bool spriteFacesRightByDefault = false;

    private void UpdateSpriteFacing()
    {
        if (spriteRenderer == null) return;

        Vector3 moveVelocity = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity : Vector3.zero;
        if (moveVelocity.sqrMagnitude > 0.05f)
        {
            spriteRenderer.flipX = spriteFacesRightByDefault ? (moveVelocity.x < 0f) : (moveVelocity.x > 0f);
        }
        else if (TargetPlayer != null)
        {
            float dx = TargetPlayer.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.1f)
            {
                spriteRenderer.flipX = spriteFacesRightByDefault ? (dx < 0f) : (dx > 0f);
            }
        }
    }

    private void OnValidate()
    {
        if (enemyConfig != null)
        {
            canUseMelee = enemyConfig.canUseMelee;
            canUseRanged = enemyConfig.canUseRanged;
            canExplode = enemyConfig.canExplode;
            explosionTriggerDistance = enemyConfig.explosionTriggerDistance;
            explosionRadius = enemyConfig.explosionRadius;
            explosionDamage = enemyConfig.explosionDamage;
            explosionFuseTime = enemyConfig.explosionFuseTime;
            detectionRadius = enemyConfig.detectionRadius;
            meleeRange = enemyConfig.meleeRange;
            rangedRange = enemyConfig.rangedRange;
            attackCooldown = enemyConfig.attackCooldown;
            movementSpeed = enemyConfig.moveSpeed;
            if (enemyConfig.projectilePrefab != null) projectilePrefab = enemyConfig.projectilePrefab;
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = movementSpeed;
            float stopDist = canExplode ? Mathf.Max(0.5f, explosionTriggerDistance * 0.7f) : (canUseMelee ? meleeRange * 0.75f : rangedRange * 0.7f);
            agent.stoppingDistance = Mathf.Max(0.5f, stopDist);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Se estiver selecionado no Editor, OnDrawGizmosSelected cuidará do desenho completo e destacado
        if (UnityEditor.Selection.activeGameObject == gameObject || (UnityEditor.Selection.activeTransform != null && UnityEditor.Selection.activeTransform.IsChildOf(transform)))
        {
            return;
        }

        // Desenho suave quando não selecionado (linhas sutis)
        DrawGizmoRanges(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        // Desenho destacado com preenchimento quando o inimigo estiver selecionado na Scene ou Hierarchy
        DrawGizmoRanges(true);
    }

    private void DrawGizmoRanges(bool isSelected)
    {
        Vector3 pos = transform.position;

        // 1. Raio de Detecção de Visão (Amarelo / Dourado)
        if (detectionRadius > 0f)
        {
            Color detectionColor = isSelected ? new Color(1f, 0.85f, 0.1f, 0.95f) : new Color(1f, 0.85f, 0.1f, 0.35f);
            UnityEditor.Handles.color = detectionColor;
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, detectionRadius);

            if (isSelected)
            {
                UnityEditor.Handles.color = new Color(1f, 0.85f, 0.1f, 0.05f);
                UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, detectionRadius);

                if (showGizmoLabels)
                {
                    DrawGizmoLabel(pos + Vector3.up * detectionRadius, $"👁️ Visão ({detectionRadius:F1}m)", new Color(1f, 0.9f, 0.3f));
                }
            }
        }

        // 2. Raio Ranged / Distância de Tiro (Azul Ciano)
        if (canUseRanged && rangedRange > 0f)
        {
            Color rangedColor = isSelected ? new Color(0f, 0.8f, 1f, 0.95f) : new Color(0f, 0.8f, 1f, 0.35f);
            UnityEditor.Handles.color = rangedColor;
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, rangedRange);

            if (isSelected)
            {
                UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.08f);
                UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, rangedRange);

                if (showGizmoLabels)
                {
                    DrawGizmoLabel(pos + Vector3.right * rangedRange, $"🏹 Ranged ({rangedRange:F1}m)", new Color(0.3f, 0.9f, 1f));
                }
            }
        }

        // 3. Raio Melee / Faca (Vermelho Coral)
        if (canUseMelee && meleeRange > 0f)
        {
            Color meleeColor = isSelected ? new Color(1f, 0.25f, 0.25f, 0.95f) : new Color(1f, 0.25f, 0.25f, 0.4f);
            UnityEditor.Handles.color = meleeColor;
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, meleeRange);

            if (isSelected)
            {
                UnityEditor.Handles.color = new Color(1f, 0.25f, 0.25f, 0.15f);
                UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, meleeRange);

                if (showGizmoLabels)
                {
                    DrawGizmoLabel(pos + Vector3.left * meleeRange, $"🗡️ Melee ({meleeRange:F1}m)", new Color(1f, 0.4f, 0.4f));
                }
            }
        }

        // 3.5. Raio de Explosão / Creeper (Laranja Flamejante)
        if (canExplode && explosionRadius > 0f)
        {
            Color explodeColor = isSelected ? new Color(1f, 0.4f, 0f, 0.95f) : new Color(1f, 0.4f, 0f, 0.45f);
            UnityEditor.Handles.color = explodeColor;
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, explosionRadius);

            if (isSelected)
            {
                UnityEditor.Handles.color = new Color(1f, 0.3f, 0f, 0.15f);
                UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, explosionRadius);

                if (showGizmoLabels)
                {
                    DrawGizmoLabel(pos + Vector3.down * explosionRadius, $"💥 Explosão ({explosionRadius:F1}m)", new Color(1f, 0.5f, 0.1f));
                }
            }
        }

        // 4. Distância de Parada do Agente (Verde Claro)
        if (isSelected && agent != null && agent.stoppingDistance > 0f)
        {
            UnityEditor.Handles.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, agent.stoppingDistance);

            if (showGizmoLabels)
            {
                DrawGizmoLabel(pos + Vector3.down * agent.stoppingDistance, $"🛑 Parada ({agent.stoppingDistance:F1}m)", new Color(0.4f, 1f, 0.5f));
            }
        }

        // 5. Linha de Visão em Tempo de Execução (Verde = Livre, Vermelho = Bloqueada por parede)
        if (TargetPlayer != null && Application.isPlaying)
        {
            bool hasLos = HasLineOfSightToTarget();
            Gizmos.color = hasLos ? new Color(0.2f, 1f, 0.2f, 0.9f) : new Color(1f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawLine(pos, TargetPlayer.position);
        }

        // 6. Caminho do NavMesh
        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.9f);
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }

        // 7. Nome do Estado Atual em Play Mode
        if (Application.isPlaying && showGizmoLabels)
        {
            string stateName = CurrentState != null ? CurrentState.GetType().Name.Replace("Enemy", "").Replace("State", "") : "NULL";
            DrawGizmoLabel(pos + Vector3.up * 1.5f, $"[IA: {stateName}]", Color.yellow);
        }
    }

    private void DrawGizmoLabel(Vector3 position, string text, Color textColor)
    {
        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
        style.normal.textColor = textColor;
        style.fontSize = 11;
        style.alignment = TextAnchor.MiddleCenter;

        UnityEditor.Handles.Label(position, text, style);
    }
#endif
}

