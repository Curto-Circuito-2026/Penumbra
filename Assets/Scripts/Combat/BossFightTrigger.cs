using UnityEngine;

/// <summary>
/// Gatilho de área 2D (Trigger) que inicia o combate contra um Boss ao detectar a entrada do Player.
/// Ativa a Barra de Vida do Boss (BossHealthBarUI) e o BossController correspondente.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossFightTrigger : MonoBehaviour
{
    [Header("Identificação do Boss")]
    [Tooltip("Nome que será exibido na Barra de Vida Épica do Topo.")]
    [SerializeField] private string bossDisplayName = "BOITATÁ - Serpente de Fogo";

    [Tooltip("Referência ao BoitataBossController na cena (opcional - se nulo, busca automaticamente).")]
    [SerializeField] private BoitataBossController bossController;

    [Tooltip("Referência ao MapinguariBossController na cena (opcional - se nulo, busca automaticamente).")]
    [SerializeField] private MapinguariBossController mapinguariController;

    [Tooltip("Referência ao CucaBossController na cena (opcional - se nulo, busca automaticamente).")]
    [SerializeField] private CucaBossController cucaController;

    [Tooltip("Referência ao MatintaBossController na cena (opcional - se nulo, busca automaticamente).")]
    [SerializeField] private MatintaBossController matintaController;

    [Tooltip("Referência ao EnemyStats do Boss (opcional - se for outro tipo de boss).")]
    [SerializeField] private EnemyStats bossStats;

    [Header("Configurações do Gatilho")]
    [Tooltip("Se verdadeiro, o gatilho é destruído ou desativado após o primeiro disparo.")]
    [SerializeField] private bool triggerOnce = true;

    [Tooltip("Toca o rugido ou efeito de câmera na ativação.")]
    [SerializeField] private bool shakeCameraOnStart = true;

    private bool hasTriggered = false;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        FindBossReference();
    }

    private void OnEnable()
    {
        PlayerStats.OnAnyPlayerRespawned += HandlePlayerRespawned;
    }

    private void OnDisable()
    {
        PlayerStats.OnAnyPlayerRespawned -= HandlePlayerRespawned;
    }

    private void HandlePlayerRespawned()
    {
        FindBossReference();
        bool isDead = (bossController != null && bossController.IsDead) ||
                      (mapinguariController != null && mapinguariController.IsDead) ||
                      (cucaController != null && cucaController.IsDead) ||
                      (matintaController != null && matintaController.IsDead) ||
                      (bossStats != null && bossStats.CurrentHealth <= 0);

        if (!isDead)
        {
            hasTriggered = false;
        }
    }

    private void FindBossReference()
    {
        if (bossController == null && transform.parent != null)
        {
            bossController = transform.parent.GetComponentInChildren<BoitataBossController>();
        }
        if (bossController == null)
        {
            bossController = Object.FindAnyObjectByType<BoitataBossController>();
        }

        if (mapinguariController == null && transform.parent != null)
        {
            mapinguariController = transform.parent.GetComponentInChildren<MapinguariBossController>();
        }
        if (mapinguariController == null)
        {
            mapinguariController = Object.FindAnyObjectByType<MapinguariBossController>();
        }

        if (cucaController == null && transform.parent != null)
        {
            cucaController = transform.parent.GetComponentInChildren<CucaBossController>();
        }
        if (cucaController == null)
        {
            cucaController = Object.FindAnyObjectByType<CucaBossController>();
        }

        if (matintaController == null && transform.parent != null)
        {
            matintaController = transform.parent.GetComponentInChildren<MatintaBossController>();
        }
        if (matintaController == null)
        {
            matintaController = Object.FindAnyObjectByType<MatintaBossController>();
        }

        if (bossStats == null)
        {
            if (bossController != null)
            {
                bossStats = bossController.GetComponent<EnemyStats>();
            }
            else if (mapinguariController != null)
            {
                bossStats = mapinguariController.GetComponent<EnemyStats>();
            }
            else if (cucaController != null)
            {
                bossStats = cucaController.GetComponent<EnemyStats>();
            }
            else if (matintaController != null)
            {
                bossStats = matintaController.GetComponent<EnemyStats>();
            }

            if (bossStats == null && transform.parent != null)
            {
                bossStats = transform.parent.GetComponentInChildren<EnemyStats>();
            }
            if (bossStats == null)
            {
                bossStats = Object.FindAnyObjectByType<EnemyStats>();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player") || other.GetComponent<CharacterController2D>() != null || other.GetComponentInParent<CharacterController2D>() != null)
        {
            TriggerBossFight();
        }
    }

    public void TriggerBossFight()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        FindBossReference();

        if (bossController != null)
        {
            bossController.StartCombat();
        }
        else if (mapinguariController != null)
        {
            mapinguariController.StartCombat();
        }
        else if (cucaController != null)
        {
            cucaController.StartCombat();
        }
        else if (matintaController != null)
        {
            matintaController.StartCombat();
        }
        else if (bossStats != null)
        {
            float maxHp = bossStats.MaxHealth;
            float currentHp = bossStats.CurrentHealth;

            bossStats.OnHealthChanged += (cur, max) =>
            {
                if (BossHealthBarUI.Instance != null)
                {
                    BossHealthBarUI.Instance.UpdateHealth(cur, max);
                }
            };

            if (BossHealthBarUI.Instance != null)
            {
                BossHealthBarUI.Instance.ShowBoss(bossDisplayName, currentHp, maxHp);
            }
        }
        else
        {
            if (BossHealthBarUI.Instance != null)
            {
                BossHealthBarUI.Instance.ShowBoss(bossDisplayName, 500f, 500f);
            }
        }

        if (shakeCameraOnStart && CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.TriggerCameraShake(0.4f, 0.25f);
        }

        Debug.Log($"[BossFightTrigger] Batalha de Boss iniciada contra '{bossDisplayName}'!");

        if (triggerOnce)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }
}
