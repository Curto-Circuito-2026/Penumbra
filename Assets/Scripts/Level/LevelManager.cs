using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerenciador do Nível e Sala (Level & Room Manager).
/// Responsável por reiniciar a sala, reposicionar o jogador, respawnar obstáculos e inimigos,
/// limpar projéteis/itens antigos e controlar o desbloqueio da Porta ao limpar a fase.
/// </summary>
public class LevelManager : MonoBehaviour
{
    private static LevelManager instance;

    public static LevelManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<LevelManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("[LevelManager]");
                    instance = obj.AddComponent<LevelManager>();
                }
            }
            return instance;
        }
    }

    [Header("Ponto de Início e Porta")]
    [Tooltip("Ponto onde o jogador é reposicionado no início de cada fase.")]
    [SerializeField] private Transform playerSpawnPoint;

    [Tooltip("Porta de transição para a próxima fase.")]
    [SerializeField] private StageDoor stageDoor;

    [Header("Prefabs de Inimigos e Obstáculos")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject woodenCratePrefab;
    [SerializeField] private GameObject stoneBlockPrefab;

    [Header("Pontos de Spawn no Mapa")]
    [SerializeField] private List<Transform> enemySpawnPoints = new List<Transform>();
    [SerializeField] private List<Transform> crateSpawnPoints = new List<Transform>();
    [SerializeField] private List<Transform> stoneSpawnPoints = new List<Transform>();

    // Listas internas de controle de entidades ativas na sala
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<GameObject> activeObstacles = new List<GameObject>();
    private int remainingEnemiesCount = 0;

    public int RemainingEnemiesCount => remainingEnemiesCount;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged += HandleStageChanged;
        }

        PlayerStats playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.OnPlayerRespawned += HandlePlayerRespawned;
        }
    }

    private void OnDisable()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged -= HandleStageChanged;
        }

        PlayerStats playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.OnPlayerRespawned -= HandlePlayerRespawned;
        }
    }

    private void Start()
    {
        FindDefaultsIfNull();
        int currentStage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;
        RespawnRoom(currentStage);
    }

    /// <summary>
    /// Localiza automaticamente prefabs e pontos de spawn se não configurados no Inspector.
    /// </summary>
    public void FindDefaultsIfNull()
    {
        if (stageDoor == null)
        {
            stageDoor = UnityEngine.Object.FindAnyObjectByType<StageDoor>();
        }

        if (playerSpawnPoint == null)
        {
            GameObject pSpawnObj = GameObject.Find("PlayerSpawnPoint");
            if (pSpawnObj != null) playerSpawnPoint = pSpawnObj.transform;
        }
    }

    /// <summary>
    /// Responde à mudança de fase no StageManager.
    /// </summary>
    private void HandleStageChanged(int stage)
    {
        RespawnRoom(stage);
    }

    /// <summary>
    /// Responde ao respawn do jogador (reinício após morte).
    /// </summary>
    private void HandlePlayerRespawned()
    {
        RespawnRoom(1);
    }

    /// <summary>
    /// Limpa a sala e respawna todos os elementos (Player, Obstáculos e Inimigos) para a fase informada.
    /// </summary>
    public void RespawnRoom(int stage)
    {
        Debug.Log($"<color=#00AAFF>[LevelManager] Reiniciando sala para a FASE {stage}...</color>");

        // 1. Limpa todas as entidades antigas da sala
        ClearRoomEntities();

        // 2. Reposiciona o Player no Ponto de Spawn
        RepositionPlayer();

        // 3. Respawna Obstáculos (Caixas e Blocos de Pedra)
        SpawnObstacles();

        // 4. Respawna Inimigos escalados para a fase atual
        SpawnEnemies(stage);

        // 5. Atualiza o estado da Porta
        UpdateDoorState();
    }

    /// <summary>
    /// Destrói projéteis, itens dropados, inimigos e obstáculos remanescentes.
    /// </summary>

    public void ClearRoomEntities()
    {
        // Destrói inimigos rastreados
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        activeEnemies.Clear();

        // Destrói obstáculos rastreados
        foreach (var obs in activeObstacles)
        {
            if (obs != null) Destroy(obs);
        }
        activeObstacles.Clear();

        // Limpa projéteis e itens dropados que sobraram na cena
        Projectile[] activeProjectiles = UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude);
        foreach (var proj in activeProjectiles)
        {
            if (proj != null) Destroy(proj.gameObject);
        }

        // Limpa moedas/itens dropados de caixas
        GameObject[] coins = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (var coin in coins)
        {
            if (coin != null && coin.name.Contains("DroppedCoin"))
            {
                Destroy(coin);
            }
        }

        // Remove inimigos que não estavam na lista por garantia
        EnemyAIController[] leftoverEnemies = UnityEngine.Object.FindObjectsByType<EnemyAIController>(FindObjectsInactive.Exclude);
        foreach (var enemy in leftoverEnemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }

        // Remove obstáculos destrutíveis/indestrutíveis antigos
        DestructibleObstacle[] leftoverDestructibles = UnityEngine.Object.FindObjectsByType<DestructibleObstacle>(FindObjectsInactive.Exclude);
        foreach (var obs in leftoverDestructibles)
        {
            if (obs != null) Destroy(obs.gameObject);
        }

        IndestructibleObstacle[] leftoverIndestructibles = UnityEngine.Object.FindObjectsByType<IndestructibleObstacle>(FindObjectsInactive.Exclude);
        foreach (var obs in leftoverIndestructibles)
        {
            if (obs != null) Destroy(obs.gameObject);
        }
    }

    private void RepositionPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("Player");

        if (playerObj != null)
        {
            Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : new Vector3(0f, -3f, 0f);
            playerObj.transform.position = spawnPos;

            Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            Debug.Log($"[LevelManager] Jogador reposicionado em {spawnPos}.");
        }
    }

    private void SpawnObstacles()
    {
        // Spawna Caixas Destrutíveis
        if (woodenCratePrefab != null && crateSpawnPoints.Count > 0)
        {
            foreach (var sp in crateSpawnPoints)
            {
                if (sp != null)
                {
                    GameObject crate = Instantiate(woodenCratePrefab, sp.position, Quaternion.identity);
                    activeObstacles.Add(crate);
                }
            }
        }

        // Spawna Blocos de Pedra Indestrutíveis
        if (stoneBlockPrefab != null && stoneSpawnPoints.Count > 0)
        {
            foreach (var sp in stoneSpawnPoints)
            {
                if (sp != null)
                {
                    GameObject stone = Instantiate(stoneBlockPrefab, sp.position, Quaternion.identity);
                    activeObstacles.Add(stone);
                }
            }
        }
    }

    private void SpawnEnemies(int stage)
    {
        remainingEnemiesCount = 0;

        if (enemyPrefab == null || enemySpawnPoints.Count == 0)
        {
            Debug.LogWarning("[LevelManager] Nenhum prefab de inimigo ou ponto de spawn configurado!");
            return;
        }

        // Determina a quantidade de inimigos baseada no nível da fase:
        // Fase 1 = 1 | Fase 2 = 2 | Fase 3 = 2 | Fase 4 = 3 | Fase 5 = 3 | Fase 6 = 4 ...
        int enemiesToSpawn = Mathf.Max(1, 1 + (stage / 2));

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            Transform sp = enemySpawnPoints[i % enemySpawnPoints.Count];
            if (sp == null) continue;

            // Se houver mais inimigos do que pontos de spawn, adiciona um leve deslocamento para não nascerem exatamente em cima
            Vector3 spawnPos = sp.position;
            if (i >= enemySpawnPoints.Count)
            {
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 0.8f;
                spawnPos += new Vector3(randomOffset.x, randomOffset.y, 0f);
            }

            // Instancia o inimigo
            GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            activeEnemies.Add(enemyObj);
            remainingEnemiesCount++;

            // Configura os eventos e escalonamento do inimigo
            if (enemyObj.TryGetComponent(out EnemyAIController aiController))
            {
                aiController.ApplyStageScaling(stage);
            }

            if (enemyObj.TryGetComponent(out EnemyStats stats))
            {
                stats.OnEnemyDied += HandleEnemyDefeated;
            }
        }

        Debug.Log($"[LevelManager] {remainingEnemiesCount} inimigos spawnados para a Fase {stage}.");
    }

    private void HandleEnemyDefeated()
    {
        remainingEnemiesCount--;
        if (remainingEnemiesCount < 0) remainingEnemiesCount = 0;

        Debug.Log($"[LevelManager] Inimigo derrotado! Restantes: {remainingEnemiesCount}");

        if (remainingEnemiesCount <= 0)
        {
            OnRoomCleared();
        }
    }

    private void OnRoomCleared()
    {
        Debug.Log("<color=#00FF88>[LevelManager] TODOS OS INIMIGOS DERROTADOS! SALA LIMPA!</color>");

        if (stageDoor != null)
        {
            stageDoor.SetUnlocked(true);
        }
    }

    private void UpdateDoorState()
    {
        if (stageDoor != null)
        {
            bool shouldUnlock = remainingEnemiesCount <= 0;
            stageDoor.SetUnlocked(shouldUnlock);
        }
    }

    #region Atribuição Manual de Ponto de Spawn (Editor/Runtime)
    public void ConfigureSpawnPoints(Transform pSpawn, StageDoor door, GameObject enemyPf, GameObject cratePf, GameObject stonePf)
    {
        playerSpawnPoint = pSpawn;
        stageDoor = door;
        enemyPrefab = enemyPf;
        woodenCratePrefab = cratePf;
        stoneBlockPrefab = stonePf;
    }

    public void AddEnemySpawnPoint(Transform sp) { if (sp != null && !enemySpawnPoints.Contains(sp)) enemySpawnPoints.Add(sp); }
    public void AddCrateSpawnPoint(Transform sp) { if (sp != null && !crateSpawnPoints.Contains(sp)) crateSpawnPoints.Add(sp); }
    public void AddStoneSpawnPoint(Transform sp) { if (sp != null && !stoneSpawnPoints.Contains(sp)) stoneSpawnPoints.Add(sp); }
    #endregion
}
