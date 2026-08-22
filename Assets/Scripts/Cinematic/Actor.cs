using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public struct MoveTarget
{
    public Vector2 position;
    public float speed;
}
public class Actor : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] public Sprite face;
    [SerializeField] public float baseSpeed;

    [SerializeField] public string actorName;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");

    public Rigidbody2D body;

    public bool moving = false;
    Queue<MoveTarget> targetPool = new Queue<MoveTarget>();
    MoveTarget? currentTarget = null;
    public void Awake()
    {
        body = this.GetComponent<Rigidbody2D>();
    }

    public void Say(List<string> text)
    {
        //toDo
    }

    public void MoveTo(Vector2 pos)
    {
        moving = true;
        MoveTarget target = new MoveTarget() { position = pos, speed = baseSpeed };
        targetPool.Enqueue(target);

    }

    public void MoveTo(MoveTarget target)
    {
        moving = true;
        targetPool.Enqueue(target);
    }

    public void MoveSequence(List<MoveTarget> target)
    {
        foreach (MoveTarget targetItem in target){ MoveTo(targetItem);}
    }

    public void SetMoveTarget(Vector2 pos)
    {
        currentTarget = new MoveTarget() { position = pos, speed = baseSpeed };
    }

    public void SetMoveTarget(MoveTarget target)
    {
        currentTarget = target;
    }

    private void FixedUpdate()
    {
        if (!moving) return;
        if (currentTarget == null) {
            if (targetPool.Count > 0) { currentTarget = targetPool.Dequeue(); }
            else { moving = false; currentTarget = null; return; }
        }

        
            if (Vector2.Distance(body.position, currentTarget.Value.position) <= 0.01f)
            {
                currentTarget = null;
                return;
            }
            else
            {

                float step = currentTarget.Value.speed * Time.fixedDeltaTime;

                Vector2 newPos = Vector2.MoveTowards(body.position, currentTarget.Value.position, step);
                body.MovePosition(newPos);

                float deltaX = currentTarget.Value.position.x - body.position.x;
                float deltaY = currentTarget.Value.position.y - body.position.y;

                float dirX = math.sign(deltaX);
                float dirY = math.sign(deltaY);

                if(animator)
                {
                    animator.SetFloat(MoveX, dirX);
                    animator.SetFloat(MoveY, dirY);
                    animator.SetBool(IsMoving, moving);
                }

            }
        
    }
}
