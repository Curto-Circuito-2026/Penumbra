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

    [Header("Alcances e Detecção")]
    [Tooltip("Raio de detecção de visão do Player.")]
    [SerializeField] private float detectionRadius = 10f;

    [Tooltip("Alcance de ataque Melee (Faca).")]
    [SerializeField] private float meleeRange = 1.8f;

    [Tooltip("Alcance de ataque Ranged (Distância de tiro/segurança).")]
    [SerializeField] private float rangedRange = 7.5f;

    [Tooltip("Tempo de recarga entre ataques em segundos.")]
    [SerializeField] private float attackCooldown = 1.5f;

    [Tooltip("Velocidade de movimentação no NavMesh.")]
    [SerializeField] private float movementSpeed = 3.5f;

    [Header("Detecção de Visão e Paredes")]
    [Tooltip("Máscara das camadas que bloqueiam a visão do inimigo (Obstáculos e Paredes).")]
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("Prefabs de Ataque")]
    [Tooltip("Prefab do Projétil em linha reta (mesmo do Player).")]
    [SerializeField] private GameObject projectilePrefab;

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
    public float DetectionRadius => detectionRadius;
    public float MeleeRange => meleeRange;
    public float RangedRange => rangedRange;
    public float AttackCooldown => attackCooldown;
    public bool IsAttackOnCooldown => attackCooldownTimer > 0f;
    public GameObject ProjectilePrefab => projectilePrefab;
    public EnemyCombatController CombatController => combatController;

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
            obstacleLayerMask = LayerMask.GetMask("Obstacle", "Default");
        }

        // Aplica EnemyConfigSO se fornecido
        if (enemyConfig != null)
        {
            canUseMelee = enemyConfig.canUseMelee;
            canUseRanged = enemyConfig.canUseRanged;
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
        if (baseMovementSpeed <= 0f) baseMovementSpeed = movementSpeed;

        // Garante o alinhamento com o NavMesh no início
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
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
        if (enemyStats != null && enemyStats.IsDead) return;

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
        UpdateSpriteFacing();
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
    /// Localiza o Transform do Player na cena.
    /// </summary>
    public void FindPlayerTarget()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            TargetPlayer = playerObj.transform;
            playerStats = playerObj.GetComponent<PlayerStats>();
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

        Vector3 origin = transform.position;
        Vector3 targetPos = TargetPlayer.position;
        Vector3 direction = (targetPos - origin).normalized;
        float distance = Vector3.Distance(origin, targetPos);

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, obstacleLayerMask);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;

            // Ignora o próprio inimigo e o próprio Player
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.gameObject == TargetPlayer.gameObject || hit.collider.transform.IsChildOf(TargetPlayer)) continue;

            // Se atingir qualquer outro colisor (parede/obstáculo):
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

    public void ResetAttackCooldown()
    {
        attackCooldownTimer = attackCooldown;
    }

    private void HandleEnemyDied()
    {
        ChangeState(DeadState);
    }

    private void UpdateSpriteFacing()
    {
        if (spriteRenderer == null) return;

        Vector3 moveVelocity = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity : Vector3.zero;
        if (moveVelocity.sqrMagnitude > 0.05f)
        {
            spriteRenderer.flipX = moveVelocity.x < 0f;
        }
        else if (TargetPlayer != null)
        {
            float dx = TargetPlayer.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.1f)
            {
                spriteRenderer.flipX = dx < 0f;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 1. Raio de Detecção (Círculo Amarelo)
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, detectionRadius);

        // 2. Raio Melee (Círculo Vermelho)
        if (canUseMelee)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, meleeRange);
        }

        // 3. Raio Ranged (Círculo Azul Ciano)
        if (canUseRanged)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, rangedRange);
        }

        // 4. Linha de Visão (Verde = Livre, Vermelho = Bloqueada)
        if (TargetPlayer != null)
        {
            bool hasLos = HasLineOfSightToTarget();
            Gizmos.color = hasLos ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, TargetPlayer.position);
        }

        // 5. Caminho do NavMesh
        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
        {
            Gizmos.color = Color.magenta;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }

        // 6. Texto com Nome do Estado Atual sobre a cabeça do Inimigo
        string stateName = CurrentState != null ? CurrentState.GetType().Name.Replace("Enemy", "").Replace("State", "") : "NULL";
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"[IA: {stateName}]", style);
    }
#endif
}

