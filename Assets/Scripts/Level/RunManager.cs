using PrimeTween;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

[Serializable]
public struct Region
{
    public string title;
    public string subtitle;
#if UNITY_EDITOR
    public SceneAsset scene;
#endif
    public string sceneName;
    public Vector2 spawnPoint;
    [Tooltip("Música de fundo da fase/bioma.")]
    public AudioClip stageBgm;
    [Tooltip("Música de batalha do Boss desta fase.")]
    public AudioClip bossBgm;
}

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }
    [SerializeField] List<Region> regions;
    [SerializeField] CinematicManager cinematicManager;
    [SerializeField] SceneController sceneController;
    [SerializeField] GameStateManager gameStateManager;
    [SerializeField] GameObject startRunScreen;
    [SerializeField] GameObject deathScreen;
    [SerializeField] TMP_Text starsText;
    [SerializeField] PlayerStats playerStats;

    [Header("Áudio Geral da Run")]
    [Tooltip("Música de fundo a ser tocada no Hub (A Terra Sem Males).")]
    [SerializeField] private AudioClip hubBgm;

    [Header("Modo de Teste / Mock da Run")]
    [Tooltip("Se marcado, usa a ordem de teste fixada abaixo em vez de sortear aleatoriamente.")]
    [SerializeField] private bool useFixedTestOrder = false;

    [Tooltip("Ordem fixa para teste: 0 = Boitatá (Mata Atlântica), 1 = Mapinguari (Cidade), 2 = Matinta (Pântano), 3 = Cuca (Final).")]
    [SerializeField] private List<int> testRegionOrder = new List<int> { 0, 1, 2, 3 };

    [Tooltip("Se for 0, 1, 2 ou 3, força ir direto para essa fase específica (-1 = desativado).")]
    [Range(-1, 3)]
    [SerializeField] private int forceSingleRegion = -1;

    [Header("Fila de Regiões Pendentes da Run Atual")]
    [Tooltip("Lista de regiões que ainda precisam ser jogadas nesta run. A primeira da lista é a fase atual.")]
    [SerializeField] private List<int> pendingRegions = new List<int>();

    public List<int> PendingRegions => pendingRegions;
    public int CurrentRegion => pendingRegions != null && pendingRegions.Count > 0 ? pendingRegions[0] : 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }

        cinematicManager = GameObject.Find("CinematicManager") != null ? GameObject.Find("CinematicManager").GetComponent<CinematicManager>() : (CinematicManager.Instance ?? FindAnyObjectByType<CinematicManager>());
        gameStateManager = GameObject.Find("GameStateManager") != null ? GameObject.Find("GameStateManager").GetComponent<GameStateManager>() : (GameStateManager.Instance ?? FindAnyObjectByType<GameStateManager>());
    }

    private void Start()
    {
        sceneController = FindAnyObjectByType<SceneController>();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (startRunScreen != null) startRunScreen.SetActive(false);
        StartCoroutine(SceneLoadCoroutine(scene.name));
    }

    private IEnumerator SceneLoadCoroutine(string sceneName)
    {
        string title = "";
        string subTitle = "";

        if (sceneName == "Hub")
        {
            while (gameStateManager != null && gameStateManager.CurrentState != GameState.Playing)
            {
                yield return null;
            }

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
            }

            if (playerStats != null)
            {
                playerStats.RestartPlayer();

                GameObject spawn = GameObject.Find("PlayerSpawnPoint") ?? GameObject.Find("SpawnPoint") ?? GameObject.Find("Player_Spawn");
                if (spawn != null)
                {
                    playerStats.transform.position = new Vector3(spawn.transform.position.x, spawn.transform.position.y, playerStats.transform.position.z);
                }
                else
                {
                    playerStats.transform.position = new Vector3(0.35f, -0.14f, playerStats.transform.position.z);
                }

                if (Camera.main != null)
                {
                    CameraManager cam = Camera.main.GetComponent<CameraManager>();
                    if (cam != null) cam.SetTarget(playerStats.transform);
                }
            }

            title = "A Terra Sem Males";
            subTitle = "Yby Marã E'Yma";
        }
        else
        {
            // 1. Procura ponto de spawn específico na cena recém-carregada
            GameObject spawn = GameObject.Find("PlayerSpawnPoint") ?? GameObject.Find("SpawnPoint") ?? GameObject.Find("Player_Spawn");
            if (spawn != null && playerStats != null)
            {
                playerStats.transform.position = new Vector3(spawn.transform.position.x, spawn.transform.position.y, playerStats.transform.position.z);
            }
            else if (playerStats != null)
            {
                // 2. Fallbacks de posicionamento se a coordenada estiver em (0, 0)
                if (sceneName.Contains("Mata") && (playerStats.transform.position.x < 300f || playerStats.transform.position == Vector3.zero))
                {
                    playerStats.transform.position = new Vector3(460f, 276f, playerStats.transform.position.z);
                }
                else if ((sceneName.Contains("Cidade") || sceneName.Contains("Destruida")) && (playerStats.transform.position.x < 300f || playerStats.transform.position == Vector3.zero))
                {
                    playerStats.transform.position = new Vector3(520.94f, 283.02f, playerStats.transform.position.z);
                }
                else if (sceneName.Contains("Pantano") && (playerStats.transform.position.x < 300f || playerStats.transform.position == Vector3.zero))
                {
                    playerStats.transform.position = new Vector3(525f, 280f, playerStats.transform.position.z);
                }
            }

            // Define títulos cinematográficos correspondentes à cena carregada
            for (int i = 0; i < (regions != null ? regions.Count : 0); i++)
            {
                if (GetSceneName(i) == sceneName)
                {
                    title = regions[i].title;
                    subTitle = regions[i].subtitle;
                    break;
                }
            }
        }

        if (cinematicManager == null) cinematicManager = CinematicManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<CinematicManager>();
        if (cinematicManager != null)
        {
            cinematicManager.ShowTitle(title, subTitle, true);
        }

        // Toca a música correspondente à cena carregada (Hub ou Fase)
        PlaySceneBGM(sceneName);
    }

    /// <summary>
    /// Toca a música da cena atual (Hub ou Fase/Bioma) com crossfade suave.
    /// </summary>
    public void PlaySceneBGM(string sceneName)
    {
        if (AudioController.Instance == null) return;

        if (sceneName == "Hub" || sceneName.Contains("Hub"))
        {
            AudioClip clip = hubBgm;
#if UNITY_EDITOR
            if (clip == null) clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Menu.mp3");
#endif
            if (clip != null) AudioController.Instance.PlayBGM(clip, fadeDuration: 1.2f, loop: true);
            return;
        }

        // Procura na lista de regiões do Inspector
        for (int i = 0; i < (regions != null ? regions.Count : 0); i++)
        {
            if (GetSceneName(i) == sceneName)
            {
                if (regions[i].stageBgm != null)
                {
                    AudioController.Instance.PlayBGM(regions[i].stageBgm, fadeDuration: 1.2f, loop: true);
                    return;
                }
                break;
            }
        }

        // Fallbacks automáticos conforme o bioma corrigido:
        // 1 = Pântano (Stage 1.mp3)
        // 2 = Mata Atlântica (Stage 2.mp3)
        // 3 = Cidade Destruída (Stage 3.mp3)
        AudioClip fallbackClip = null;
#if UNITY_EDITOR
        if (sceneName.Contains("Pantano"))
        {
            fallbackClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Stages/Stage 1.mp3");
        }
        else if (sceneName.Contains("Mata"))
        {
            fallbackClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Stages/Stage 2.mp3");
        }
        else if (sceneName.Contains("Cidade") || sceneName.Contains("Destruida"))
        {
            fallbackClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Stages/Stage 3.mp3");
        }
        else if (sceneName.Contains("Cuca") || sceneName.Contains("Covil"))
        {
            fallbackClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Boss/Boss 4.mp3");
        }
#endif
        if (fallbackClip != null)
        {
            AudioController.Instance.PlayBGM(fallbackClip, fadeDuration: 1.2f, loop: true);
        }
    }

    /// <summary>
    /// Toca a música da loja correspondente ao bioma/cena atual.
    /// 1 = Pântano (Lojinha 1.mp3)
    /// 2 = Mata Atlântica (Lojinha 2.mp3)
    /// 3 = Cidade Destruída (Lojinha 3.mp3)
    /// 4 = Hub (Lojinha 4.mp3)
    /// </summary>
    public void PlayShopBGM(string sceneName = null)
    {
        if (AudioController.Instance == null) return;
        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = SceneManager.GetActiveScene().name;
        }

        AudioClip shopClip = null;
