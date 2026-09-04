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

    [Header("Comportamento Creeper / Explosão Suicide")]
    [Tooltip("Permite o inimigo agir como Creeper: corre até o player e explode causando dano massivo em área.")]
    public bool canExplode = false;

    [Tooltip("Distância do player em que inicia a contagem do pavio/animação de explosão.")]
    public float explosionTriggerDistance = 1.8f;

    [Tooltip("Raio da explosão em área.")]
    public float explosionRadius = 2.8f;

    [Tooltip("Dano causado pela explosão no centro.")]
    public float explosionDamage = 40f;

    [Tooltip("Tempo de pavio/animação de preparação antes da detonação em segundos.")]
    public float explosionFuseTime = 0.55f;

    [Tooltip("Tempo de recarga entre ataques em segundos.")]
    public float attackCooldown = 1.5f;

    [Tooltip("Tempo de espera (windup) na animação até instanciar o projétil em segundos.")]
    public float rangedAttackWindupDelay = 0.28f;

    [Tooltip("Duração total da animação de ataque ranged em segundos.")]
    public float rangedAttackDuration = 0.45f;

    [Header("IA e Visão")]
    [Tooltip("Raio de detecção do Player em unidades.")]
    public float detectionRadius = 10f;

    [Tooltip("Velocidade de movimentação no NavMesh.")]
    public float moveSpeed = 3.5f;

    [Header("Prefabs de Ataque")]
    [Tooltip("Prefab de Projétil disparado pelo inimigo (mesmo do Player).")]
    public GameObject projectilePrefab;
}
