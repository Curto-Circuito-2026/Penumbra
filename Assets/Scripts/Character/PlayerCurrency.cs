using System;
using UnityEngine;

/// <summary>
/// Gerencia a economia e as moedas do jogador:
/// - Fragmentos de Estrela (moeda temporária coletada durante a fase/run).
/// - Estrelas (moeda principal obtida por conversão).
/// Preserva o saldo entre transições de cenas durante a mesma partida/execução.
/// Ao fechar ou reiniciar o jogo, recomeça do zero para o modo Demo One-Shot.
/// </summary>
public class PlayerCurrency : MonoBehaviour
{
    private static PlayerCurrency _instance;
    public static PlayerCurrency Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = UnityEngine.Object.FindAnyObjectByType<PlayerCurrency>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player == null)
                    {
                        PlayerStats ps = UnityEngine.Object.FindAnyObjectByType<PlayerStats>(FindObjectsInactive.Include);
                        if (ps != null) player = ps.gameObject;
                    }

                    if (player != null)
                    {
                        _instance = player.GetComponent<PlayerCurrency>();
                        if (_instance == null)
                        {
                            _instance = player.AddComponent<PlayerCurrency>();
                            Debug.Log("[PlayerCurrency] Componente PlayerCurrency anexado dinamicamente ao jogador.");
                        }
                    }
                }
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    // Memória estática para persistência imediata entre fases/cenas durante a mesma partida (One-Shot Run)
    private static int staticRunFragments = 0;
    private static int staticTotalStars = 0;
    private static bool isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticSessionData()
    {
        staticRunFragments = 0;
        staticTotalStars = 0;
        isInitialized = false;
        _instance = null;
    }

    [Header("Moedas")]
    [Tooltip("Quantidade atual de fragmentos de estrela coletados nesta run.")]
    [SerializeField] private int starFragments = 0;

    [Tooltip("Quantidade total de estrelas conquistadas nesta partida.")]
    [SerializeField] private int stars = 0;

    [Header("Taxa de Conversão")]
    [Tooltip("Quantidade de fragmentos necessários para forjar 1 estrela.")]
    [SerializeField] private int fragmentsPerStar = 10;

    [Header("Persistência em Disco")]
    [Tooltip("Se verdadeiro, salva o saldo total de estrelas no PlayerPrefs.")]
    [SerializeField] private bool persistStars = true;
    private const string STARS_PREFS_KEY = "PLAYER_STARS_TOTAL";

    private PlayerStats playerStats;

    public int StarFragments => starFragments;
    public int Stars => stars;
    public int FragmentsPerStar => fragmentsPerStar;


    [SerializeField] RunManager runManager;

    // Eventos para atualização de interface e efeitos
    public event Action<int> OnStarFragmentsChanged;
    public event Action<int> OnStarsChanged;
    public event Action<int, int> OnFragmentsConverted; // (fragmentsConverted, starsGained)

    private void Awake()
    {
        _instance = this;
        playerStats = GetComponent<PlayerStats>();

        // Na primeira inicialização da sessão
        if (!isInitialized)
        {
            if (persistStars && PlayerPrefs.HasKey(STARS_PREFS_KEY))
            {
                staticTotalStars = PlayerPrefs.GetInt(STARS_PREFS_KEY, 0);
            }
            else
            {
                staticTotalStars = stars;
            }
            staticRunFragments = starFragments;
            isInitialized = true;
        }

        // Sincroniza os saldos desta instância com a memória entre cenas
        starFragments = staticRunFragments;
        stars = staticTotalStars;
    }

    private void OnEnable()
    {
        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        if (playerStats != null)
        {
            playerStats.OnPlayerDied += HandlePlayerDied;
        }
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnPlayerDied -= HandlePlayerDied;
        }
    }

    private void Start()
    {
        // Notifica saldos iniciais para a UI da cena recém-carregada
        OnStarFragmentsChanged?.Invoke(starFragments);
        OnStarsChanged?.Invoke(stars);
    }

    /// <summary>
    /// Adiciona fragmentos de estrela à carteira do jogador.
    /// </summary>
    public void AddStarFragments(int amount)
    {
        if (amount <= 0) return;

        starFragments += amount;
        staticRunFragments = starFragments;

        Debug.Log($"[PlayerCurrency] +{amount} Fragmento(s) de Estrela coletado(s). Total: {starFragments}");

        OnStarFragmentsChanged?.Invoke(starFragments);
    }

    /// <summary>
    /// Consome fragmentos de estrela se o jogador possuir quantidade suficiente.
    /// </summary>
    public bool SpendStarFragments(int amount)
    {
        if (amount <= 0) return true;

        if (starFragments >= amount)
        {
            starFragments -= amount;
            staticRunFragments = starFragments;
            OnStarFragmentsChanged?.Invoke(starFragments);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adiciona estrelas ao jogador.
    /// </summary>
    public void AddStars(int amount)
    {
        if (amount <= 0) return;

        stars += amount;
        staticTotalStars = stars;
        SaveStars();

        Debug.Log($"[PlayerCurrency] +{amount} Estrela(s) obtida(s)! Total de Estrelas: {stars}");
        OnStarsChanged?.Invoke(stars);
    }

    /// <summary>
    /// Consome estrelas se o jogador possuir quantidade suficiente.
    /// </summary>
    public bool SpendStars(int amount)
    {
        if (amount <= 0) return true;

        if (stars >= amount)
        {
            stars -= amount;
            staticTotalStars = stars;
            SaveStars();
            OnStarsChanged?.Invoke(stars);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Realiza a conversão de fragmentos em estrelas (10 fragmentos = 1 estrela).
    /// </summary>
    public void ConvertFragmentsToStars()
    {
        if (starFragments < fragmentsPerStar)
        {
            Debug.Log($"[PlayerCurrency] Fragmentos insuficientes para conversão ({starFragments}/{fragmentsPerStar}).");
            return;
        }

        int starsToGain = starFragments / fragmentsPerStar;
        int fragmentsSpent = starsToGain * fragmentsPerStar;

        starFragments -= fragmentsSpent;
        stars += starsToGain;

        staticRunFragments = starFragments;
        staticTotalStars = stars;
        SaveStars();

        Debug.Log($"[PlayerCurrency] Conversão concluída! {fragmentsSpent} fragmentos convertidos em {starsToGain} estrela(s). Saldo: {starFragments} frag, {stars} estrelas.");

        // Feedback visual flutuante se disponível
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.SpawnFloatingText(
                transform.position + Vector3.up * 1.2f,
                $"+{starsToGain} Estrela(s)!",
                new Color(1f, 0.9f, 0.2f),
                4.5f
            );
        }

        OnStarFragmentsChanged?.Invoke(starFragments);
        OnStarsChanged?.Invoke(stars);
        OnFragmentsConverted?.Invoke(fragmentsSpent, starsToGain);
    }

    /// <summary>
    /// Reinicia os fragmentos e estrelas da partida (útil para reiniciar demo/run).
    /// </summary>
    public static void ResetSession()
    {
        staticRunFragments = 0;
        staticTotalStars = 0;
        if (Instance != null)
        {
            Instance.starFragments = 0;
            Instance.stars = 0;
            Instance.OnStarFragmentsChanged?.Invoke(0);
            Instance.OnStarsChanged?.Invoke(0);
        }
    }

    /// <summary>
    /// Handler invocado automaticamente quando o jogador morre.
    /// </summary>
    private void HandlePlayerDied()
    {
        CameraManager camManager = Camera.main != null ? Camera.main.GetComponent<CameraManager>() : null;
        if (camManager != null) { camManager.SetTarget(this.transform); Camera.main.orthographicSize = 5f; }
        Debug.Log("[PlayerCurrency] Jogador morreu! Processando conversão de fragmentos de estrela para estrelas...");
        
        int frags = staticRunFragments;
        int prevStars = stars;
        ConvertFragmentsToStars();
        int starsGained = stars - prevStars;

        if (runManager == null) runManager = RunManager.Instance ?? FindAnyObjectByType<RunManager>();
        if (runManager != null)
        {
            runManager.ShowDeathScreen(frags, starsGained, stars);
        }
    }

    private void SaveStars()
    {
        if (persistStars)
        {
            PlayerPrefs.SetInt(STARS_PREFS_KEY, stars);
            PlayerPrefs.Save();
        }
    }
}