#if UNITY_EDITOR
        if (sceneName == "Hub" || sceneName.Contains("Hub"))
        {
            shopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Loja/Lojinha 4.mp3");
        }
        else if (sceneName.Contains("Pantano"))
        {
            shopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Loja/Lojinha 1.mp3");
        }
        else if (sceneName.Contains("Mata"))
        {
            shopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Loja/Lojinha 2.mp3");
        }
        else if (sceneName.Contains("Cidade") || sceneName.Contains("Destruida"))
        {
            shopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Loja/Lojinha 3.mp3");
        }
#endif
        if (shopClip != null)
        {
            AudioController.Instance.PlayBGM(shopClip, fadeDuration: 1.2f, loop: true);
        }
    }

    public void ShowStartRunScreen()
    {
        if (startRunScreen != null)
        {
            startRunScreen.gameObject.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
            startRunScreen.SetActive(true);
        }
    }

    public void CloseStartRunScreen()
    {
        if (startRunScreen != null) startRunScreen.SetActive(false);
    }

    public void ShowDeathScreen(int starFragments, int starsGained, int totalStars)
    {
        if (starsText != null)
        {
            starsText.text = $"Seu{(starFragments == 1 ? "" : "s")} {starFragments} fragmento{(starFragments == 1 ? "" : "s")} de estrela\r\n{(starFragments == 1 ? "Foi" : "Foram")} convertido{(starFragments == 1 ? "" : "s")} em\r\n{starsGained} estrela{(starsGained == 1 ? "" : "s")}\r\n\r\n<size=80%><color=#FFD700>Total na Carteira: {totalStars} ★</color></size>";
        }
        if (deathScreen != null) deathScreen.SetActive(true);
    }

    public void ShowDeathScreen(int starFragments)
    {
        int totalStars = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Stars : PlayerPrefs.GetInt("PLAYER_STARS_TOTAL", 0);
        int rate = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.FragmentsPerStar : 10;
        int starsGained = starFragments / (rate > 0 ? rate : 10);
        ShowDeathScreen(starFragments, starsGained, totalStars);
    }

    /// <summary>
    /// Chamado ao morrer na fase e confirmar na tela de morte.
    /// Retorna para o Hub no SpawnPoint, preservando a lista de fases pendentes
    /// para que o jogador continue tentando a fase onde morreu.
    /// </summary>
    public void Restart()
    {
        if (deathScreen != null) deathScreen.SetActive(false);
        StartCoroutine(RestartCoroutine());
    }

    /// <summary>
    /// Retorna para o Céu (Hub) no SpawnPoint.
    /// </summary>
    public void ReturnToHub()
    {
        if (deathScreen != null) deathScreen.SetActive(false);
        StartCoroutine(RestartCoroutine());
    }

    private IEnumerator RestartCoroutine()
    {
        // Fade out suave da música atual antes de voltar ao Hub
        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopBGM(fadeDuration: 0.8f);
        }

        if (sceneController == null) sceneController = FindAnyObjectByType<SceneController>();
        if (sceneController != null)
        {
            sceneController.LoadScene("Hub", TransitionType.CrossFade);
        }
        else
        {
            SceneManager.LoadScene("Hub");
        }
        yield return new WaitForSeconds(0.3f);

        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.RestartPlayer();
            GameObject spawn = GameObject.Find("PlayerSpawnPoint") ?? GameObject.Find("SpawnPoint") ?? GameObject.Find("Player_Spawn");
            if (spawn != null)
            {
                playerStats.transform.position = new Vector3(spawn.transform.position.x, spawn.transform.position.y, playerStats.transform.position.z);
            }
            else
            {
                playerStats.transform.position = new Vector3(0.35f, -0.14f, playerStats.transform.position.z);
            }

            if (Camera.main != null)
            {
                CameraManager cam = Camera.main.GetComponent<CameraManager>();
                if (cam != null) cam.SetTarget(playerStats.transform);
            }
        }
        yield return null;
    }

    /// <summary>
    /// Inicia ou continua a Run a partir do Hub.
    /// Se não houver fila ativa, faz o sorteio sem repetição.
    /// Se houver fila ativa (ex: morreu e voltou ao Hub), continua na fase onde parou.
    /// </summary>
    public void StartRun()
    {
        EnsurePendingRegionsQueue();

        PlayerCombatController combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        if (combat != null)
        {
            combat.SaveStageCheckpoint();
        }

        if (startRunScreen != null)
        {
            Tween.UIAnchoredPositionX(startRunScreen.GetComponent<RectTransform>(), -1920f, 1f, Ease.InOutSine).OnComplete(() =>
            {
                LoadCurrentPendingRegion();
            });
        }
        else
        {
            LoadCurrentPendingRegion();
        }
    }

    /// <summary>
    /// Garante que a lista de regiões pendentes esteja criada se estiver vazia.
    /// </summary>
    private void EnsurePendingRegionsQueue()
    {
        if (pendingRegions == null || pendingRegions.Count == 0)
        {
            if (forceSingleRegion >= 0 && regions != null && forceSingleRegion < regions.Count)
            {
                pendingRegions = new List<int> { forceSingleRegion };
            }
            else if (useFixedTestOrder && testRegionOrder != null && testRegionOrder.Count > 0)
            {
                pendingRegions = new List<int>(testRegionOrder);
            }
            else if (regions != null && regions.Count > 1)
            {
                // Sorteia dinamicamente todas as regiões normais cadastradas (todas menos a última)
                List<int> baseRegions = Enumerable.Range(0, regions.Count - 1).ToList();
                pendingRegions = baseRegions.OrderBy(x => Random.value).ToList();

                // A última região da lista do Inspector (ex: Cuca) é sempre a batalha final
                pendingRegions.Add(regions.Count - 1);
            }
            else
            {
                // Fallback de segurança se a lista regions não estiver preenchida no Inspector
                int[] fallbackRegions = { 0, 1, 2 };
                pendingRegions = fallbackRegions.OrderBy(x => Random.value).ToList();
                pendingRegions.Add(3);
            }

            Debug.Log($"[RunManager] Nova Fila de Run sorteada: [{string.Join(" -> ", pendingRegions)}]");
        }
        else
        {
            Debug.Log($"[RunManager] Continuando Fila existente: [{string.Join(" -> ", pendingRegions)}]. Próxima fase: {pendingRegions[0]}");
        }
    }

    /// <summary>
    /// Chamado pela Mãe do Ouro após derrotar o Boss.
    /// Remove a fase concluída da lista e:
    /// - Se ainda houver fases restantes: teletransporta DIRETO para a próxima fase (sem ir pro Céu).
    /// - Se concluiu a última fase (Cuca): finaliza a jornada e retorna ao Céu com a lista resetada.
    /// </summary>
    public void AdvanceToNextRegionOrFinish()
    {
        // Salva as habilidades e bênçãos adquiridas na fase que acabou de ser concluída com sucesso
        PlayerCombatController combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        if (combat != null)
        {
            combat.SaveStageCheckpoint();
        }

        if (pendingRegions != null && pendingRegions.Count > 0)
        {
            int completedRegion = pendingRegions[0];
            pendingRegions.RemoveAt(0);
            Debug.Log($"[RunManager] Fase {completedRegion} concluída e removida da lista! Fases restantes: {pendingRegions.Count} [{string.Join(", ", pendingRegions)}]");
        }

        if (pendingRegions != null && pendingRegions.Count > 0)
        {
            // Vai DIRETO para a próxima fase!
            int nextRegion = pendingRegions[0];
            string nextScene = GetSceneName(nextRegion);
            Debug.Log($"[RunManager] Avançando diretamente para a próxima fase: {nextScene} (Região {nextRegion})");

            if (sceneController == null) sceneController = FindAnyObjectByType<SceneController>();
            if (sceneController != null)
            {
                sceneController.LoadScene(nextScene, TransitionType.CrossFade);
            }
            else
            {
                SceneManager.LoadScene(nextScene);
            }
        }
        else
        {
            // Todas as fases foram concluídas (Cuca derrotada)!
            Debug.Log("[RunManager] Todas as fases foram concluídas com vitória total! Retornando ao Céu.");
            pendingRegions = new List<int>(); // Reseta para uma nova run
            ReturnToHub();
        }
    }

    /// <summary>
    /// Carrega a fase que está atualmente no topo da lista pendente (pendingRegions[0]).
    /// </summary>
    public void LoadCurrentPendingRegion(bool first = false)
    {
        EnsurePendingRegionsQueue();

        int targetRegion = pendingRegions[0];
        string sceneToLoad = GetSceneName(targetRegion);

        if (regions != null && targetRegion < regions.Count && regions[targetRegion].spawnPoint != Vector2.zero && playerStats != null)
        {
            playerStats.transform.position = new Vector3(regions[targetRegion].spawnPoint.x, regions[targetRegion].spawnPoint.y, playerStats.transform.position.z);
        }

        if (sceneController == null) sceneController = FindAnyObjectByType<SceneController>();
        if (sceneController != null && !string.IsNullOrEmpty(sceneToLoad))
        {
            sceneController.LoadScene(sceneToLoad, !first ? TransitionType.CrossFade : TransitionType.None);
        }
        else if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    public void PassRegion(bool first = false)
    {
        LoadCurrentPendingRegion(first);
    }

    #region Atalhos de Teste no Inspector (Botão Direito no Componente)
    [ContextMenu("Testar: Ir Direto para Boitatá (Mata Atlântica)")]
    public void TestJumpToBoitata() => JumpToRegionForTest(0);

    [ContextMenu("Testar: Ir Direto para Mapinguari (Cidade Destruída)")]
    public void TestJumpToMapinguari() => JumpToRegionForTest(1);

    [ContextMenu("Testar: Ir Direto para Matinta Perera (Pântano)")]
    public void TestJumpToMatinta() => JumpToRegionForTest(2);

    [ContextMenu("Testar: Ir Direto para Cuca (Covil Final)")]
    public void TestJumpToCuca() => JumpToRegionForTest(3);

    private void JumpToRegionForTest(int index)
    {
        pendingRegions = new List<int> { index };
        LoadCurrentPendingRegion();
    }
    #endregion

    public string GetSceneName(int regionIndex)
    {
        if (regions == null || regionIndex < 0 || regionIndex >= regions.Count) return "";
        var reg = regions[regionIndex];
        if (!string.IsNullOrEmpty(reg.sceneName)) return reg.sceneName;

#if UNITY_EDITOR
        try
        {
            if (reg.scene != null) return reg.scene.name;
        }
        catch { }
#endif

        if (!string.IsNullOrEmpty(reg.title))
        {
            if (reg.title.Contains("Floresta") || reg.title.Contains("Mata")) return "Mata Atlantica";
            if (reg.title.Contains("Cidade") || reg.title.Contains("Destruida")) return "Cidade Destruida";
            if (reg.title.Contains("Pantano")) return "Pantano";
            if (reg.title.Contains("Cuca") || reg.title.Contains("Covil")) return "CucasLair";
        }

        switch (regionIndex)
        {
            case 0: return "Mata Atlantica";
            case 1: return "Cidade Destruida";
            case 2: return "Pantano";
            case 3: return "CucasLair";
            default: return "Mata Atlantica";
        }
    }
}
