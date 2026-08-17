using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gerenciador de Fases / Níveis do Jogo (Roguelike Stage Manager).
/// Controla em qual fase o jogador está e calcula multiplicadores de escalonamento (Vida, Dano e Velocidade)
/// para deixar os inimigos mais fortes proporcionalmente à fase do jogador.
/// </summary>
public class StageManager : MonoBehaviour
{
    private static StageManager instance;

    public static StageManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<StageManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("[StageManager]");
                    instance = obj.AddComponent<StageManager>();
                }
            }
            return instance;
        }
    }

    [Header("Configuração de Fases")]
    [Tooltip("Fase / Nível atual em que o jogador se encontra.")]
    [SerializeField] private int currentStage = 1;

    [Header("Escalonamento de Dificuldade dos Inimigos (Roguelike)")]
    [Tooltip("Aumento percentual de vida dos inimigos por fase (ex: 0.25 = +25% de vida por fase).")]
    [SerializeField] private float healthGrowthPerStage = 0.25f;

    [Tooltip("Aumento percentual de dano dos inimigos por fase (ex: 0.15 = +15% de dano por fase).")]
    [SerializeField] private float damageGrowthPerStage = 0.15f;

    [Tooltip("Aumento percentual de velocidade dos inimigos por fase (ex: 0.04 = +4% por fase, máx +50%).")]
    [SerializeField] private float speedGrowthPerStage = 0.04f;

    [Header("Atalhos de Debug (Testes)")]
    [Tooltip("Se verdadeiro, permite pressionar N ou '+' para avançar de fase e '-' para voltar.")]
    [SerializeField] private bool enableDebugShortcuts = true;

    public int CurrentStage => currentStage;

    public event Action<int> OnStageChanged; // (newStage)

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        OnStageChanged?.Invoke(currentStage);
        ApplyScalingToAllActiveEnemies();
    }

    private void Update()
    {
        if (!enableDebugShortcuts || Keyboard.current == null) return;

        // Atalhos de teclado para teste rápido de mudança de fases no Unity Editor
        if (Keyboard.current.nKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame || Keyboard.current.equalsKey.wasPressedThisFrame)
        {
            NextStage();
        }
        else if (Keyboard.current.numpadMinusKey.wasPressedThisFrame || Keyboard.current.minusKey.wasPressedThisFrame)
        {
            if (currentStage > 1) SetStage(currentStage - 1);
        }
    }

    /// <summary>
    /// Avança para a próxima fase e atualiza os inimigos e a UI.
    /// </summary>
    public void NextStage()
    {
        SetStage(currentStage + 1);
    }

    /// <summary>
    /// Define a fase atual do jogador e atualiza o escalonamento de todos os inimigos.
    /// </summary>
    public void SetStage(int newStage)
    {
        if (newStage < 1) newStage = 1;
        currentStage = newStage;

        Debug.Log($"<color=#FF8800>[StageManager] Jogador avançou para a FASE {currentStage}!</color> " +
                  $"(Multiplicador Vida: {GetHealthMultiplier(currentStage):F2}x, Dano: {GetDamageMultiplier(currentStage):F2}x)");

        OnStageChanged?.Invoke(currentStage);

        // Aplica o escalonamento da nova fase em todos os inimigos ativos imediatamente
        ApplyScalingToAllActiveEnemies();
    }

    /// <summary>
    /// Reinicia o progresso para a Fase 1 (ex: quando o player morre e reinicia o jogo).
    /// </summary>
    public void ResetToStage1()
    {
        SetStage(1);
    }

    /// <summary>
    /// Retorna o multiplicador de vida dos inimigos para uma determinada fase.
    /// </summary>
    public float GetHealthMultiplier(int stage)
    {
        return 1f + (stage - 1) * healthGrowthPerStage;
    }

    /// <summary>
    /// Retorna o multiplicador de dano dos inimigos para uma determinada fase.
    /// </summary>
    public float GetDamageMultiplier(int stage)
    {
        return 1f + (stage - 1) * damageGrowthPerStage;
    }

    /// <summary>
    /// Retorna o multiplicador de velocidade dos inimigos para uma determinada fase (com limite em 1.5x).
    /// </summary>
    public float GetSpeedMultiplier(int stage)
    {
        return Mathf.Min(1.5f, 1f + (stage - 1) * speedGrowthPerStage);
    }

    /// <summary>
    /// Varre e atualiza a força de todos os inimigos ativos na cena.
    /// </summary>
    public void ApplyScalingToAllActiveEnemies()
    {
        EnemyAIController[] activeEnemies = UnityEngine.Object.FindObjectsByType<EnemyAIController>(FindObjectsInactive.Exclude);
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.ApplyStageScaling(currentStage);
            }
        }
    }
}

