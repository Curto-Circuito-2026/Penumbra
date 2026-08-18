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
    [SerializeField] private Ability slotR; // Ultimate

    [Header("Carga da Ultimate")]
    [SerializeField] private float ultimateCharge = 0f;
    [SerializeField] private float maxUltimateCharge = 100f;
    [SerializeField] private float chargePerHit = 20f;

    [Header("Retorno Visual & Indicadores")]
    [SerializeField] private LineRenderer rangeIndicator;
    [SerializeField] private TargetSelectionRing targetSelectionRing;
    [SerializeField] private MultiRangeIndicator multiRangeIndicator;
    [SerializeField] private int circleSegments = 40;

    // Cores dos Indicadores de Alcance por Habilidade/Ataque
    private readonly Color meleeColor = new Color(0.9f, 0.96f, 1f, 0.8f);
    private readonly Color rangedColor = new Color(0.2f, 0.8f, 1f, 0.75f);
    private readonly Color qColor = new Color(0.2f, 1f, 0.5f, 0.75f);
    private readonly Color eColor = new Color(1f, 0.5f, 0.2f, 0.75f);
    private readonly Color rColor = new Color(1f, 0.85f, 0.2f, 0.9f);

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
    private InputAction showAllRangesAction;

    // Eventos para atualização dinâmica da HUD
    public event Action<float, float, float, float> OnBasicCooldownsUpdated; // (meleeRem, meleeMax, rangedRem, rangedMax)
    public event Action<int, float, float> OnAbilityCooldownUpdated;          // (slotIndex, remaining, max)
    public event Action<float, float> OnUltimateChargeUpdated;               // (current, max)
    public event Action<Ability, Ability, Ability> OnEquippedAbilitiesChanged; // (Q, E, R)

    public float UltimateCharge => ultimateCharge;
    public float MaxUltimateCharge => maxUltimateCharge;
    public bool IsUltimateReady => ultimateCharge >= maxUltimateCharge;

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

        if (enemyLayerMask == 0)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                enemyLayerMask = 1 << enemyLayer;
            }
            else
            {
                enemyLayerMask = ~0; // Fallback
            }
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

        // Tecla Shift / Tab / C para exibir todos os alcances
        showAllRangesAction = new InputAction("ShowAllRanges", binding: "<Keyboard>/leftShift");
        showAllRangesAction.AddBinding("<Keyboard>/tab");
        showAllRangesAction.AddBinding("<Keyboard>/c");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lmbAction.Enable();
        rmbAction.Enable();
        keyQAction.Enable();
        keyEAction.Enable();
        keyRAction.Enable();
        showAllRangesAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lmbAction.Disable();
        rmbAction.Disable();
        keyQAction.Disable();
        keyEAction.Disable();
        keyRAction.Disable();
        showAllRangesAction.Disable();
    }

    private void Start()
    {
        OnEquippedAbilitiesChanged?.Invoke(slotQ, slotE, slotR);
        OnUltimateChargeUpdated?.Invoke(ultimateCharge, maxUltimateCharge);
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

        // Tecla Q - Habilidade 1 Direcional
        if (keyQAction.WasPressedThisFrame())
        {
            TryTargetOrCastAbility(0, slotQ, ref cooldownQ, mouseWorldPos);
        }

        // Tecla E - Habilidade 2 Direcional
        if (keyEAction.WasPressedThisFrame())
        {
            TryTargetOrCastAbility(1, slotE, ref cooldownE, mouseWorldPos);
        }

        // Tecla R - Ultimate Direcional
        if (keyRAction.WasPressedThisFrame())
        {
            TryCastUltimate(mouseWorldPos);
        }
    }

    /// <summary>
    /// Atualiza retorno visual de hover sobre inimigos, alcance individual e exibição de TODOS os alcances.
    /// </summary>
    private void HandleMouseHoverAndVisuals()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero, 0f, enemyLayerMask);

        // 1. Destaque Visual do Inimigo focado / atacado
        GameObject hoveredOrTargeted = hit.collider != null ? hit.collider.gameObject : currentTarget;

        if (hoveredOrTargeted != null && targetSelectionRing != null)
        {
            Color highlightColor = GetColorForAction(pendingAction);
            targetSelectionRing.ShowOnTarget(hoveredOrTargeted.transform, highlightColor);
        }
        else if (targetSelectionRing != null)
        {
            targetSelectionRing.Hide();
        }

        // 2. Se a tecla Shift / Tab / C estiver pressionada, exibe TODOS os alcances simultaneamente
        if (showAllRangesAction != null && showAllRangesAction.IsPressed())
        {
            ShowAllRanges();
            HideRangeIndicator();
            return;
        }
        else if (multiRangeIndicator != null)
        {
            multiRangeIndicator.HideAll();
        }

        // 3. Se houver hover na HUD sobre um slot de habilidade específico
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

        // 4. Indicadores de Alcance Visual padrão
        if (hit.collider != null)
        {
            float displayRange = (pendingAction == PendingActionType.Ranged) ? rangedRange : (pendingAction != PendingActionType.None ? GetRequiredRange(pendingAction) : meleeRange);
            Color rangeCol = GetColorForAction(pendingAction != PendingActionType.None ? pendingAction : PendingActionType.Melee);
            ShowRangeIndicator(displayRange, rangeCol);
        }
        else if (pendingAction != PendingActionType.None)
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

        // Raycast da posição do jogador em direção ao mouse até o alcance Melee
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, meleeRange, enemyLayerMask);
        GameObject targetEnemy = hit.collider != null ? hit.collider.gameObject : null;

        Debug.Log($"[PlayerCombatController] Ataque Melee Direcional. Primeiro inimigo no caminho: {(targetEnemy != null ? targetEnemy.name : "Nenhum")}");

        // Retorno Visual: Arco de corte Melee
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayMeleeSlash(transform.position, dir);
        }

        if (targetEnemy != null && targetEnemy.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(meleeDamage, dir);
            AddUltimateCharge(chargePerHit);
        }
        else
        {
            // Procura inimigos por OverlapCircle no arco curto em frente
            Collider2D hitCol = Physics2D.OverlapCircle(transform.position + dir * (meleeRange * 0.5f), meleeRange * 0.6f, enemyLayerMask);
            if (hitCol != null && hitCol.TryGetComponent(out IDamageable hitDmg))
            {
                hitDmg.TakeDamage(meleeDamage, dir);
                AddUltimateCharge(chargePerHit);
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

        float mouseDist = Vector3.Distance(transform.position, mouseWorldPos);
        float castDist = Mathf.Min(mouseDist, rangedRange);
        if (castDist < 0.5f) castDist = rangedRange;

        // Raycast da posição do jogador na direção do mouse até o alcance máximo
        // Encontra o PRIMEIRO inimigo que estiver na frente entre o jogador e a mira!
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, rangedRange, enemyLayerMask);
        GameObject targetEnemy = hit.collider != null ? hit.collider.gameObject : null;
        Vector3 impactPos = hit.collider != null ? (Vector3)hit.point : transform.position + dir * castDist;

        Debug.Log($"[PlayerCombatController] Disparo Ranged Direcional. Primeiro inimigo no caminho: {(targetEnemy != null ? targetEnemy.name : "Nenhum")}");

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayRangedProjectile(transform.position, impactPos, () =>
            {
                if (targetEnemy != null && targetEnemy.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(rangedDamage, dir);
                    AddUltimateCharge(chargePerHit);
                }
                else
                {
                    Collider2D hitCol = Physics2D.OverlapCircle(impactPos, 0.8f, enemyLayerMask);
                    if (hitCol != null && hitCol.TryGetComponent(out IDamageable hitDmg))
                    {
                        hitDmg.TakeDamage(rangedDamage, dir);
                        AddUltimateCharge(chargePerHit);
                    }
                }
            });
        }
        else
        {
            if (targetEnemy != null && targetEnemy.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(rangedDamage, dir);
                AddUltimateCharge(chargePerHit);
            }
        }

        rangedCooldownTimer = rangedCooldown;
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

        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
        if (playerStats != null && !playerStats.HasEnoughMana(ability.ManaCost))
        {
            Debug.Log($"[PlayerCombatController] Mana insuficiente para usar {ability.AbilityName}! Custo: {ability.ManaCost}, Atual: {playerStats.CurrentMana:F0}");
            return;
        }

        Vector3 dir = (mouseWorldPos - transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;

        float mouseDist = Vector3.Distance(transform.position, mouseWorldPos);
        float castDist = Mathf.Min(mouseDist, ability.Range);
        if (castDist < 0.5f) castDist = ability.Range;

        // Raycast da posição do jogador na direção do mouse até o alcance máximo da habilidade
        // Se houver um inimigo na frente entre o jogador e o ponto mirado, o Raycast acerta o primeiro!
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, ability.Range, enemyLayerMask);
        GameObject targetEnemy = hit.collider != null ? hit.collider.gameObject : null;
        Vector3 targetPos = hit.collider != null ? (Vector3)hit.point : transform.position + dir * castDist;

        bool success = ability.Cast(gameObject, targetPos, targetEnemy);
        if (success)
        {
            if (playerStats != null) playerStats.UseMana(ability.ManaCost);
            cooldownTimer = ability.Cooldown;
            OnAbilityCooldownUpdated?.Invoke(slotIndex, cooldownTimer, ability.Cooldown);
        }
    }

    private void TryCastUltimate(Vector3 mouseWorldPos)
    {
        if (characterController == null) characterController = GetComponent<CharacterController2D>();
        if (characterController != null && characterController.IsDashing) return;

        if (slotR == null) return;
        if (cooldownR > 0f)
        {
            Debug.Log($"[PlayerCombatController] Ultimate {slotR.AbilityName} em recarga ({cooldownR:F1}s restante)!");
            return;
        }
        if (!IsUltimateReady)
        {
            Debug.Log($"[PlayerCombatController] Ultimate não está carregada! Carga atual: {ultimateCharge}/{maxUltimateCharge}");
            return;
        }

        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
        if (playerStats != null && !playerStats.HasEnoughMana(slotR.ManaCost))
        {
            Debug.Log($"[PlayerCombatController] Mana insuficiente para usar Ultimate {slotR.AbilityName}! Custo: {slotR.ManaCost}, Atual: {playerStats.CurrentMana:F0}");
            return;
        }

        Vector3 dir = (mouseWorldPos - transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;

        float mouseDist = Vector3.Distance(transform.position, mouseWorldPos);
        float castDist = Mathf.Min(mouseDist, slotR.Range);
        if (castDist < 0.5f) castDist = slotR.Range;

        // Raycast na direção do mouse até o alcance da Ultimate
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, slotR.Range, enemyLayerMask);
        GameObject targetEnemy = hit.collider != null ? hit.collider.gameObject : null;
        Vector3 targetPos = hit.collider != null ? (Vector3)hit.point : transform.position + dir * castDist;

        bool success = slotR.Cast(gameObject, targetPos, targetEnemy);
        if (success)
        {
            if (playerStats != null) playerStats.UseMana(slotR.ManaCost);
            ultimateCharge = 0f;
            cooldownR = slotR.Cooldown;
            OnUltimateChargeUpdated?.Invoke(ultimateCharge, maxUltimateCharge);
            OnAbilityCooldownUpdated?.Invoke(2, cooldownR, slotR.Cooldown);
        }
    }

    public void AddUltimateCharge(float amount)
    {
        ultimateCharge = Mathf.Clamp(ultimateCharge + amount, 0f, maxUltimateCharge);
        OnUltimateChargeUpdated?.Invoke(ultimateCharge, maxUltimateCharge);
    }

    #region Dynamic Ability Loadout API
    /// <summary>
    /// Equipa uma nova habilidade no slot especificado (0 = Q, 1 = E, 2 = R/Ultimate).
    /// Dispara atualização automática da HUD de combate.
    /// </summary>
    public void EquipAbility(int slotIndex, Ability newAbility)
    {
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

    /// <summary>
    /// Retorna as informações de Tooltip (Nome, Descrição, Recarga, Custo de Mana e Ícone) da habilidade ou ataque básico correspondente.
    /// </summary>
    public void GetSkillTooltipData(PendingActionType action, out string name, out string description, out float cooldown, out float manaCost, out Sprite icon)
    {
        switch (action)
        {
            case PendingActionType.Melee:
                name = "Ataque Corpo a Corpo (Botão Esquerdo)";
                description = $"Golpe físico rápido de curto alcance. Causa {meleeDamage:F0} de dano e gera +{chargePerHit:F0}% de carga para a Ultimate.";
                cooldown = meleeCooldown;
                manaCost = 0f;
                icon = null;
                break;

            case PendingActionType.Ranged:
                name = "Ataque à Distância (Botão Direito)";
                description = $"Disparo de projétil à distância. Causa {rangedDamage:F0} de dano e gera +{chargePerHit:F0}% de carga para a Ultimate.";
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
                GetAbilityInfo(slotR, "R - Ultimate", out name, out description, out cooldown, out manaCost, out icon);
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


