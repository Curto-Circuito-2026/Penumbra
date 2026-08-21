using UnityEngine;

/// <summary>
/// Estado de Ataque Ranged (Tiro em Linha Reta).
/// Para a movimentação, captura a direção do Player no instante do disparo,
/// aciona a animação 'AttackRanged' e lança o Projétil em linha reta constante.
/// </summary>
public class EnemyRangedAttackState : IEnemyState
{
    private readonly EnemyAIController ai;
    private bool projectileSpawned = false;
    private float timer = 0f;
    private Vector3 lockedDirection;

    public EnemyRangedAttackState(EnemyAIController aiController)
    {
        ai = aiController;
    }

    public void Enter()
    {
        ai.StopMovement();
        projectileSpawned = false;
        timer = 0f;

        if (ai.TargetPlayer != null)
        {
            lockedDirection = (ai.TargetPlayer.position - ai.transform.position).normalized;
        }
        else
        {
            lockedDirection = ai.transform.right;
        }

        // Aciona o gatilho da animação de arremesso
        ai.TriggerRangedAttack();
    }

    public void Update()
    {
        if (!ai.IsTargetAlive())
        {
            ai.ChangeState(ai.IdleState);
            return;
        }

        timer += Time.deltaTime;

        float windup = ai.RangedAttackWindupDelay > 0f ? ai.RangedAttackWindupDelay : 0.28f;
        float totalDuration = ai.RangedAttackDuration > 0f ? ai.RangedAttackDuration : 0.45f;

        // Instancia o projétil exatamente no frame correto da animação de ataque
        if (!projectileSpawned && timer >= windup)
        {
            projectileSpawned = true;

            if (ai.CombatController != null)
            {
                ai.CombatController.PerformRangedAttack(lockedDirection, ai.ProjectilePrefab);
            }
        }

        // Retorna ao estado de perseguição apenas após o término completo da animação
        if (timer >= totalDuration)
        {
            ai.ChangeState(ai.ChaseState);
        }
    }

    public void Exit() { }
}

