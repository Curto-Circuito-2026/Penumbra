using UnityEngine;

/// <summary>
/// Interface para a Máquina de Estados da IA do Inimigo.
/// </summary>
public interface IEnemyState
{
    /// <summary>
    /// Chamado ao entrar no estado.
    /// </summary>
    void Enter();

    /// <summary>
    /// Chamado a cada frame durante o Update.
    /// </summary>
    void Update();

    /// <summary>
    /// Chamado ao sair do estado.
    /// </summary>
    void Exit();
}
