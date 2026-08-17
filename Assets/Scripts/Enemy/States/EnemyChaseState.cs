using UnityEngine;

/// <summary>
/// Estado de Perseguição (Chase).
/// Move o inimigo em direção ao Player pelo NavMesh e decide quando transicionar para MeleeAttack ou RangedAttack.
/// </summary>
public class EnemyChaseState : IEnemyState
{
    private readonly EnemyAIController ai;

    public EnemyChaseState(EnemyAIController aiController)
    {
        ai = aiController;
    }

    public void Enter() { }

    public void Update()
    {
        if (!ai.IsTargetAlive())
        {
            ai.ChangeState(ai.IdleState);
            return;
        }

        float distance = Vector3.Distance(ai.transform.position, ai.TargetPlayer.position);

        // Se o player estiver fora do raio de detecção, volta para Idle
        if (distance > ai.DetectionRadius * 1.3f)
        {
            ai.ChangeState(ai.IdleState);
            return;
        }

        // Se não estiver em cooldown de ataque, verifica se pode atacar
        if (!ai.IsAttackOnCooldown)
        {
            // Check 1: Ataque Melee (Se habilitado e no alcance da faca)
            if (ai.CanUseMelee && distance <= ai.MeleeRange)
            {
                ai.ChangeState(ai.MeleeAttackState);
                return;
            }

            // Check 2: Ataque Ranged (Se habilitado, no alcance e com Linha de Visão limpa)
            if (ai.CanUseRanged && distance <= ai.RangedRange && ai.HasLineOfSightToTarget())
            {
                if (!ai.CanUseMelee || distance <= ai.RangedRange * 0.85f)
                {
                    ai.ChangeState(ai.RangedAttackState);
                    return;
                }
            }
        }

        // Se já estiver dentro da distância de parada, para o movimento para não empurrar o player
        float stopDist = ai.CanUseMelee ? ai.MeleeRange * 0.8f : ai.RangedRange * 0.7f;
        if (distance <= stopDist)
        {
            ai.StopMovement();
            return;
        }

        // Continua perseguindo o player pelo NavMesh
        ai.MoveToTarget(ai.TargetPlayer.position);
    }

    public void Exit()
    {
        ai.StopMovement();
    }
}

