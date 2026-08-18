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
    [SerializeField] private float runSpeed = 8.5f;

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashSpeed = 18f;

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;
    [SerializeField] private float runStaminaCostPerSecond = 20f;
    [SerializeField] private float dashStaminaCost = 25f;
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float staminaRegenDelay = 1f;

    [Header("Visual Colors (Testing)")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color runColor = Color.yellow;
    [SerializeField] private Color dashColor = Color.cyan;

    private InputAction moveAction;
    private InputAction runAction;
    private InputAction dashAction;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private PlayerStats playerStats;

    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.down;
    private bool isDashing;
    private bool isRunning;
    private float staminaRegenTimer;

    public CharacterState CurrentState { get; private set; } = CharacterState.Idle;
    public bool IsDashing => isDashing || CurrentState == CharacterState.Dashing;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int LastMoveX = Animator.StringToHash("LastMoveX");
    private static readonly int LastMoveY = Animator.StringToHash("LastMoveY");
    private static readonly int DashTrigger = Animator.StringToHash("Dash");

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

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

        runAction = new InputAction("Run", binding: "<Keyboard>/leftShift");
        runAction.AddBinding("<Gamepad>/buttonEast");

        dashAction = new InputAction("Dash", binding: "<Keyboard>/space");
        dashAction.AddBinding("<Gamepad>/buttonSouth");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        runAction.Enable();
        dashAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        runAction.Disable();
        dashAction.Disable();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameObject spawn = GameObject.Find("SpawnPoint");
        if (spawn) {this.transform.position = spawn.transform.position;}
    }

    private void Update()
    {
        UpdateCharacterState();

        if (isDashing) return;

        // Verifica se o jogador pode se movimentar de acordo com o estado do jogo (GameStateManager)
        bool canMove = GameStateManager.Instance == null || GameStateManager.Instance.CanPlayerMove;

        // Caso alternativo se o DialogueManager estiver ativo diretamente
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            canMove = false;
        }

        if (!canMove)
        {
            moveInput = Vector2.zero;
            isRunning = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            UpdateAnimator();
            return;
        }

        HandleInput();
        HandleStamina();
        //UpdateVisuals();
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
        else if (isRunning)
        {
            CurrentState = CharacterState.Running;
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

        // Se houver input manual WASD, aplica o movimento WASD
        if (moveInput != Vector2.zero)
        {
            float speed = isRunning ? runSpeed : walkSpeed;
            rb.linearVelocity = moveInput * speed;
        }
        else
        {
            // Se NÃO houver input WASD, só zera a velocidade se não houver perseguição/pathfinding de combate ativo
            PlayerCombatController combat = GetComponent<PlayerCombatController>();
            if (combat == null || !combat.IsPursuingTarget)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
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

        bool runPressed = runAction.IsPressed();
        isRunning = runPressed && moveInput != Vector2.zero && currentStamina > 0f;

        if (dashAction.WasPressedThisFrame() && currentStamina >= dashStaminaCost)
        {
            StartCoroutine(PerformDash(moveInput));
        }
    }

    private void HandleStamina()
    {
        if (isRunning && moveInput != Vector2.zero)
        {
            currentStamina -= runStaminaCostPerSecond * Time.deltaTime;
            staminaRegenTimer = staminaRegenDelay;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isRunning = false;
            }
        }
        else if (!isDashing)
        {
            if (staminaRegenTimer > 0f)
            {
                staminaRegenTimer -= Time.deltaTime;
            }
            else if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina > maxStamina) currentStamina = maxStamina;
            }
        }
    }

    private IEnumerator PerformDash(Vector2 inputDirection)
    {
        isDashing = true;
        CurrentState = CharacterState.Dashing;
        currentStamina -= dashStaminaCost;
        staminaRegenTimer = staminaRegenDelay;

        Vector2 dashDir = inputDirection != Vector2.zero ? inputDirection : lastMoveDirection;

        // Dispara o Trigger e atualiza a direção do dash no Animator
        if (animator != null)
        {
            animator.SetFloat(LastMoveX, dashDir.x);
            animator.SetFloat(LastMoveY, dashDir.y);
            animator.SetTrigger(DashTrigger);
        }

        Vector2 startPos = rb.position;
        Vector2 targetPos = startPos + dashDir * dashDistance;
        float travelDuration = dashDistance / dashSpeed;
        float elapsedTime = 0f;

        rb.linearVelocity = Vector2.zero;

        while (elapsedTime < travelDuration)
        {
            elapsedTime += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / travelDuration);
            rb.MovePosition(Vector2.Lerp(startPos, targetPos, t));
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPos);
        isDashing = false;
        UpdateCharacterState();
    }

    private void UpdateVisuals()
    {
        if (spriteRenderer == null) return;

        if (isDashing)
        {
            spriteRenderer.color = dashColor;
        }
        else if (isRunning)
        {
            spriteRenderer.color = runColor;
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
    /// Aumenta permanentemente a velocidade de caminhada e corrida.
    /// </summary>
    public void IncreaseMovementSpeed(float amount)
    {
        if (amount <= 0f) return;
        walkSpeed += amount;
        runSpeed += amount;
    }
}