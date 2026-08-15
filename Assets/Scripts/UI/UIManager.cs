using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Gerenciador Central de UI (UIManager).
/// Responsável por controlar a visibilidade dos painéis da interface (HUD de Combate/Habilidades,
/// Painel de Diálogo, Menu de Pausa/ESC e Status) de acordo com o estado do jogo (GameState).
/// Pode ser mantido entre cenas (DontDestroyOnLoad).
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager instance;

    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<UIManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("UIManager");
                    instance = obj.AddComponent<UIManager>();
                }
            }
            return instance;
        }
    }

    [Header("Configurações de Persistência entre Cenas")]
    [Tooltip("Se verdadeiro, preserva o Canvas da UI entre carregamentos de cena (DontDestroyOnLoad).")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Painéis Principais da UI")]
    [Tooltip("Painel da HUD de Combate/Habilidades (BottomLeft e BottomRight).")]
    [SerializeField] private GameObject combatHUD;

    [Tooltip("Painel da Caixa de Diálogo.")]
    [SerializeField] private GameObject dialoguePanel;

    [Tooltip("Painel do Menu de Pausa (ESC).")]
    [SerializeField] private GameObject pausePanel;

    [Header("Indicador de Status (Opcional)")]
    [Tooltip("Texto com o estado atual do jogo.")]
    [SerializeField] private TextMeshProUGUI statusText;

    private bool isSubscribed = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureEventSystem();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        if (GameStateManager.Instance != null)
        {
            UpdateUIVisibility(GameStateManager.Instance.CurrentState);
        }
    }

    private void Update()
    {
        // Polling para garantir inscrição se o GameStateManager inicializou depois
        if (!isSubscribed)
        {
            TrySubscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (!isSubscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
            isSubscribed = true;
            UpdateUIVisibility(GameStateManager.Instance.CurrentState);
        }
    }

    private void Unsubscribe()
    {
        if (isSubscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
            isSubscribed = false;
        }
    }

    private void HandleStateChanged(GameState previousState, GameState newState)
    {
        UpdateUIVisibility(newState);
    }

    /// <summary>
    /// Garante que exista um EventSystem na cena para processar cliques e eventos de UI.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(esObj);
            }
        }
    }

    /// <summary>
    /// Atualiza a visibilidade de todos os painéis com base no estado atual do jogo.
    /// - Playing: Exibe apenas a HUD de Combate/Habilidades.
    /// - Dialogue: Oculta a HUD de Combate e exibe a caixa de Diálogo.
    /// - Paused: Oculta a HUD de Combate e exibe o Menu de Pausa (ESC).
    /// - Menu: Oculta a HUD de Combate e Diálogo.
    /// </summary>
    public void UpdateUIVisibility(GameState state)
    {
        // 1. HUD de Combate / Poderes: visível APENAS durante o jogo normal (Playing)
        if (combatHUD != null)
        {
            combatHUD.SetActive(state == GameState.Playing);
        }

        // 2. Painel de Diálogo: visível em estado de Diálogo
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(state == GameState.Dialogue);
        }

        // 3. Painel de Pausa (ESC): visível em estado Pausado
        if (pausePanel != null)
        {
            pausePanel.SetActive(state == GameState.Paused);
        }

        // 4. Texto de Status
        if (statusText != null)
        {
            switch (state)
            {
                case GameState.Playing:
                    statusText.text = "<color=#00FF88>Status: JOGANDO</color>";
                    break;
                case GameState.Paused:
                    statusText.text = "<color=#FFCC00>Status: PAUSADO (ESC)</color>";
                    break;
                case GameState.Menu:
                    statusText.text = "<color=#00CCFF>Status: MENU</color>";
                    break;
                case GameState.Dialogue:
                    statusText.text = "<color=#FF6699>Status: EM DIÁLOGO</color>";
                    break;
            }
        }
    }

    #region Atribuição Dinâmica de Referências
    public void SetCombatHUD(GameObject panel) => combatHUD = panel;
    public void SetDialoguePanel(GameObject panel) => dialoguePanel = panel;
    public void SetPausePanel(GameObject panel) => pausePanel = panel;
    public void SetStatusText(TextMeshProUGUI text) => statusText = text;
    #endregion
}

