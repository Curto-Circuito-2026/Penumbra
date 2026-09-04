using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado de Explosão / Suicídio (estilo Creeper).
/// Ao se aproximar do Player, o inimigo para, ativa a animação de detonação/ataque,
/// aguarda o tempo do pavio (fuse time) e detona causando dano em área (AoE) e se autodestruindo.
/// </summary>
public class EnemyExplodeState : IEnemyState
{
    private readonly EnemyAIController ai;
    private float fuseTimer = 0f;
    private bool hasExploded = false;

    public EnemyExplodeState(EnemyAIController aiController)
    {
        ai = aiController;
    }

    public void Enter()
    {
        ai.StopMovement();
        fuseTimer = ai.ExplosionFuseTime;
        hasExploded = false;

        // Força a execução imediata do estado de animação Attack (Sprites 1 -> 4 -> 7)
        if (ai.Animator != null)
        {
            ai.Animator.Play("Attack", 0, 0f);
        }
        else
        {
            ai.TriggerMeleeAttack();
        }

        // Desativa colisores para não empurrar nem colidir durante a contagem
        foreach (var c in ai.GetComponentsInChildren<Collider2D>())
        {
            c.enabled = false;
        }

        Debug.Log($"[EnemyExplodeState] '{ai.gameObject.name}' armou explosivo! Detonação em {fuseTimer:F2}s...");
    }

    public void Update()
    {
        if (hasExploded) return;

        ai.StopMovement();

        fuseTimer -= Time.deltaTime;

        if (fuseTimer <= 0f)
        {
            hasExploded = true;
            ExecuteExplosion();
        }
    }

    private void ExecuteExplosion()
    {
        ai.TriggerExplosionImmediate();
    }

    public void Exit() { }
}
