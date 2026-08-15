using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{
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

    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.down;
    private bool isDashing;
    private bool isRunning;
    private float staminaRegenTimer;

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
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

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

    private void Update()
    {
        if (isDashing) return;

        HandleInput();
        HandleStamina();
        UpdateVisuals();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        float speed = isRunning ? runSpeed : walkSpeed;
        rb.linearVelocity = moveInput * speed;
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
}