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

    private void FindBossReference()
    {
        if (bossController == null)
        {
            if (transform.parent != null)
            {
                bossController = transform.parent.GetComponentInChildren<BoitataBossController>();
            }

            if (bossController == null)
            {
                bossController = Object.FindAnyObjectByType<BoitataBossController>();
            }
        }

        if (bossStats == null && bossController != null)
        {
            bossStats = bossController.GetComponent<EnemyStats>();
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
            gameObject.SetActive(false);
        }
    }
}
