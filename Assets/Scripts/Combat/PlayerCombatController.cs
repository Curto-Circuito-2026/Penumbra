using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador principal de combate MOBA/Action RPG 2D.
/// Suporta alcance próprio por ataque/habilidade, navegação e pathfinding (NavMesh 2D),
/// cancelamento de comando via WASD, destaque visual do inimigo alvo e indicadores de alcance coloridos.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombatController : MonoBehaviour
{
    public enum PendingActionType { None, Melee, Ranged, AbilityQ, AbilityE, AbilityR }

    [Header("Mascara de Layer do Inimigo")]
    [SerializeField] private LayerMask enemyLayerMask;

    [Header("Ataque Básico Corpo a Corpo (LMB / Melee)")]
    [SerializeField] private float meleeRange = 2f;
    [SerializeField] private float meleeDamage = 20f;
    [SerializeField] private float meleeCooldown = 0.8f;

    [Header("Ataque Básico à Distância (RMB / Ranged)")]
    [SerializeField] private float rangedRange = 7f;
    [SerializeField] private float rangedDamage = 12f;
    [SerializeField] private float rangedCooldown = 1.2f;

    [Header("Velocidade de Movimento")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Habilidades Equipadas (ScriptableObjects)")]
    [SerializeField] private Ability slotQ;
    [SerializeField] private Ability slotE;
    [SerializeField] private Ability slotR;

    [Header("Retorno Visual & Indicadores")]
    [SerializeField] private LineRenderer rangeIndicator;
    [SerializeField] private TargetSelectionRing targetSelectionRing;
    [SerializeField] private MultiRangeIndicator multiRangeIndicator;
    [SerializeField] private int circleSegments = 40;

    [Header("Efeitos Sonoros de Ataque (SFX)")]
    [Tooltip("Áudio para o ataque básico corpo a corpo (Botão Esquerdo).")]
    [SerializeField] private AudioClip meleeAttackSFX;
    [Tooltip("Áudio para o ataque à distância (Botão Direito).")]
    [SerializeField] private AudioClip rangedAttackSFX;

    // Cores dos Indicadores de Alcance por Habilidade/Ataque
    private readonly Color meleeColor = new Color(0.9f, 0.96f, 1f, 0.8f);
    private readonly Color rangedColor = new Color(0.2f, 0.8f, 1f, 0.75f);
    private readonly Color qColor = new Color(0.2f, 1f, 0.5f, 0.75f);
    private readonly Color eColor = new Color(1f, 0.5f, 0.2f, 0.75f);
    private readonly Color rColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Header("Desbloqueio Progressivo de Slots de Habilidade")]
    [Tooltip("Slot Q sempre começa desbloqueado.")]
    [SerializeField] private bool isSlotEUnlocked = false;
    [SerializeField] private bool isSlotRUnlocked = false;

    // Checkpoint de Habilidades e Bênçãos por Fase
    private Ability[] checkpointEquippedAbilities = new Ability[3];
    private readonly List<AbilityBoonSO> confirmedBoons = new List<AbilityBoonSO>();
    private readonly List<AbilityBoonSO> stageSessionBoons = new List<AbilityBoonSO>();

    // Timers de Cooldown
    private float meleeCooldownTimer;
    private float rangedCooldownTimer;
    private float cooldownQ;
    private float cooldownE;
    private float cooldownR;

    // Estado da Perseguição, Ação Pendente e Hover da HUD
    private PendingActionType pendingAction = PendingActionType.None;
    private PendingActionType hudHoverAction = PendingActionType.None;
    private GameObject currentTarget;
    private Vector3 targetPoint;
    private NavMeshPath calculatedPath;

    private Camera mainCamera;
    private Rigidbody2D rb;
    private PlayerStats playerStats;
    private CharacterController2D characterController;

    // Input Actions do New Input System
    private InputAction moveAction;
    private InputAction lmbAction;
    private InputAction rmbAction;
    private InputAction keyQAction;
    private InputAction keyEAction;
    private InputAction keyRAction;

    // Eventos para atualização dinâmica da HUD
    public event Action<float, float, float, float> OnBasicCooldownsUpdated; // (meleeRem, meleeMax, rangedRem, rangedMax)
    public event Action<int, float, float> OnAbilityCooldownUpdated;          // (slotIndex, remaining, max)
    public event Action<float, float> OnUltimateChargeUpdated;               // (current, max)
    public event Action<Ability, Ability, Ability> OnEquippedAbilitiesChanged; // (Q, E, R)
    public event Action<int, bool> OnSlotUnlockStateChanged;                 // (slotIndex, isUnlocked)


    /// <summary>
    /// Propriedade que indica se o jogador está em modo de perseguição de combate ativo.
    /// Usada para que outros scripts de controle não sobrescrevam a velocidade de movimento.
    /// </summary>
    public bool IsPursuingTarget => pendingAction != PendingActionType.None;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerStats = GetComponent<PlayerStats>();
        characterController = GetComponent<CharacterController2D>();
        mainCamera = Camera.main;
        calculatedPath = new NavMeshPath();
        checkpointEquippedAbilities = new Ability[] { slotQ, slotE, slotR };

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
        {
            enemyLayerMask = (1 << enemyLayer) | LayerMask.GetMask("Default");
        }
        else
        {
            enemyLayerMask = ~0; // Fallback
        }

        // Setup do LineRenderer para o indicador de alcance
        if (rangeIndicator == null)
        {
            rangeIndicator = GetComponent<LineRenderer>();
            if (rangeIndicator == null)
            {
                rangeIndicator = gameObject.AddComponent<LineRenderer>();
            }
        }

        ConfigureRangeIndicator();

        // Setup do Anel de Destaque no Inimigo
        if (targetSelectionRing == null)
        {
            targetSelectionRing = UnityEngine.Object.FindAnyObjectByType<TargetSelectionRing>();
            if (targetSelectionRing == null)
            {
                GameObject ringObj = new GameObject("TargetSelectionRing");
                targetSelectionRing = ringObj.AddComponent<TargetSelectionRing>();
            }
        }

        // Setup do MultiRangeIndicator para exibir todos os alcances
        if (multiRangeIndicator == null)
        {
            multiRangeIndicator = GetComponent<MultiRangeIndicator>();
            if (multiRangeIndicator == null)
            {
                multiRangeIndicator = gameObject.AddComponent<MultiRangeIndicator>();
            }
        }

        // Auto-carregamento dos áudios de ataque (suporta Editor e WebGL via Resources)
        if (meleeAttackSFX == null)
        {
            meleeAttackSFX = Resources.Load<AudioClip>("Audio/ataque basico")
                          ?? Resources.Load<AudioClip>("ataque basico");
#if UNITY_EDITOR
            if (meleeAttackSFX == null)
            {
                meleeAttackSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ataque basico.mp3");
            }
#endif
        }

        if (rangedAttackSFX == null)
        {
            rangedAttackSFX = Resources.Load<AudioClip>("Audio/lançando")
                          ?? Resources.Load<AudioClip>("lançando");
#if UNITY_EDITOR
            if (rangedAttackSFX == null)
            {
                rangedAttackSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/lançando.mp3");
            }
#endif
        }

        // Configuração dos Inputs via New Input System
        moveAction = new InputAction("MoveInput", expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");

        lmbAction = new InputAction("LMB", binding: "<Mouse>/leftButton");
        rmbAction = new InputAction("RMB", binding: "<Mouse>/rightButton");
        keyQAction = new InputAction("KeyQ", binding: "<Keyboard>/q");
        keyEAction = new InputAction("KeyE", binding: "<Keyboard>/e");
        keyRAction = new InputAction("KeyR", binding: "<Keyboard>/r");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lmbAction.Enable();
        rmbAction.Enable();
        keyQAction.Enable();
        keyEAction.Enable();
        keyRAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lmbAction.Disable();
        rmbAction.Disable();
        keyQAction.Disable();
        keyEAction.Disable();
        keyRAction.Disable();
    }

    private void Start()
    {
        OnEquippedAbilitiesChanged?.Invoke(slotQ, slotE, slotR);
    }

    private void Update()
    {
        // Se o jogo não estiver no estado de jogando (pausado/diálogo), ignora entradas
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove)
        {
            CancelAction();
            HideRangeIndicator();
            if (targetSelectionRing != null) targetSelectionRing.Hide();
            return;
        }

        UpdateCooldowns();
        HandleMouseHoverAndVisuals();
        HandleInput();
    }

    private void FixedUpdate()
    {
        /*
        // [COMENTADO A PEDIDO] Auto-caminhada até o alcance do inimigo
        if (pendingAction != PendingActionType.None)
        {
            Vector3 destination = currentTarget != null ? currentTarget.transform.position : targetPoint;
            float requiredRange = GetRequiredRange(pendingAction);
            float dist = Vector2.Distance(transform.position, destination);

            if (dist <= requiredRange)
            {
                StopMovement();
                ExecutePendingAction();
            }
            else
            {
                NavigateAlongPath(destination);
            }
        }
        */
    }

    /// <summary>
    /// Calcula o caminho usando NavMesh.CalculatePath e move o personagem pelo próximo waypoint contornando obstáculos.
    /// </summary>
    private void NavigateAlongPath(Vector3 destination)
    {
        bool hasPath = NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, calculatedPath);

        if (hasPath && calculatedPath.corners != null && calculatedPath.corners.Length > 1)
        {
            // O próximo ponto a seguir no caminho
            Vector3 nextCorner = calculatedPath.corners[1];
            Vector2 moveDir = ((Vector2)nextCorner - rb.position).normalized;
            rb.linearVelocity = moveDir * moveSpeed;
        }
        else
        {
            // Fallback: Movimento direto caso o NavMesh não esteja disponível no ponto
            Vector2 directDir = ((Vector2)destination - rb.position).normalized;
            rb.linearVelocity = directDir * moveSpeed;
        }
    }

    /// <summary>
    /// Para o movimento do Rigidbody2D.
    /// </summary>
    private void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Processa comandos de clique do mouse, WASD e atalhos de habilidades Q, E, R.
    /// Ataques e habilidades são disparados na direção do ponteiro do mouse em linha reta.
    /// </summary>
    private void HandleInput()
    {
        // Se o personagem estiver dando dash, impede atirar ou usar habilidades até o dash terminar
        if (characterController == null) characterController = GetComponent<CharacterController2D>();
        if (characterController != null && characterController.IsDashing)
        {
            return;
        }

        Vector3 mouseWorldPos = GetMouseWorldPosition();

        // Clique do Mouse Esquerdo (Ataque Melee Direcional)
        if (lmbAction.WasPressedThisFrame())
        {
            PerformMeleeAttack(mouseWorldPos);
        }

        // Clique do Mouse Direito (Ataque Ranged Direcional)
        if (rmbAction.WasPressedThisFrame())
        {
            PerformRangedAttack(mouseWorldPos);
        }

        // Tecla Q - Habilidade 1 Direcional (Sempre Desbloqueado)
        if (keyQAction.WasPressedThisFrame())
        {
            TryTargetOrCastAbility(0, slotQ, ref cooldownQ, mouseWorldPos);
        }

        // Tecla E - Habilidade 2 Direcional (Requer Slot E Desbloqueado)
        if (keyEAction.WasPressedThisFrame())
        {
            if (IsSlotUnlocked(1))
            {
                TryTargetOrCastAbility(1, slotE, ref cooldownE, mouseWorldPos);
            }
            else
            {
                Debug.Log("[PlayerCombatController] Slot [E] bloqueado! Desbloqueie com o Curupira.");
            }
        }

        // Tecla R - Habilidade 3 Direcional (Requer Slot R Desbloqueado)
        if (keyRAction.WasPressedThisFrame())
        {
            if (IsSlotUnlocked(2))
            {
                TryTargetOrCastAbility(2, slotR, ref cooldownR, mouseWorldPos);
            }
            else
            {
                Debug.Log("[PlayerCombatController] Slot [R] bloqueado! Desbloqueie com o Curupira.");
            }
        }
    }

    /// <summary>
    /// Atualiza retorno visual de hover sobre inimigos, alcance individual e exibição de TODOS os alcances.
    /// </summary>
    private void HandleMouseHoverAndVisuals()
    {
        // 1. Destaque Visual do Inimigo (Apenas se houver um alvo ativo travado, sem indicador no hover)
        if (currentTarget != null && targetSelectionRing != null)
        {
            Color highlightColor = GetColorForAction(pendingAction);
            targetSelectionRing.ShowOnTarget(currentTarget.transform, highlightColor);
        }
        else if (targetSelectionRing != null)
        {
            targetSelectionRing.Hide();
        }

        // 2. Se houver hover na HUD sobre um slot de habilidade específico
        if (hudHoverAction != PendingActionType.None)
        {
            float range = GetRequiredRange(hudHoverAction);
            if (range > 0f)
            {
                Color col = GetColorForAction(hudHoverAction);
                ShowRangeIndicator(range, col);
            }
            else
            {
                HideRangeIndicator();
            }
            return;
        }

        // 3. Indicadores de Alcance Visual (apenas quando uma ação/habilidade estiver pendente para conjurar)
        if (pendingAction != PendingActionType.None)
        {
            float displayRange = GetRequiredRange(pendingAction);
            Color rangeCol = GetColorForAction(pendingAction);
            ShowRangeIndicator(displayRange, rangeCol);
        }
        else
        {
            HideRangeIndicator();
        }
    }

    /// <summary>
    /// Exibe todos os círculos de alcance de todas as habilidades e ataques equipados simultaneamente.
    /// </summary>
    public void ShowAllRanges()
    {
        if (multiRangeIndicator == null) return;

        List<MultiRangeIndicator.RangeCircleData> list = new List<MultiRangeIndicator.RangeCircleData>();
        list.Add(new MultiRangeIndicator.RangeCircleData(meleeRange, meleeColor, "Melee"));
        list.Add(new MultiRangeIndicator.RangeCircleData(rangedRange, rangedColor, "Ranged"));

        if (slotQ != null) list.Add(new MultiRangeIndicator.RangeCircleData(slotQ.Range, qColor, "Q: " + slotQ.AbilityName));
        if (slotE != null) list.Add(new MultiRangeIndicator.RangeCircleData(slotE.Range, eColor, "E: " + slotE.AbilityName));
        if (slotR != null) list.Add(new MultiRangeIndicator.RangeCircleData(slotR.Range, rColor, "R: " + slotR.AbilityName));

        multiRangeIndicator.DisplayRanges(transform.position, list);
    }

    /// <summary>
    /// Chamado pela HUD para exibir o alcance de um slot específico ao passar o cursor do mouse sobre o ícone.
    /// </summary>
    public void SetHudHoverAction(PendingActionType action)
    {
        hudHoverAction = action;
    }

    /// <summary>
    /// Limpa o alcance de hover da HUD.
    /// </summary>
    public void ClearHudHoverAction()
    {
        hudHoverAction = PendingActionType.None;
    }

    private void PerformMeleeAttack(Vector3 mouseWorldPos)
    {
        if (characterController == null) characterController = GetComponent<CharacterController2D>();
        if (characterController != null && characterController.IsDashing) return;

        if (meleeCooldownTimer > 0f) return;

        Vector3 dir = (mouseWorldPos - transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;

        // Dispara a animação de ataque Melee virando para a direção do golpe (4 direções)
        if (characterController != null)
        {
            characterController.TriggerMeleeAnimation(dir);
        }

        // Toca o efeito sonoro de ataque básico (Botão Esquerdo)
        if (meleeAttackSFX != null && AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(meleeAttackSFX);
        }

        // Raycast da posição do jogador em direção ao mouse até o alcance Melee
        RaycastHit2D[] meleeHits = Physics2D.RaycastAll(transform.position, dir, meleeRange, enemyLayerMask);
        GameObject targetEnemy = null;
        IDamageable targetDmg = null;

        foreach (var h in meleeHits)
        {
            if (IsValidEnemyTarget(h.collider, out IDamageable dmg))
            {
                targetEnemy = h.collider.gameObject;
                targetDmg = dmg;
                break;
            }
        }

        Debug.Log($"[PlayerCombatController] Ataque Melee Direcional. Primeiro inimigo no caminho: {(targetEnemy != null ? targetEnemy.name : "Nenhum")}");

        // Retorno Visual: Arco de corte Melee
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayMeleeSlash(transform.position, dir);
        }

        if (targetDmg != null)
        {
            targetDmg.TakeDamage(meleeDamage, dir);
        }
        else
        {
            // Procura inimigos por OverlapCircle no arco curto em frente
            Collider2D[] hitCols = Physics2D.OverlapCircleAll(transform.position + dir * (meleeRange * 0.5f), meleeRange * 0.7f, enemyLayerMask);
            foreach (var col in hitCols)
            {
                if (IsValidEnemyTarget(col, out IDamageable hitDmg))
                {
                    hitDmg.TakeDamage(meleeDamage, dir);
                    break;
                }
            }
        }

        meleeCooldownTimer = meleeCooldown;
    }

    private void PerformRangedAttack(Vector3 mouseWorldPos)
    {
        if (characterController == null) characterController = GetComponent<CharacterController2D>();
        if (characterController != null && characterController.IsDashing) return;

        if (rangedCooldownTimer > 0f) return;

        Vector3 dir = (mouseWorldPos - transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;

        // Dispara a animação de ataque à distância virando para a direção do disparo (4 direções)
        if (characterController != null)
        {
            characterController.TriggerRangedAnimation(dir);
        }

        // Toca o efeito sonoro de ataque à distância (Botão Direito)
        if (rangedAttackSFX != null && AudioController.Instance != null)
        {
            AudioController.Instance.PlaySFX(rangedAttackSFX);
        }

        StartCoroutine(ExecuteDelayedRangedAttack(dir, mouseWorldPos, 0.28f));
        rangedCooldownTimer = rangedCooldown;
    }

    private IEnumerator ExecuteDelayedRangedAttack(Vector3 dir, Vector3 mouseWorldPos, float delay)
    {
        yield return new WaitForSeconds(delay);

        Vector3 spawnPos = GetRangedSpawnPosition(dir);
        float mouseDist = Vector3.Distance(spawnPos, mouseWorldPos);
        float castDist = Mathf.Min(mouseDist, rangedRange);
        if (castDist < 0.5f) castDist = rangedRange;

        // Raycast da posição da mão na direção do mouse até o alcance máximo
        RaycastHit2D[] rangedHits = Physics2D.RaycastAll(spawnPos, dir, rangedRange, enemyLayerMask);
        GameObject targetEnemy = null;
        IDamageable targetDmg = null;
        Vector3 impactPos = spawnPos + dir * castDist;

        foreach (var h in rangedHits)
        {
            if (IsValidEnemyTarget(h.collider, out IDamageable dmg))
            {
                targetEnemy = h.collider.gameObject;
                targetDmg = dmg;
                impactPos = h.point;
                break;
            }
        }

        Debug.Log($"[PlayerCombatController] Disparo Ranged Direcional (Lança arremessada). Primeiro inimigo: {(targetEnemy != null ? targetEnemy.name : "Nenhum")}");

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayRangedProjectile(spawnPos, impactPos, () =>
            {
                if (targetDmg != null)
                {
                    targetDmg.TakeDamage(rangedDamage, dir);
                }
                else
                {
                    Collider2D[] hitCols = Physics2D.OverlapCircleAll(impactPos, 1.2f, enemyLayerMask);
                    foreach (var col in hitCols)
                    {
                        if (IsValidEnemyTarget(col, out IDamageable hitDmg))
                        {
                            hitDmg.TakeDamage(rangedDamage, dir);
                            break;
                        }
                    }
                }
            });
        }
        else if (targetDmg != null)
        {
            targetDmg.TakeDamage(rangedDamage, dir);
        }
    }

    /// <summary>
    /// Valida se o colisor atingido pertence a um inimigo ou entidade com IDamageable, ignorando explicitamente zonas, delimitadores e triggers sem vida.
    /// </summary>
    private bool IsValidEnemyTarget(Collider2D col, out IDamageable damageable)
    {
        damageable = null;
        if (col == null) return false;

        // Ignora o próprio player e seus filhos/pais
        if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.transform.root == transform.root) return false;
        if (col.CompareTag("Player") || col.GetComponentInParent<CharacterController2D>() != null || col.GetComponentInParent<PlayerStats>() != null) return false;

        // Ignora explicitamente Fightzone, triggers de arena, boundaries e áreas de transição
        string colName = col.gameObject.name;
        if (colName.IndexOf("Fightzone", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Fighzone", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Trigger", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Zone", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Bounds", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            damageable = col.GetComponent<IDamageable>();
            return damageable != null && !(damageable is CharacterController2D) && !(damageable is PlayerStats);
        }

        // Tenta obter IDamageable no próprio objeto ou no pai (ex: partes de Boss ou inimigos compostos)
        damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();

        // Se for um Trigger e não tiver IDamageable, ignora
        if (col.isTrigger && damageable == null)
        {
            return false;
        }

        // Se não tiver IDamageable e não estiver na layer Enemy, ignora
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (damageable == null && (enemyLayer == -1 || col.gameObject.layer != enemyLayer))
        {
            return false;
        }

        // Não aplica dano ao Player
        if (damageable is CharacterController2D || damageable is PlayerStats)
        {
            return false;
        }

        return true;
    }

    private Vector3 GetRangedSpawnPosition(Vector3 dir)
    {
        // Elevação do tronco/ombro da Naia a partir dos pés
        Vector3 baseOffset = new Vector3(0f, 0.55f, 0f);

        // Deslocamento da mão conforme a direção do arremesso
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            // Lateral (Direita ou Esquerda)
            baseOffset += new Vector3(Mathf.Sign(dir.x) * 0.35f, 0f, 0f);
        }
        else if (dir.y > 0)
        {
            // Cima
            baseOffset += new Vector3(0.12f, 0.15f, 0f);
        }
        else
        {
            // Baixo
            baseOffset += new Vector3(-0.1f, -0.1f, 0f);
        }

        return transform.position + baseOffset;
    }

    private void TryTargetOrCastAbility(int slotIndex, Ability ability, ref float cooldownTimer, Vector3 mouseWorldPos)
    {
        if (characterController == null) characterController = GetComponent<CharacterController2D>();
        if (characterController != null && characterController.IsDashing) return;

        if (ability == null) return;
        if (cooldownTimer > 0f)
        {
            Debug.Log($"[PlayerCombatController] Habilidade {ability.AbilityName} em recarga ({cooldownTimer:F1}s restante)!");
            return;
        }
        Vector3 dir = (mouseWorldPos - transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;

        float mouseDist = Vector3.Distance(transform.position, mouseWorldPos);
        float castDist = Mathf.Min(mouseDist, ability.Range);

        // A habilidade é lançada exatamente no ponto do mouse (limitado pelo alcance máximo)
        Vector3 targetPos = transform.position + dir * castDist;

        // Dispara a animação de Casting na direção do feitiço (4 direções)
        if (characterController != null)
        {
            characterController.TriggerCastAnimation(dir);
        }

        cooldownTimer = ability.Cooldown;
        OnAbilityCooldownUpdated?.Invoke(slotIndex, cooldownTimer, ability.Cooldown);

        StartCoroutine(ExecuteDelayedAbilityCast(ability, targetPos, 0.22f));
    }

    private IEnumerator ExecuteDelayedAbilityCast(Ability ability, Vector3 targetPos, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ability != null)
        {
            ability.Cast(gameObject, targetPos, null);
        }
    }


    private IEnumerator ExecuteDelayedUltimateCast(Vector3 targetPos, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (slotR != null)
        {
            slotR.Cast(gameObject, targetPos, null);
        }
    }



    #region Dynamic Ability Loadout API
    /// <summary>
    /// Retorna se o slot de habilidade especificado está desbloqueado (0 = Q [sempre true], 1 = E, 2 = R).
    /// </summary>
    public bool IsSlotUnlocked(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return true; // Slot Q sempre começa desbloqueado
            case 1: return isSlotEUnlocked;
            case 2: return isSlotRUnlocked;
            default: return false;
        }
    }

    /// <summary>
    /// Desbloqueia o próximo slot de habilidade disponível na ordem: Slot [E] (1ª compra) e depois Slot [R] (2ª compra).
    /// Retorna true se um slot foi desbloqueado com sucesso; false se todos os slots já foram liberados.
    /// </summary>
    public bool UnlockNextAbilitySlot(out string unlockedSlotName)
    {
        if (!isSlotEUnlocked)
        {
            isSlotEUnlocked = true;
            unlockedSlotName = "Slot [E]";
            OnSlotUnlockStateChanged?.Invoke(1, true);
            Debug.Log("[PlayerCombatController] Slot [E] de habilidade foi DESBLOQUEADO com sucesso!");
            return true;
        }
        else if (!isSlotRUnlocked)
        {
            isSlotRUnlocked = true;
            unlockedSlotName = "Slot [R]";
            OnSlotUnlockStateChanged?.Invoke(2, true);
            Debug.Log("[PlayerCombatController] Slot [R] de habilidade foi DESBLOQUEADO com sucesso!");
            return true;
        }

        unlockedSlotName = "";
        return false;
    }

    /// <summary>
    /// Desbloqueia diretamente um slot específico (1 = E, 2 = R).
    /// </summary>
    public void UnlockAbilitySlot(int slotIndex)
    {
        if (slotIndex == 1 && !isSlotEUnlocked)
        {
            isSlotEUnlocked = true;
            OnSlotUnlockStateChanged?.Invoke(1, true);
        }
        else if (slotIndex == 2 && !isSlotRUnlocked)
        {
            isSlotRUnlocked = true;
            OnSlotUnlockStateChanged?.Invoke(2, true);
        }
    }

    /// <summary>
    /// Equipa uma nova habilidade no slot especificado (0 = Q, 1 = E, 2 = R/Ultimate).
    /// Dispara atualização automática da HUD de combate.
    /// </summary>
    public void EquipAbility(int slotIndex, Ability newAbility)
    {
        if (!IsSlotUnlocked(slotIndex))
        {
            Debug.LogWarning($"[PlayerCombatController] Não é possível equipar no Slot {slotIndex} porque ele está bloqueado!");
            return;
        }

        switch (slotIndex)
        {
            case 0:
                slotQ = newAbility;
                cooldownQ = 0f;
                OnAbilityCooldownUpdated?.Invoke(0, 0f, newAbility != null ? newAbility.Cooldown : 1f);
                break;
            case 1:
                slotE = newAbility;
                cooldownE = 0f;
                OnAbilityCooldownUpdated?.Invoke(1, 0f, newAbility != null ? newAbility.Cooldown : 1f);
                break;
            case 2:
                slotR = newAbility;
                cooldownR = 0f;
                OnAbilityCooldownUpdated?.Invoke(2, 0f, newAbility != null ? newAbility.Cooldown : 1f);
                break;
        }

        OnEquippedAbilitiesChanged?.Invoke(slotQ, slotE, slotR);
        Debug.Log($"[PlayerCombatController] Habilidade '{(newAbility != null ? newAbility.AbilityName : "Vazia")}' equipada no Slot {slotIndex}");
    }

    /// <summary>
    /// Desequipa a habilidade do slot especificado (deixa o slot vazio/null).
    /// </summary>
    public void UnequipAbility(int slotIndex)
    {
        EquipAbility(slotIndex, null);
    }

    /// <summary>
    /// Retorna a habilidade atualmente equipada no slot especificado.
    /// </summary>
    public Ability GetEquippedAbility(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return slotQ;
            case 1: return slotE;
            case 2: return slotR;
            default: return null;
        }
    }
    #endregion

    #region Checkpoint e Rastreamento de Habilidades / Bênçãos de Fase
    /// <summary>
    /// Verifica se a habilidade já está equipada em qualquer um dos slots Q, E ou R.
    /// </summary>
    public bool HasAbilityEquipped(Ability ability)
    {
        if (ability == null) return false;
        return (slotQ == ability || slotE == ability || slotR == ability);
    }

    /// <summary>
    /// Verifica se a bênção (ou habilidade vinculada) já foi adquirida/está ativa.
    /// </summary>
    public bool HasBoonActive(AbilityBoonSO boon)
    {
        if (boon == null) return false;
        if (boon.GrantedAbility != null && HasAbilityEquipped(boon.GrantedAbility)) return true;
        return confirmedBoons.Contains(boon) || stageSessionBoons.Contains(boon);
    }

    /// <summary>
    /// Registra uma bênção/habilidade comprada na fase atual.
    /// Se o jogador morrer nesta mesma fase, ela será revertida.
    /// </summary>
    public void RecordStageBoonAcquisition(AbilityBoonSO boon, int slotIndex = -1)
    {
        if (boon == null) return;

        if (!stageSessionBoons.Contains(boon))
        {
            stageSessionBoons.Add(boon);
        }

        Debug.Log($"[PlayerCombatController] Bênção de Fase '{boon.BoonName}' registrada na sessão temporária da fase.");
    }

    /// <summary>
    /// Salva o Checkpoint da Fase (chamado ao passar de fase ou ao iniciar a run).
    /// Torna permanentes para a run todas as habilidades e bênçãos compradas até agora.
    /// </summary>
    public void SaveStageCheckpoint()
    {
        foreach (var b in stageSessionBoons)
        {
            if (b != null && !confirmedBoons.Contains(b))
            {
                confirmedBoons.Add(b);
            }
        }
        stageSessionBoons.Clear();

        checkpointEquippedAbilities[0] = slotQ;
        checkpointEquippedAbilities[1] = slotE;
        checkpointEquippedAbilities[2] = slotR;

        Debug.Log($"[PlayerCombatController] Checkpoint de Fase Salvo com Sucesso! Q: {(slotQ != null ? slotQ.AbilityName : "null")}, E: {(slotE != null ? slotE.AbilityName : "null")}, R: {(slotR != null ? slotR.AbilityName : "null")}, Bênçãos Confirmadas: {confirmedBoons.Count}");
    }

    /// <summary>
    /// Reverte todas as habilidades e bênçãos compradas na fase atual (chamado ao morrer na fase).
    /// As estrelas e moedas gastas NÃO são recuperadas.
    /// </summary>
    public void RevertStageSessionPurchases()
    {
        if (stageSessionBoons.Count > 0)
        {
            Debug.Log($"[PlayerCombatController] Revertendo {stageSessionBoons.Count} bênçãos adquiridas nesta fase após a morte...");
            foreach (var boon in stageSessionBoons)
            {
                if (boon != null)
                {
                    boon.RemoveBoon(gameObject);
                }
            }
            stageSessionBoons.Clear();
        }

        // Restaura as habilidades equipadas para o estado do início da fase
        slotQ = checkpointEquippedAbilities[0];
        slotE = checkpointEquippedAbilities[1];
        slotR = checkpointEquippedAbilities[2];

        cooldownQ = 0f;
        cooldownE = 0f;
        cooldownR = 0f;

        OnEquippedAbilitiesChanged?.Invoke(slotQ, slotE, slotR);
        Debug.Log("[PlayerCombatController] Habilidades e buffs da fase revertidos. Habilidades salvas no checkpoint mantidas.");
    }

    /// <summary>
    /// Limpa todas as habilidades e bênçãos acumuladas ao encerrar/reiniciar uma nova Run a partir do Hub.
    /// Garante que o jogador renasça com os slots de habilidades zerados/iniciais.
    /// </summary>
    public void ResetAllRunBoonsAndAbilities()
    {
        Debug.Log("[PlayerCombatController] Resetando todas as habilidades e bênçãos para uma nova Run...");

        // Remove efeitos de todas as bênçãos acumuladas
        if (stageSessionBoons != null)
        {
            foreach (var boon in stageSessionBoons)
            {
                if (boon != null) boon.RemoveBoon(gameObject);
            }
            stageSessionBoons.Clear();
        }

        if (confirmedBoons != null)
        {
            foreach (var boon in confirmedBoons)
            {
                if (boon != null) boon.RemoveBoon(gameObject);
            }
            confirmedBoons.Clear();
        }

        // Destrói invocação de companheiro aliado (Besta-Fera) se ainda existir na cena
        AllyCompanionAI[] allies = UnityEngine.Object.FindObjectsByType<AllyCompanionAI>(FindObjectsSortMode.None);
        foreach (var ally in allies)
        {
            if (ally != null) UnityEngine.Object.Destroy(ally.gameObject);
        }

        // Reseta os slots Q, E, R
        slotQ = null;
        slotE = null;
        slotR = null;

        cooldownQ = 0f;
        cooldownE = 0f;
        cooldownR = 0f;

        checkpointEquippedAbilities = new Ability[3];

        OnEquippedAbilitiesChanged?.Invoke(null, null, null);
        OnAbilityCooldownUpdated?.Invoke(0, 0f, 1f);
        OnAbilityCooldownUpdated?.Invoke(1, 0f, 1f);
        OnAbilityCooldownUpdated?.Invoke(2, 0f, 1f);
    }
    #endregion

    /// <summary>
    /// Retorna as informações de Tooltip (Nome, Descrição, Recarga, Custo de Mana e Ícone) da habilidade ou ataque básico correspondente.
    /// </summary>
    public void GetSkillTooltipData(PendingActionType action, out string name, out string description, out float cooldown, out float manaCost, out Sprite icon)
    {
        switch (action)
        {
            case PendingActionType.Melee:
                name = "Ataque Corpo a Corpo (Botão Esquerdo)";
                description = $"Golpe físico rápido de curto alcance. Causa {meleeDamage:F0} de dano";
                cooldown = meleeCooldown;
                manaCost = 0f;
                icon = null;
                break;

            case PendingActionType.Ranged:
                name = "Ataque à Distância (Botão Direito)";
                description = $"Disparo de projétil à distância. Causa {rangedDamage:F0} de dano";
                cooldown = rangedCooldown;
                manaCost = 0f;
                icon = null;
                break;

            case PendingActionType.AbilityQ:
                GetAbilityInfo(slotQ, "Q", out name, out description, out cooldown, out manaCost, out icon);
                break;

            case PendingActionType.AbilityE:
                GetAbilityInfo(slotE, "E", out name, out description, out cooldown, out manaCost, out icon);
                break;

            case PendingActionType.AbilityR:
                GetAbilityInfo(slotR, "R", out name, out description, out cooldown, out manaCost, out icon);
                break;

            default:
                name = "Habilidade";
                description = "";
                cooldown = 0f;
                manaCost = 0f;
                icon = null;
                break;
        }
    }

    private void GetAbilityInfo(Ability ability, string slotKey, out string name, out string description, out float cooldown, out float manaCost, out Sprite icon)
    {
        if (ability != null)
        {
            name = $"{ability.AbilityName} [{slotKey}]";
            description = !string.IsNullOrEmpty(ability.Description) ? ability.Description : "Sem descrição.";
            cooldown = ability.Cooldown;
            manaCost = ability.ManaCost;
            icon = ability.Icon;
        }
        else
        {
            name = $"Slot [{slotKey}] Vazio";
            description = "Nenhuma habilidade equipada neste slot.";
            cooldown = 0f;
            manaCost = 0f;
            icon = null;
        }
    }

    public void CancelAction()
    {
        pendingAction = PendingActionType.None;
        currentTarget = null;
        StopMovement();
        if (targetSelectionRing != null) targetSelectionRing.Hide();
    }

    private float GetRequiredRange(PendingActionType action)
    {
        switch (action)
        {
            case PendingActionType.Melee: return meleeRange;
            case PendingActionType.Ranged: return rangedRange;
            case PendingActionType.AbilityQ: return slotQ != null ? slotQ.Range : 0f;
            case PendingActionType.AbilityE: return slotE != null ? slotE.Range : 0f;
            case PendingActionType.AbilityR: return slotR != null ? slotR.Range : 0f;
            default: return 0f;
        }
    }

    private Color GetColorForAction(PendingActionType action)
    {
        switch (action)
        {
            case PendingActionType.Melee: return meleeColor;
            case PendingActionType.Ranged: return rangedColor;
            case PendingActionType.AbilityQ: return qColor;
            case PendingActionType.AbilityE: return eColor;
            case PendingActionType.AbilityR: return rColor;
            default: return meleeColor;
        }
    }

    private void UpdateCooldowns()
    {
        if (meleeCooldownTimer > 0f)
        {
            meleeCooldownTimer -= Time.deltaTime;
            if (meleeCooldownTimer < 0f) meleeCooldownTimer = 0f;
        }

        if (rangedCooldownTimer > 0f)
        {
            rangedCooldownTimer -= Time.deltaTime;
            if (rangedCooldownTimer < 0f) rangedCooldownTimer = 0f;
        }

        OnBasicCooldownsUpdated?.Invoke(meleeCooldownTimer, meleeCooldown, rangedCooldownTimer, rangedCooldown);

        if (cooldownQ > 0f)
        {
            cooldownQ -= Time.deltaTime;
            if (cooldownQ < 0f) cooldownQ = 0f;
            OnAbilityCooldownUpdated?.Invoke(0, cooldownQ, slotQ != null ? slotQ.Cooldown : 1f);
        }

        if (cooldownE > 0f)
        {
            cooldownE -= Time.deltaTime;
            if (cooldownE < 0f) cooldownE = 0f;
            OnAbilityCooldownUpdated?.Invoke(1, cooldownE, slotE != null ? slotE.Cooldown : 1f);
        }

        if (cooldownR > 0f)
        {
            cooldownR -= Time.deltaTime;
            if (cooldownR < 0f) cooldownR = 0f;
            OnAbilityCooldownUpdated?.Invoke(2, cooldownR, slotR != null ? slotR.Cooldown : 1f);
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || Mouse.current == null) return transform.position;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, -mainCamera.transform.position.z));
        worldPos.z = 0f;
        return worldPos;
    }

    #region LineRenderer Range Indicator
    private void ConfigureRangeIndicator()
    {
        if (rangeIndicator == null) return;

        rangeIndicator.useWorldSpace = true;
        rangeIndicator.positionCount = circleSegments + 1;
        rangeIndicator.startWidth = 0.06f;
        rangeIndicator.endWidth = 0.06f;
        rangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
        rangeIndicator.startColor = meleeColor;
        rangeIndicator.endColor = meleeColor;
        rangeIndicator.enabled = false;
    }

    private void ShowRangeIndicator(float radius, Color color)
    {
        if (rangeIndicator == null) return;

        rangeIndicator.enabled = true;
        rangeIndicator.startColor = color;
        rangeIndicator.endColor = color;
        float angleStep = 360f / circleSegments;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            rangeIndicator.SetPosition(i, pos);
        }
    }

    private void HideRangeIndicator()
    {
        if (rangeIndicator != null && rangeIndicator.enabled)
        {
            rangeIndicator.enabled = false;
        }
    }

    /// <summary>
    /// Aumenta o dano de ataque corpo a corpo e à distância.
    /// </summary>
    public void IncreaseDamage(float amount)
    {
        if (amount <= 0f) return;
        meleeDamage += amount;
        rangedDamage += amount;
        Debug.Log($"[PlayerCombatController] Dano aumentado em +{amount}! (Melee: {meleeDamage}, Ranged: {rangedDamage})");
    }
    #endregion
}


