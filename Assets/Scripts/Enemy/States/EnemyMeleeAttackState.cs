using UnityEngine;

/// <summary>
/// Estado de Ataque Melee (Faca).
/// Para a navegação, gira na direção do Player, dispara a animação 'AttackMelee' e executa o golpe de faca.
/// </summary>
public class EnemyMeleeAttackState : IEnemyState
{
    private readonly EnemyAIController ai;
    private bool attackExecuted = false;
    private float holdTimer = 0f;

    public EnemyMeleeAttackState(EnemyAIController aiController)
    {
        ai = aiController;
    }

    public void Enter()
    {
        ai.StopMovement();
        attackExecuted = false;
        holdTimer = 0.25f; // Tempo de retenção da animação da faca
    }

    public void Update()
    {
        if (!ai.IsTargetAlive())
        {
            ai.ChangeState(ai.IdleState);
            return;
        }

        Vector3 direction = (ai.TargetPlayer.position - ai.transform.position).normalized;

        if (!attackExecuted)
        {
            attackExecuted = true;

            // Dispara trigger de animação 'AttackMelee' idêntico ao do Player
            ai.TriggerMeleeAttack();

            // Executa a lógica e o golpe da faca
            if (ai.CombatController != null)
            {
                ai.CombatController.PerformMeleeAttack(direction);
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

