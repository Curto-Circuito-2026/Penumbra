using UnityEngine;

/// <summary>
/// Interface para qualquer entidade que possa receber dano no sistema de combate.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Aplica dano à entidade.
    /// </summary>
    /// <param name="amount">Quantidade de dano.</param>
    /// <param name="hitDirection">Direção de onde veio o impacto do ataque.</param>
    void TakeDamage(float amount, Vector3 hitDirection);
}
