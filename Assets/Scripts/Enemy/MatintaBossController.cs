using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador da Boss Matinta Perera:
/// - Flutua suavemente mantendo distância estratégica do Player dentro de sua Zona de Confinamento (Arena).
/// - Magia 1: Invoca Corpo-Seco (de 2 a 5 dependendo da vida restante).
/// - Magia 2: Dispara projétil de Pássaro Sombrio que bate asas em direção ao jogador.
/// - Mecânica Especial (Casulo / Ilusão):
///   Ao sofrer dano alto ou se o player chegar muito perto, fecha a capa virando casulo e se transforma REALMENTE em um pássaro!
///   Junto com outros pássaros ilusórios, ela voa e zanza pela arena.
///   No fim da ilusão, o pássaro pousa e roda a animação de casulo ao contrário se transformando em humano novamente, enquanto os outros pássaros mergulham no jogador!
/// </summary>
public class MatintaBossController : MonoBehaviour, IDamageable
{
    [Header("Identificação & Vida")]
    [SerializeField] private string bossName = "MATINTA PERERA";
    [SerializeField] private float maxHealth = 400f;
    [SerializeField] private float currentHealth = 400f;

    [Header("Zona de Confinamento da Arena")]
    [SerializeField] private Vector3 arenaCenter;
    [SerializeField] private float arenaRadius = 8.5f;
    [SerializeField] private bool useSpawnPositionAsArenaCenter = true;

    [Header("Combate & Ataques")]
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float attackCooldown = 2.4f;
    [SerializeField] private float birdProjectileDamage = 18f;
    [SerializeField] private float closeDistanceTrigger = 2.5f;
    [SerializeField] private float illusionDamageThreshold = 75f;
    [SerializeField] private float illusionCooldown = 9.0f;

    [Header("Prefabs & Referências")]
    [SerializeField] private GameObject corpoSecoPrefab;
    [SerializeField] private GameObject birdProjectilePrefab;
    [SerializeField] private GameObject illusionBirdPrefab;
    [SerializeField] private GameObject starPickupPrefab;
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Animators para Transformação")]
    [SerializeField] private RuntimeAnimatorController matintaAnimatorController;
    [SerializeField] private RuntimeAnimatorController birdAnimatorController;

    [Header("Movimentação Flutuante")]
    [SerializeField] private float moveSpeed = 2.4f;
    [SerializeField] private float preferredDistance = 6.5f;

    [Header("Efeitos Visuais")]
    [SerializeField] private Color damageFlashColor = new Color(0.6f, 0.2f, 0.8f, 1f);

    [Header("Ativação de Combate")]
    [SerializeField] private bool autoStartCombat = false;
    [SerializeField] private BossTrigger bossIntro;

    [Header("Áudio da Transformação em Pássaro")]
    [Tooltip("Som que toca desde o início da transformação (casulo) até o momento em que os pássaros ilusórios partem para o ataque.")]
    [SerializeField] private AudioClip illusionTransformSFX;
    [Tooltip("AudioSource dedicado a este som. Se deixado vazio, um é criado automaticamente.")]
    [SerializeField] private AudioSource transformAudioSource;

    [Header("Outros SFX")]
    [SerializeField] private AudioClip magicCastSFX;
    [SerializeField] private AudioClip birdProjectileLaunchSFX;
    [SerializeField] private AudioClip deathSFX;

    // Componentes
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private NavMeshAgent agent;
    private Collider2D bodyCollider;
    private Transform playerTransform;

    // Estados
    private bool isCombatActive = false;
    private bool isExecutingAction = false;
    private bool isDead = false;
    private bool isTransformedInBird = false;
    private float attackTimer = 1.0f;
    private float illusionTimer = 0f;
    private float damageSinceLastIllusion = 0f;
    private int attackCycleIndex = 0;
    private List<GameObject> activeMinions = new List<GameObject>();

