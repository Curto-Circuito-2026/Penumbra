using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum CharacterState
{
    Idle,
    Walking,
    Running,
    Dashing,
    Dead
}

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{
    public static CharacterController2D Instance { get; private set; }

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashSpeed = 18f;
    [Tooltip("Tempo de espera (segundos) após o término de um Dash antes de permitir outro.")]
    [SerializeField] private float dashCooldown = 0.15f;

    [Header("Visual Colors (Testing)")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color dashColor = Color.cyan;

    private InputAction moveAction;
    private InputAction dashAction;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private PlayerStats playerStats;

    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.down;
    private bool isDashing;
    private float dashCooldownTimer;
    private float speedBuffMultiplier = 1f;
    private Coroutine speedBuffCoroutine;

    public CharacterState CurrentState { get; private set; } = CharacterState.Idle;
    public bool IsDashing => isDashing || CurrentState == CharacterState.Dashing;
    public float SpeedBuffMultiplier => speedBuffMultiplier;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int LastMoveX = Animator.StringToHash("LastMoveX");
    private static readonly int LastMoveY = Animator.StringToHash("LastMoveY");
    private static readonly int DashTrigger = Animator.StringToHash("Dash");
    private static readonly int MeleeTrigger = Animator.StringToHash("Melee");
    private static readonly int RangedTrigger = Animator.StringToHash("Ranged");
    private static readonly int CastTrigger = Animator.StringToHash("Cast");

    public float CurrentStamina => 100f;
    public float MaxStamina => 100f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerStats = GetComponent<PlayerStats>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (spriteRenderer != null) spriteRenderer.color = normalColor;

        //setando o input action para o movimento do personagem pq nao sei usar o newinput no editorr
        moveAction = new InputAction("Move", expectedControlType: "Vector2");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Gamepad>/dpad/up").With("Down", "<Gamepad>/dpad/down")
            .With("Left", "<Gamepad>/dpad/left").With("Right", "<Gamepad>/dpad/right");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Gamepad>/leftStick/up").With("Down", "<Gamepad>/leftStick/down")
            .With("Left", "<Gamepad>/leftStick/left").With("Right", "<Gamepad>/leftStick/right");

        //dash
        dashAction = new InputAction("Dash", binding: "<Keyboard>/space");
        dashAction.AddBinding("<Gamepad>/buttonSouth");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        dashAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        dashAction?.Disable();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
    }

    private void Update()
    {
        UpdateCharacterState();

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (isDashing) return;

        // Se o jogador não puder se mover (diálogo, pausa ou morte), cancela o movimento
        bool canMove = GameStateManager.Instance == null || GameStateManager.Instance.CanPlayerMove;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            canMove = false;
        }

        if (!canMove)
        {
            moveInput = Vector2.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            UpdateAnimator();
            return;
        }

        HandleInput();
        UpdateAnimator();
    }

    private void UpdateCharacterState()
    {
        if (playerStats == null) playerStats = GetComponent<PlayerStats>();

        if (playerStats != null && playerStats.IsDead)
        {
            CurrentState = CharacterState.Dead;
        }
        else if (isDashing)
        {
            CurrentState = CharacterState.Dashing;
        }
        else if (moveInput != Vector2.zero)
        {
            CurrentState = CharacterState.Walking;
        }
        else
        {
            CurrentState = CharacterState.Idle;
        }
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        bool canMove = GameStateManager.Instance == null || GameStateManager.Instance.CanPlayerMove;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            canMove = false;
        }

        if (!canMove)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Aplica o movimento com a velocidade de caminhada
        if (moveInput != Vector2.zero)
        {
            float speed = walkSpeed * speedBuffMultiplier;
            rb.linearVelocity = moveInput * speed;
        }
        else
        {
            PlayerCombatController combat = GetComponent<PlayerCombatController>();
            if (combat == null || !combat.IsPursuingTarget)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void ApplySpeedBuff(float multiplier, float duration)
    {
        if (speedBuffCoroutine != null) StopCoroutine(speedBuffCoroutine);
        speedBuffCoroutine = StartCoroutine(SpeedBuffRoutine(multiplier, duration));
    }

    private IEnumerator SpeedBuffRoutine(float multiplier, float duration)
    {
        speedBuffMultiplier = Mathf.Max(1f, multiplier);
        yield return new WaitForSeconds(duration);
        speedBuffMultiplier = 1f;
        speedBuffCoroutine = null;
    }

    private void HandleInput()
    {
        Vector2 rawInput = moveAction.ReadValue<Vector2>();
        moveInput = rawInput.sqrMagnitude > 0.01f ? rawInput.normalized : Vector2.zero;

        if (moveInput != Vector2.zero)
        {
            lastMoveDirection = moveInput;

            if (animator != null)
            {
                animator.SetFloat(LastMoveX, lastMoveDirection.x);
                animator.SetFloat(LastMoveY, lastMoveDirection.y);
            }
        }

        if (dashAction.WasPressedThisFrame() && !isDashing && dashCooldownTimer <= 0f)
        {
            StartCoroutine(PerformDash(moveInput));
        }
    }

    private void HandleStamina()
    {
        // Stamina removida do jogo
    }

    private IEnumerator PerformDash(Vector2 inputDirection)
    {
        isDashing = true;
        CurrentState = CharacterState.Dashing;

        Vector2 dashDir = (inputDirection != Vector2.zero ? inputDirection : lastMoveDirection).normalized;
        if (dashDir == Vector2.zero) dashDir = Vector2.down;

        if (animator != null)
        {
            animator.SetFloat(LastMoveX, dashDir.x);
            animator.SetFloat(LastMoveY, dashDir.y);
            animator.SetTrigger(DashTrigger);
        }

        Vector2 startPos = rb.position;
        float actualDashDistance = dashDistance;

        // Validação física de obstáculos: detecta paredes, bordas do mapa e colliders sólidos
        Collider2D playerCol = GetComponent<Collider2D>();
        float skinWidth = 0.08f;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false; // Ignora triggers
        filter.useLayerMask = true;
        int playerLayer = gameObject.layer;
        filter.layerMask = ~(1 << playerLayer); // Todas as camadas sólidas exceto o próprio player

        RaycastHit2D[] hits = new RaycastHit2D[10];
        int hitCount = playerCol != null ? playerCol.Cast(dashDir, filter, hits, dashDistance) : rb.Cast(dashDir, filter, hits, dashDistance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject)
            {
                // Se for um obstáculo sólido, limita a distância do dash antes do impacto
                float safeDist = Mathf.Max(0f, hit.distance - skinWidth);
                if (safeDist < actualDashDistance)
                {
                    actualDashDistance = safeDist;
                }
            }
        }

        Vector2 targetPos = startPos + dashDir * actualDashDistance;
        float travelDuration = actualDashDistance > 0.01f ? (actualDashDistance / dashSpeed) : 0f;
        float elapsedTime = 0f;

        rb.linearVelocity = Vector2.zero;

        if (travelDuration > 0f)
        {
            while (elapsedTime < travelDuration)
            {
                elapsedTime += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / travelDuration);
                rb.MovePosition(Vector2.Lerp(startPos, targetPos, t));
                yield return new WaitForFixedUpdate();
            }
        }

        rb.MovePosition(targetPos);
        isDashing = false;
        dashCooldownTimer = dashCooldown;
        UpdateCharacterState();
    }

    private void UpdateVisuals()
    {
        if (spriteRenderer == null) return;

        if (isDashing)
        {
            spriteRenderer.color = dashColor;
        }
        else
        {
            spriteRenderer.color = normalColor;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(MoveX, moveInput.x);
        animator.SetFloat(MoveY, moveInput.y);
        animator.SetBool(IsMoving, moveInput != Vector2.zero);
    }

    /// <summary>
    /// Aumenta permanentemente a velocidade de movimento.
    /// </summary>
    public void IncreaseMovementSpeed(float amount)
    {
        if (amount <= 0f) return;
        walkSpeed += amount;
    }

    /// <summary>
    /// Define a direção em que o personagem está olhando e sincroniza os parâmetros do Animator.
    /// </summary>
    public void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = direction.normalized;
            if (animator != null)
            {
                animator.SetFloat(LastMoveX, lastMoveDirection.x);
                animator.SetFloat(LastMoveY, lastMoveDirection.y);
            }
        }
    }

    /// <summary>
    /// Dispara a animação de ataque Melee virando a personagem para a direção do golpe.
    /// Bloqueia se o personagem estiver no meio de um Dash.
    /// </summary>
    public void TriggerMeleeAnimation(Vector2 attackDirection)
    {
        if (IsDashing) return;

        SetFacingDirection(attackDirection);
        if (animator != null)
        {
            animator.SetTrigger(MeleeTrigger);
        }
    }

    /// <summary>
    /// Dispara a animação de ataque à distância (Ranged) virando a personagem para a direção do disparo.
    /// Bloqueia se o personagem estiver no meio de um Dash.
    /// </summary>
    public void TriggerRangedAnimation(Vector2 attackDirection)
    {
        if (IsDashing) return;

        SetFacingDirection(attackDirection);
        if (animator != null)
        {
            animator.SetTrigger(RangedTrigger);
        }
    }

    /// <summary>
    /// Dispara a animação de conjuração de magia (Cast) virando a personagem para a direção visada.
    /// Bloqueia se o personagem estiver no meio de um Dash.
    /// </summary>
    public void TriggerCastAnimation(Vector2 castDirection)
    {
        if (IsDashing) return;

        SetFacingDirection(castDirection);
        if (animator != null)
        {
            animator.SetTrigger(CastTrigger);
        }
    }
}