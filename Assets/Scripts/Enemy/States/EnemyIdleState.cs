using UnityEngine;

/// <summary>
/// Estado Idle do Inimigo.
/// Fica parado e varre a área ao redor em busca do Player dentro do raio de visão e linha de visada.
/// </summary>
public class EnemyIdleState : IEnemyState
{
    private readonly EnemyAIController ai;

    public EnemyIdleState(EnemyAIController aiController)
    {
        ai = aiController;
    }

    public void Enter()
    {
        ai.StopMovement();
        ai.UpdateAnimatorSpeed(0f);
    }

    public void Update()
    {
        if (!ai.IsTargetAlive())
        {
            ai.FindPlayerTarget();
            ai.StopMovement();
            return;
        }

        // Verifica distância e linha de visão até o Player
        float distance = Vector3.Distance(ai.transform.position, ai.TargetPlayer.position);
        if (distance <= ai.DetectionRadius && ai.HasLineOfSightToTarget())
        {
            ai.ChangeState(ai.ChaseState);
        }
    }

    public void Exit() { }
}
