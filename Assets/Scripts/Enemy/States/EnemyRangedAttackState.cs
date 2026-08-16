using UnityEngine;

/// <summary>
/// Estado de Ataque Ranged (Tiro em Linha Reta).
/// Para a movimentação, captura a direção do Player no instante do disparo,
/// aciona a animação 'AttackRanged' e lança o Projétil em linha reta constante.
/// </summary>
public class EnemyRangedAttackState : IEnemyState
{
    private readonly EnemyAIController ai;
    private bool attackExecuted = false;
    private float holdTimer = 0f;

    public EnemyRangedAttackState(EnemyAIController aiController)
    {
        ai = aiController;
    }

    public void Enter()
    {
        ai.StopMovement();
        attackExecuted = false;
        holdTimer = 0.25f; // Tempo de retenção da animação de tiro
    }

    public void Update()
    {
        if (!ai.IsTargetAlive())
        {
            ai.ChangeState(ai.IdleState);
            return;
        }

        Vector3 targetDirection = (ai.TargetPlayer.position - ai.transform.position).normalized;

        if (!attackExecuted)
        {
            attackExecuted = true;

            // Dispara o parâmetro 'AttackRanged' no Animator
            ai.TriggerRangedAttack();

            // Dispara o Projétil em linha reta constante na direção capturada
            if (ai.CombatController != null)
            {
                ai.CombatController.PerformRangedAttack(targetDirection, ai.ProjectilePrefab);
            }
        }

        holdTimer -= Time.deltaTime;
        if (holdTimer <= 0f)
        {
            ai.ChangeState(ai.ChaseState);
        }
    }

    public void Exit() { }
}