    // Hashes de Animação
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MagicHash = Animator.StringToHash("Magic");
    private static readonly int TransformInHash = Animator.StringToHash("TransformIn");
    private static readonly int TransformOutHash = Animator.StringToHash("TransformOut");

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsCombatActive => isCombatActive;
    public bool IsDead => isDead;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        bodyCollider = GetComponent<Collider2D>();

        if (animator != null && matintaAnimatorController == null)
        {
            matintaAnimatorController = animator.runtimeAnimatorController;
        }

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = moveSpeed;
            agent.stoppingDistance = preferredDistance * 0.8f;
        }

        if (playerLayerMask == 0)
        {
            int pLayer = LayerMask.NameToLayer("Player");
            playerLayerMask = pLayer != -1 ? (1 << pLayer) : (1 << 0);
        }

        currentHealth = maxHealth;

        if (transformAudioSource == null)
        {
            transformAudioSource = gameObject.AddComponent<AudioSource>();
        }
        transformAudioSource.playOnAwake = false;
        transformAudioSource.loop = false;
        transformAudioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (useSpawnPositionAsArenaCenter)
        {
            arenaCenter = transform.position;
        }

        LocatePlayer();

        if (autoStartCombat)
        {
            StartCombat();
        }
    }

    public void StartCombat()
    {
        if (bossIntro == null)
        {
            if (transform.parent != null) bossIntro = transform.parent.GetComponentInChildren<BossTrigger>();
            if (bossIntro == null) bossIntro = UnityEngine.Object.FindAnyObjectByType<BossTrigger>();
        }

        CinematicManager cinematicManager = GameObject.Find("CinematicManager") != null ? GameObject.Find("CinematicManager").GetComponent<CinematicManager>() : (CinematicManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<CinematicManager>());

        if (cinematicManager != null && bossIntro != null)
        {
            bossIntro.Boss = this.gameObject;
            cinematicManager.PlayClip(bossIntro.gameObject);
        }

        StartBossFight();
    }

    public Vector3 ClampToArena(Vector3 position)
    {
        Vector3 offset = position - arenaCenter;
        offset.z = 0f;
        if (offset.magnitude > arenaRadius)
        {
            return arenaCenter + offset.normalized * arenaRadius;
        }
        return position;
    }

    private void LocatePlayer()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            IgnorePlayerPhysicsCollision();
        }
    }

    private void IgnorePlayerPhysicsCollision()
    {
        if (playerTransform == null) return;
        Collider2D[] myCols = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] playerCols = playerTransform.GetComponentsInChildren<Collider2D>(true);
        foreach (var m in myCols)
        {
            if (m == null) continue;
            foreach (var p in playerCols)
            {
                if (p != null) Physics2D.IgnoreCollision(m, p, true);
            }
        }
    }

    private void Update()
    {
        if (isDead) return;

        if (playerTransform == null)
        {
            LocatePlayer();
            if (playerTransform == null) return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // Ativação da luta apenas se autoStartCombat for verdadeiro
        if (!isCombatActive && autoStartCombat && distance <= detectionRadius)
        {
            StartCombat();
        }

        bool isCutscene = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Playing;
        if (!isCombatActive || isExecutingAction || isCutscene)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            if (animator != null) animator.SetFloat(SpeedHash, 0f);
            return;
        }

        UpdateFacingDirection();

        attackTimer -= Time.deltaTime;
        illusionTimer -= Time.deltaTime;

        // Gatilho de Fuga / Ilusão do Casulo
        bool shouldTriggerIllusion = (distance <= closeDistanceTrigger || damageSinceLastIllusion >= illusionDamageThreshold) && illusionTimer <= 0f;

        if (shouldTriggerIllusion)
        {
            StartCoroutine(PerformIllusionTransformationRoutine());
            return;
        }

        // Execução de Magias Regulares (Corpo-Seco ou Projétil Pássaro)
        if (attackTimer <= 0f)
        {
            ExecuteNextSpell();
        }
        else
        {
            // Movimentação mantendo distância tática
            MaintainDistanceWithPlayer(distance);
        }
    }

    public void StartBossFight()
    {
        if (isCombatActive || isDead) return;
        isCombatActive = true;
        attackTimer = 2.2f; // Delay de "acordar" após a cutscene
        illusionTimer = 4.5f;

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.ShowBoss(bossName, currentHealth, maxHealth);
        }

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.TriggerCameraShake(0.4f, 0.25f);
        }

        Debug.Log($"[MatintaBoss] Combate com {bossName} iniciado na Arena ({arenaCenter}, Raio: {arenaRadius})! Primeiro ataque em {attackTimer}s.");
    }

    private void UpdateFacingDirection()
    {
        if (playerTransform == null || spriteRenderer == null || isTransformedInBird) return;
        bool playerIsRight = playerTransform.position.x > transform.position.x;
        spriteRenderer.flipX = !playerIsRight; // Inverte para olhar para o jogador
    }

    private void MaintainDistanceWithPlayer(float currentDistance)
    {
        if (playerTransform == null) return;

        if (currentDistance < preferredDistance - 1.0f)
        {
            // Muito perto: afasta-se do jogador respeitando a arena
            Vector3 retreatDir = (transform.position - playerTransform.position).normalized;
            Vector3 targetRetreat = ClampToArena(transform.position + retreatDir * 3f);
            MoveToPosition(targetRetreat);
        }
        else if (currentDistance > preferredDistance + 2.5f)
        {
            // Muito longe: aproxima-se suavemente respeitando a arena
            Vector3 target = ClampToArena(playerTransform.position);
            MoveToPosition(target);
        }
        else
        {
            // Na distância ideal: circula ou para suavemente
            StopMovement();
        }
    }

    private void MoveToPosition(Vector3 destination)
    {
        destination = ClampToArena(destination);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
            if (animator != null) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        }
        else
        {
            Vector3 dir = (destination - transform.position).normalized;
            transform.position += dir * (moveSpeed * Time.deltaTime);
            transform.position = ClampToArena(transform.position);
            if (animator != null) animator.SetFloat(SpeedHash, moveSpeed);
        }
    }

    private void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (animator != null) animator.SetFloat(SpeedHash, 0f);
    }

    private void ExecuteNextSpell()
    {
        isExecutingAction = true;
        StopMovement();

        // Limpa lacaios que já morreram ou foram destruídos
        activeMinions.RemoveAll(item => item == null || !item.activeInHierarchy || (item.TryGetComponent(out EnemyStats es) && es.CurrentHealth <= 0));

        // Regra: Só pode invocar novos Corpo-Secos se TODOS os invocados anteriormente tiverem morrido!
        if (activeMinions.Count > 0)
        {
            // Enquanto tiver Corpo-Seco vivo, ataca apenas com Projéteis de Pássaro
            StartCoroutine(PerformBirdProjectileRoutine());
            return;
        }

        // Se a arena estiver limpa de lacaios, alterna entre invocar e disparar pássaros
        int spellIndex = attackCycleIndex % 2;
        attackCycleIndex++;

        if (spellIndex == 0)
        {
            StartCoroutine(PerformSummonCorpoSecoRoutine());
        }
        else
        {
            StartCoroutine(PerformBirdProjectileRoutine());
        }
    }

    #region Magia 1: Invocar Corpo-Seco
    private IEnumerator PerformSummonCorpoSecoRoutine()
    {
        Debug.Log("[MatintaBoss] Invocando lacaios Corpo-Seco!");
        if (animator != null) animator.SetTrigger(MagicHash);

        yield return new WaitForSeconds(0.28f);

        AudioController.Instance.PlaySFX(magicCastSFX);

        // Quantidade de Corpo-Secos baseada na vida atual da Matinta
        float healthPct = currentHealth / maxHealth;
        int summonCount = 2;
        if (healthPct <= 0.40f) summonCount = 4;
        else if (healthPct <= 0.70f) summonCount = 3;

        // Limita o total de lacaios em cena para não sobrecarregar
        int canSpawn = Mathf.Max(1, 5 - activeMinions.Count);
        summonCount = Mathf.Min(summonCount, canSpawn);

        if (corpoSecoPrefab != null)
        {
            for (int i = 0; i < summonCount; i++)
            {
                float angle = (360f / summonCount) * i * Mathf.Deg2Rad;
                Vector3 spawnOffset = new Vector3(Mathf.Cos(angle) * 2.2f, Mathf.Sin(angle) * 1.6f, 0f);
                Vector3 spawnPos = ClampToArena(transform.position + spawnOffset);

                // Efeito de invocação das trevas
                if (CombatVisualEffects.Instance != null)
                {
                    CombatVisualEffects.Instance.PlayImpactBurst(spawnPos, new Color(0.15f, 0.05f, 0.25f), 1.5f);
                }

                GameObject minion = Instantiate(corpoSecoPrefab, spawnPos, Quaternion.identity);
                activeMinions.Add(minion);
            }
        }

        yield return new WaitForSeconds(0.45f);

        attackTimer = attackCooldown;
        isExecutingAction = false;
    }
    #endregion

    #region Magia 2: Disparo de Pássaro Projétil
    private IEnumerator PerformBirdProjectileRoutine()
    {
        Debug.Log("[MatintaBoss] Lançando Projétil Pássaro Sombrio!");
        if (animator != null) animator.SetTrigger(MagicHash);

        AudioController.Instance.PlaySFX(birdProjectileLaunchSFX);

        yield return new WaitForSeconds(0.25f);

        Vector3 targetPos = (playerTransform != null) ? playerTransform.position : (transform.position + Vector3.left * 5f);
        Vector3 baseDir = (targetPos - transform.position).normalized;

        float healthPct = currentHealth / maxHealth;
        int birdCount = (healthPct < 0.45f) ? 3 : 1;

        if (birdProjectilePrefab != null)
        {
            if (birdCount == 1)
            {
                SpawnBirdProjectile(transform.position + baseDir * 0.8f, baseDir);
            }
            else
            {
                // Disparo em leque de 3 pássaros
                float[] angles = new float[] { -22f, 0f, 22f };
                foreach (var ang in angles)
                {
                    Vector3 rotatedDir = Quaternion.Euler(0, 0, ang) * baseDir;
                    SpawnBirdProjectile(transform.position + rotatedDir * 0.8f, rotatedDir);
                }
            }
        }

        yield return new WaitForSeconds(0.35f);

        attackTimer = attackCooldown * 0.9f;
        isExecutingAction = false;
    }

    private void SpawnBirdProjectile(Vector3 spawnPos, Vector3 direction)
    {
        GameObject birdObj = Instantiate(birdProjectilePrefab, spawnPos, Quaternion.identity);
        if (birdObj.TryGetComponent(out Projectile proj))
        {
            proj.Initialize(direction, gameObject, birdProjectileDamage, playerLayerMask);
        }

        // Flip do sprite do pássaro na direção do voo
        if (birdObj.TryGetComponent(out SpriteRenderer sr))
        {
            sr.flipX = direction.x < 0f;
        }
    }
    #endregion

    #region Especial: Ilusão do Casulo e Transformação em Pássaro

    private void PlayIllusionTransformSFX()
    {
        if (transformAudioSource == null || illusionTransformSFX == null) return;

        float sfxVolume = AudioController.Instance != null ? AudioController.Instance.SFXVolume : 1f;
        float masterVolume = AudioController.Instance != null ? AudioController.Instance.MasterVolume : 1f;

        transformAudioSource.clip = illusionTransformSFX;
        transformAudioSource.volume = masterVolume * sfxVolume;
        transformAudioSource.Play();
    }

    private void StopIllusionTransformSFX()
    {
        if (transformAudioSource != null && transformAudioSource.isPlaying)
        {
            transformAudioSource.Stop();
        }
    }

    private IEnumerator PerformIllusionTransformationRoutine()
    {
        Debug.Log("[MatintaBoss] Fechando casulo, transformando-se em Pássaro e iniciando revoada!");
        isExecutingAction = true;
        damageSinceLastIllusion = 0f;
        illusionTimer = illusionCooldown;
        StopMovement();

        // 1. Animação de entrar no casulo e emergir como pássaro
        if (animator != null) animator.SetTrigger(TransformInHash);
        if (bodyCollider != null) bodyCollider.enabled = false;
        PlayIllusionTransformSFX();

        yield return new WaitForSeconds(0.60f);

        // Efeito de fumaça e penas na transformação
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.25f, 0.05f, 0.4f), 2.2f);
        }

        // 2. Estado de pássaro voando ativo
        isTransformedInBird = true;
        transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        // 3. Spawna os outros pássaros ilusórios que zanzam pela arena junto com ela
        int flockSize = Random.Range(5, 8);
        List<MatintaIllusionBird> illusionBirds = new List<MatintaIllusionBird>();

        if (illusionBirdPrefab != null)
        {
            for (int i = 0; i < flockSize; i++)
            {
                Vector3 spawnOffset = (Vector3)(Random.insideUnitCircle * Random.Range(1.5f, 4f));
                Vector3 bSpawnPos = ClampToArena(transform.position + spawnOffset);
                GameObject bObj = Instantiate(illusionBirdPrefab, bSpawnPos, Quaternion.identity);
                if (bObj.TryGetComponent(out MatintaIllusionBird birdComp))
                {
                    birdComp.InitializeWander(arenaCenter, playerTransform, arenaRadius);
                    illusionBirds.Add(birdComp);
                }
            }
        }

        // 4. A própria Matinta voa batendo asas zanzando na arena junto com os pássaros ilusórios
        float illusionFlyDuration = 3.2f;
        float elapsed = 0f;
        Vector3 birdWanderTarget = ClampToArena(arenaCenter + (Vector3)(Random.insideUnitCircle * Random.Range(2f, arenaRadius)));
        float wanderTimer = 0.8f;

        while (elapsed < illusionFlyDuration)
        {
            elapsed += Time.deltaTime;
            wanderTimer -= Time.deltaTime;

            if (wanderTimer <= 0f || Vector3.Distance(transform.position, birdWanderTarget) < 0.5f)
            {
                birdWanderTarget = ClampToArena(arenaCenter + (Vector3)(Random.insideUnitCircle * Random.Range(2f, arenaRadius)));
                wanderTimer = Random.Range(0.6f, 1.2f);
            }

            Vector3 moveDir = (birdWanderTarget - transform.position).normalized;
            transform.position += moveDir * (5.5f * Time.deltaTime);
            transform.position = ClampToArena(transform.position);

            if (spriteRenderer != null && Mathf.Abs(moveDir.x) > 0.05f)
            {
                spriteRenderer.flipX = moveDir.x < 0f;
            }

            yield return null;
        }

        // 5. O pássaro pousa e roda a animação de casulo ao contrário (voltando a ser humano)
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.35f, 0.1f, 0.55f), 2.0f);
        }

        isTransformedInBird = false;
        transform.localScale = new Vector3(1.4f, 1.4f, 1f);

        if (animator != null) animator.SetTrigger(TransformOutHash);

        yield return new WaitForSeconds(0.60f);

        if (bodyCollider != null) bodyCollider.enabled = true;
        
        // Encerra o áudio da transformação exatamente no momento em que os pássaros partem para o ataque
        StopIllusionTransformSFX();

        // 6. Todos os outros pássaros mergulham em linha reta no jogador como projéteis de ataque!
        foreach (var bird in illusionBirds)
        {
            if (bird != null)
            {
                bird.LaunchDiveAttack(playerTransform);
            }
        }

        yield return new WaitForSeconds(0.4f);

        attackTimer = attackCooldown * 0.8f;
        isExecutingAction = false;
    }
    #endregion

    #region IDamageable & Dano
    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead || isTransformedInBird) return; // Não toma dano na forma de pássaro!

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        damageSinceLastIllusion += damage;

        Debug.Log($"[MatintaBoss] {bossName} recebeu {damage} de dano! Vida restante: {currentHealth}/{maxHealth}");

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.UpdateHealth(currentHealth, maxHealth);
        }

        if (currentHealth <= 0f)
        {
            isDead = true;
            isCombatActive = false;
            StopAllCoroutines();
            StartCoroutine(DeathRoutine());
            return;
        }

        StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = damageFlashColor;
            yield return new WaitForSeconds(0.12f);
            spriteRenderer.color = orig;
        }
    }

    [Header("Sprites de Morte")]
    [SerializeField] private Sprite[] deathSprites;

    private IEnumerator DeathRoutine()
    {
        isDead = true;
        isCombatActive = false;
        StopMovement();

        Debug.Log($"[MatintaBoss] {bossName} FOI DERROTADA! Entrando no casulo e sumindo...");

        if (bodyCollider != null) bodyCollider.enabled = false;
        if (agent != null) agent.enabled = false;

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.HideBoss(true);
        }

        // Garante escala humana correta e visibilidade ativa
        transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = Color.white;
        }

        // Se tiver o array de sprites de morte, executa frame a frame no SpriteRenderer com precisão absoluta
        if (deathSprites != null && deathSprites.Length > 0 && spriteRenderer != null)
        {
            if (animator != null) animator.enabled = false; // Desativa o animator para não sobrescrever os frames

            for (int i = 0; i < deathSprites.Length; i++)
            {
                if (deathSprites[i] != null)
                {
                    spriteRenderer.sprite = deathSprites[i];
                }
                yield return new WaitForSeconds(0.22f);
            }
        }
        else
        {
            // Fallback via Animator
            if (animator != null)
            {
                animator.Play("Death", 0, 0f);
                animator.Update(0f);
            }
            yield return new WaitForSeconds(0.85f);
        }

        // Efeito mágico de fumaça e penas ao sumir
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.35f, 0.05f, 0.5f), 2.8f);
        }

        if (spriteRenderer != null) spriteRenderer.enabled = false;

        // Mata todos os lacaios restantes acionando a animação de morte e efeito deles!
        foreach (var m in activeMinions)
        {
            if (m != null)
            {
                if (m.TryGetComponent(out IDamageable dmg))
                {
                    dmg.TakeDamage(9999f, Vector3.zero);
                }
                else
                {
                    Destroy(m, 0.5f);
                }
            }
        }
        activeMinions.Clear();

        AudioController.Instance.PlaySFX(deathSFX);

        // Drop de Estrelas (4 a 6 estrelas)
        int drops = Random.Range(4, 7);
        for (int i = 0; i < drops; i++)
        {
            Vector3 dropPos = ClampToArena(transform.position + (Vector3)(Random.insideUnitCircle * 1.5f));
            StarPickup.SpawnStar(dropPos, starPickupPrefab);
        }

        // Mãe do Ouro surge onde o boss foi derrotado
        MaeDoOuroBossRewardNPC.SpawnAfterBoss(transform.position, BossDefeatedType.Matinta);

        yield return new WaitForSeconds(0.25f);

        Destroy(gameObject);
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.2f, 0.9f, 0.4f);
        Vector3 center = useSpawnPositionAsArenaCenter && !Application.isPlaying ? transform.position : arenaCenter;
        Gizmos.DrawWireSphere(center, arenaRadius);
    }
}
