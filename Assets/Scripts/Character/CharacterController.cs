using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{
    public enum MovementMode { GridBased, Continuous }

    [Header("Movement Settings")]
    [SerializeField] private MovementMode mode = MovementMode.GridBased;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Grid Settings")]
    [SerializeField] private float tileSize = 1f;

    private InputAction moveAction;
    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private bool isMovingGrid;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int LastMoveX = Animator.StringToHash("LastMoveX");
    private static readonly int LastMoveY = Animator.StringToHash("LastMoveY");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        //setando o input action para o movimento do personagem pq nao sei usar o newinput no editorr
        moveAction = new InputAction("Move", expectedControlType: "Vector2");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Gamepad>/dpad/up")
            .With("Down", "<Gamepad>/dpad/down")
            .With("Left", "<Gamepad>/dpad/left")
            .With("Right", "<Gamepad>/dpad/right");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Gamepad>/leftStick/up")
            .With("Down", "<Gamepad>/leftStick/down")
            .With("Left", "<Gamepad>/leftStick/left")
            .With("Right", "<Gamepad>/leftStick/right");
    }

    private void OnEnable()
    {
        moveAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
    }

    private void Update()
    {
        if (mode == MovementMode.GridBased && isMovingGrid) return;

        Vector2 rawInput = moveAction.ReadValue<Vector2>();

        moveInput = rawInput.normalized;

        if (moveInput != Vector2.zero)
        {
            if (animator != null)
            {
                animator.SetFloat(LastMoveX, moveInput.x);
                animator.SetFloat(LastMoveY, moveInput.y);
            }

            if (mode == MovementMode.GridBased)
            {
                Vector3 targetPos = transform.position + new Vector3(rawInput.x, rawInput.y, 0f) * tileSize;
                if (!IsObstacle(targetPos))
                {
                    StartCoroutine(MoveToGridPosition(targetPos));
                }
            }
        }

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (mode == MovementMode.Continuous)
        {
            rb.MovePosition(rb.position + moveInput * (moveSpeed * Time.fixedDeltaTime));
        }
    }

    private IEnumerator MoveToGridPosition(Vector3 targetPos)
    {
        isMovingGrid = true;

        while (Vector3.Distance(transform.position, targetPos) > 0.001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
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

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool moving = mode == MovementMode.GridBased ? isMovingGrid : moveInput != Vector2.zero;

        animator.SetFloat(MoveX, moveInput.x);
        animator.SetFloat(MoveY, moveInput.y);
        animator.SetBool(IsMoving, moving);
    }
}