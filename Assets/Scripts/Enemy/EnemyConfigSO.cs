using UnityEngine;

/// <summary>
/// ScriptableObject para armazenar configurações reutilizáveis do Inimigo.
/// Permite definir comportamentos Melee, Ranged, velocidade, alcances, cooldowns e prefabs de projétil.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyConfig", menuName = "Combat/Enemy Configuration")]
public class EnemyConfigSO : ScriptableObject
{
    [Header("Flags de Capacidade de Ataque")]
    [Tooltip("Permite o inimigo realizar ataques Melee de faca.")]
    public bool canUseMelee = true;

    [Tooltip("Permite o inimigo realizar disparos Ranged de projétil.")]
    public bool canUseRanged = true;

    [Header("Atributos de Combate")]
    [Tooltip("Dano do ataque Melee (Faca).")]
    public float meleeDamage = 15f;

    [Tooltip("Alcance do ataque Melee em unidades.")]
    public float meleeRange = 1.8f;

    [Tooltip("Dano do tiro Ranged.")]
    public float rangedDamage = 10f;

    [Tooltip("Alcance do tiro Ranged / Distância segura do inimigo.")]
    public float rangedRange = 7.5f;

    [Tooltip("Tempo de recarga entre ataques em segundos.")]
    public float attackCooldown = 1.5f;

    [Header("IA e Visão")]
    [Tooltip("Raio de detecção do Player em unidades.")]
    public float detectionRadius = 10f;

    [Tooltip("Velocidade de movimentação no NavMesh.")]
    public float moveSpeed = 3.5f;

    [Header("Prefabs de Ataque")]
    [Tooltip("Prefab de Projétil disparado pelo inimigo (mesmo do Player).")]
    public GameObject projectilePrefab;
}
