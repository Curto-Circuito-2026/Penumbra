using UnityEngine;

/// <summary>
/// Estado de Morte da IA do Inimigo.
/// Desativa a movimentação, navegação e comandos de ataque.
/// </summary>
public class EnemyDeadState : IEnemyState
{
    private readonly EnemyAIController ai;

    public EnemyDeadState(EnemyAIController aiController)
    {
        ai = aiController;
    }

    public void Enter()
    {
        ai.StopMovement();
    }

    public void Update() { }

    public void Exit() { }
}
