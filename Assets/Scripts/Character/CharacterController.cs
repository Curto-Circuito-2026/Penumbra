using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{
    public enum MovementMode { GridBased, Continuous }

    [Header("Movement Settings")]
    [SerializeField] private MovementMode mode = MovementMode.GridBased;
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8.5f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Grid Settings")]
    [SerializeField] private float tileSize = 1f;

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private int gridDashTiles = 2;

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
    private bool isMovingGrid;
    private bool isDashing;
    private bool isRunning;
    private float staminaRegenTimer;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int LastMoveX = Animator.StringToHash("LastMoveX");
    private static readonly int LastMoveY = Animator.StringToHash("LastMoveY");

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

        if (mode == MovementMode.Continuous)
        {
            float speed = isRunning ? runSpeed : walkSpeed;
            rb.MovePosition(rb.position + moveInput * (speed * Time.fixedDeltaTime));
        }
    }

    private void HandleInput()
    {
        if (mode == MovementMode.GridBased && isMovingGrid) return;

        Vector2 rawInput = moveAction.ReadValue<Vector2>();
        Vector2 discreteInput = new Vector2(Mathf.Round(rawInput.x), Mathf.Round(rawInput.y));

        moveInput = discreteInput.normalized;

        if (moveInput != Vector2.zero)
        {
            lastMoveDirection = moveInput;

            if (animator != null)
            {
                animator.SetFloat(LastMoveX, moveInput.x);
                animator.SetFloat(LastMoveY, moveInput.y);
            }
        }

        bool runPressed = runAction.IsPressed();
        isRunning = runPressed && moveInput != Vector2.zero && currentStamina > 0f;

        if (dashAction.WasPressedThisFrame() && currentStamina >= dashStaminaCost)
        {
            StartCoroutine(PerformDash(discreteInput));
            return;
        }

        if (mode == MovementMode.GridBased && discreteInput != Vector2.zero && !isMovingGrid)
        {
            float speed = isRunning ? runSpeed : walkSpeed;
            Vector3 targetPos = transform.position + new Vector3(discreteInput.x, discreteInput.y, 0f) * tileSize;

            if (!IsObstacle(targetPos))
            {
                StartCoroutine(MoveToGridPosition(targetPos, speed));
            }
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

        Vector2 dashDir = inputDirection != Vector2.zero ? inputDirection.normalized : lastMoveDirection;

        if (mode == MovementMode.Continuous)
        {
            Vector2 startPos = rb.position;
            Vector2 targetPos = startPos + dashDir * dashDistance;
            float travelDuration = dashDistance / dashSpeed;
            float elapsedTime = 0f;

            while (elapsedTime < travelDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / travelDuration);
                
                Vector2 nextPos = Vector2.Lerp(startPos, targetPos, t);

                if (IsObstacle(nextPos)) break;

                rb.MovePosition(nextPos);
                yield return null;
            }
        }
        else
        {
            Vector3 targetPos = transform.position;
            for (int i = 1; i <= gridDashTiles; i++)
            {
                Vector3 nextCheck = transform.position + new Vector3(dashDir.x, dashDir.y, 0f) * (tileSize * i);
                if (IsObstacle(nextCheck)) break;
                targetPos = nextCheck;
            }

            yield return MoveToGridPosition(targetPos, dashSpeed);
        }

        isDashing = false;
    }

    private IEnumerator MoveToGridPosition(Vector3 targetPos, float speed)
    {
        isMovingGrid = true;

        while (Vector3.Distance(transform.position, targetPos) > 0.001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
        isMovingGrid = false;
    }

    private bool IsObstacle(Vector3 targetPos)
    {
        if (obstacleLayer == 0) return false;
        return Physics2D.OverlapCircle(targetPos, 0.2f, obstacleLayer) != null;
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

        bool moving = mode == MovementMode.GridBased ? isMovingGrid : moveInput != Vector2.zero;

        animator.SetFloat(MoveX, moveInput.x);
        animator.SetFloat(MoveY, moveInput.y);
        animator.SetBool(IsMoving, moving);
    }
}