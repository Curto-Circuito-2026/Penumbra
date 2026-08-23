using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Estrutura para vincular um painel de UI aos estados em que deve ficar visível.
/// </summary>
[Serializable]
public class UIPanelBinding
{
    public string panelName = "Novo Painel";
    public GameObject panel;
    public List<GameState> visibleInStates = new List<GameState> { GameState.Playing };
}

/// <summary>
/// Gerenciador Central de UI (UIManager).
/// Responsável por controlar a visibilidade de todos os painéis da interface de acordo com o estado do jogo (GameState).
/// Permite registro dinâmico de novos elementos via Inspector ou via código (RegisterPanel).
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

    [Header("Painéis Principais de Gameplay (Visíveis apenas em Playing)")]
    [Tooltip("Painel de Recursos do Jogador (Vida, Mana e Stamina no canto superior esquerdo).")]
    [SerializeField] private GameObject resourceHUD;

    [Tooltip("Painel da HUD de Combate/Habilidades (Canto inferior esquerdo e direito).")]
    [SerializeField] private GameObject combatHUD;

    [Header("Painéis Principais de Estado")]
    [Tooltip("Painel da Caixa de Diálogo (Visível em Dialogue).")]
    [SerializeField] private GameObject dialoguePanel;

    [Tooltip("Painel do Menu de Pausa (ESC) (Visível em Paused).")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("Painel de Morte (Tela de Morte) (Visível em Dead).")]
    [SerializeField] private GameObject deathPanel;

    [Header("Lista de Painéis Customizados (Configurável via Inspector)")]
    [Tooltip("Adicione novos painéis e defina em quais estados eles devem ficar visíveis.")]
    [SerializeField] private List<UIPanelBinding> customPanels = new List<UIPanelBinding>();

    [Header("Indicador de Status (Opcional)")]
    [Tooltip("Texto com o estado atual do jogo.")]
    [SerializeField] private TextMeshProUGUI statusText;

    // Registro dinâmico de painéis adicionados em tempo de execução
    private readonly Dictionary<GameObject, List<GameState>> dynamicPanels = new Dictionary<GameObject, List<GameState>>();
    private bool isSubscribed = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (dontDestroyOnLoad && Application.isPlaying)
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
            if (dontDestroyOnLoad && Application.isPlaying)
            {
                DontDestroyOnLoad(esObj);
            }
        }
    }

    /// <summary>
    /// Registra dinamicamente um painel e seus estados de visibilidade.
    /// Útil para novos sistemas, inventários, lojas ou componentes UIStateVisibility.
    /// </summary>
    public void RegisterPanel(GameObject panel, List<GameState> visibleInStates)
    {
        if (panel == null) return;
        if (panel == deathPanel || panel == pausePanel || panel == dialoguePanel) return;

        if (dynamicPanels.ContainsKey(panel))
        {
            dynamicPanels[panel] = new List<GameState>(visibleInStates);
        }
        else
        {
            dynamicPanels.Add(panel, new List<GameState>(visibleInStates));
        }

        // Aplica o estado atual imediatamente
        if (GameStateManager.Instance != null)
        {
            bool shouldBeVisible = visibleInStates.Contains(GameStateManager.Instance.CurrentState);
            panel.SetActive(shouldBeVisible);
        }
    }

    /// <summary>
    /// Remove um painel do registro dinâmico de visibilidade.
    /// </summary>
    public void UnregisterPanel(GameObject panel)
    {
        if (panel != null && dynamicPanels.ContainsKey(panel))
        {
            dynamicPanels.Remove(panel);
        }
    }

    /// <summary>
    /// Atualiza a visibilidade de todos os painéis padrão, configurados e dinâmicos com base no GameState atual.
    /// </summary>
    public void UpdateUIVisibility(GameState state)
    {
        // 1. HUD de Recursos (Vida, Mana, Stamina): Visível APENAS em Playing
        if (resourceHUD != null && resourceHUD.activeSelf != (state == GameState.Playing))
        {
            resourceHUD.SetActive(state == GameState.Playing);
        }

        // 2. HUD de Combate / Habilidades: Visível APENAS em Playing
        if (combatHUD != null && combatHUD.activeSelf != (state == GameState.Playing))
        {
            combatHUD.SetActive(state == GameState.Playing);
        }

        // 3. Painel de Diálogo: Visível em Dialogue
        if (dialoguePanel != null && dialoguePanel.activeSelf != (state == GameState.Dialogue))
        {
            dialoguePanel.SetActive(state == GameState.Dialogue);
        }

        // 4. Painel de Pausa (ESC): Visível em Paused
        if (pausePanel != null && pausePanel.activeSelf != (state == GameState.Paused))
        {
            pausePanel.SetActive(state == GameState.Paused);
        }

        // 5. Painel de Morte: Visível em Dead
        if (deathPanel != null && deathPanel.activeSelf != (state == GameState.Dead))
        {
            deathPanel.SetActive(state == GameState.Dead);
        }

        // 6. Painéis Customizados da Lista no Inspector
        foreach (var binding in customPanels)
        {
            if (binding != null && binding.panel != null)
            {
                bool isVisible = binding.visibleInStates != null && binding.visibleInStates.Contains(state);
                if (binding.panel.activeSelf != isVisible)
                {
                    binding.panel.SetActive(isVisible);
                }
            }
        }

        // 7. Painéis Registrados Dinamicamente (cria uma cópia para evitar InvalidOperationException se SetActive disparar OnEnable/RegisterPanel)
        var dynamicPanelsSnapshot = new List<KeyValuePair<GameObject, List<GameState>>>(dynamicPanels);
        foreach (var kvp in dynamicPanelsSnapshot)
        {
            if (kvp.Key != null && kvp.Key != deathPanel && kvp.Key != pausePanel && kvp.Key != dialoguePanel)
            {
                bool isVisible = kvp.Value != null && kvp.Value.Contains(state);
                if (kvp.Key.activeSelf != isVisible)
                {
                    kvp.Key.SetActive(isVisible);
                }
            }
        }

        // 8. Varre e atualiza todos os componentes UIStateVisibility na cena (garantia total de padronização)
        UIStateVisibility[] allVisibilities = UnityEngine.Object.FindObjectsByType<UIStateVisibility>(FindObjectsInactive.Include);
        foreach (var vis in allVisibilities)
        {
            if (vis != null && vis.gameObject != null && vis.gameObject != deathPanel && vis.gameObject != pausePanel && vis.gameObject != dialoguePanel)
            {
                bool isVisible = vis.VisibleInStates != null && vis.VisibleInStates.Contains(state);
                if (vis.gameObject.activeSelf != isVisible)
                {
                    vis.gameObject.SetActive(isVisible);
                }
            }
        }

        // 8. Oculta barra do Boss se o player morreu ou está no menu principal
        if (state == GameState.Dead || state == GameState.Menu)
        {
            if (BossHealthBarUI.Instance != null)
            {
                BossHealthBarUI.Instance.HideImmediate();
            }
        }

        // 9. Texto de Status
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
                case GameState.Cutscene:
                    statusText.text = "<color=#CC88FF>Status: CUTSCENE</color>";
                    break;
                case GameState.Dead:
                    statusText.text = "<color=#FF0000>Status: MORTO</color>";
                    break;
            }
        }
    }

    #region Atribuição Dinâmica de Referências
    public void SetResourceHUD(GameObject panel) => resourceHUD = panel;
    public void SetCombatHUD(GameObject panel) => combatHUD = panel;
    public void SetDialoguePanel(GameObject panel) => dialoguePanel = panel;
    public void SetPausePanel(GameObject panel) => pausePanel = panel;
    public void SetDeathPanel(GameObject panel) => deathPanel = panel;
    public void SetStatusText(TextMeshProUGUI text) => statusText = text;
    #endregion
}


