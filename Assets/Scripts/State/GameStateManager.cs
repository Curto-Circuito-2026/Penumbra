using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Enumeração dos estados possíveis do jogo.
/// </summary>
public enum GameState
{
    Playing,    // Jogo rodando (Gameplay) - O jogador PODE se movimentar
    Paused,     // Jogo pausado
    Menu,       // Em algum menu de interface
    Dialogue,   // Em diálogo com NPC/evento
    Cutscene,   // Em cena de corte / animação cinemática
    Dead        // Personagem morto - Exibe tela de morte
}

public class GameStateManager : MonoBehaviour
{
    private static GameStateManager instance;

    public static GameStateManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<GameStateManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("GameStateManager");
                    instance = obj.AddComponent<GameStateManager>();
                }
            }
            return instance;
        }
    }

    [Header("State Configuration")]
    [Tooltip("Estado inicial do jogo.")]
    [SerializeField] private GameState currentState = GameState.Playing;

    [Tooltip("Se verdadeiro, congela o tempo (Time.timeScale = 0) quando Pausado ou em Menu.")]
    [SerializeField] private bool pauseTimeScaleOnPause = true;

    // Evento disparado sempre que o estado do jogo muda: Action<EstadoAnterior, EstadoNovo>
    public event Action<GameState, GameState> OnStateChanged;

    private GameState previousState = GameState.Playing;

    // Propriedades de acesso público
    public GameState CurrentState => currentState;
    public GameState PreviousState => previousState;

    /// <summary>
    /// Retorna verdadeiro APENAS se o jogo estiver no estado Playing (Jogando).
    /// Usado para autorizar ou bloquear a movimentação e ações do jogador.
    /// </summary>
    public bool CanPlayerMove => currentState == GameState.Playing;

    private void Awake()
    {
        // Padrão Singleton seguro
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

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene.Equals("Menu", System.StringComparison.OrdinalIgnoreCase) ||
            activeScene.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
        {
            currentState = GameState.Menu;
            previousState = GameState.Menu;
        }
        else
        {
            previousState = currentState;
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name.Equals("Menu", System.StringComparison.OrdinalIgnoreCase) ||
            scene.name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
        {
            SetState(GameState.Menu);
        }
        else
        {
            if (currentState == GameState.Menu)
            {
                SetState(GameState.Playing);
            }
        }
    }

    /// <summary>
    /// Frame da última alteração de estado (usado para evitar inputs simultâneos no mesmo frame de troca).
    /// </summary>
    public int StateChangeFrame { get; private set; } = -1;

    private void Start()
    {
        ApplyStateEffects(currentState);
    }

    private void Update()
    {
        // Ignora input de pausa no mesmo frame em que o estado mudou (ex: fechar janela/loja com ESC)
        if (Time.frameCount == StateChangeFrame) return;

        // Permite alternar Pausa com a tecla ESC, P ou botão Start do Gamepad
        bool pausePressed = (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)) ||
                           (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        if (pausePressed)
        {
            TogglePause();
        }
    }

    /// <summary>
    /// Altera o estado atual do jogo e dispara os eventos correspondentes.
    /// </summary>
    /// <param name="newState">Novo estado a ser aplicado.</param>
    public void SetState(GameState newState)
    {
        bool stateChanged = currentState != newState;

        previousState = currentState;
        currentState = newState;

        if (stateChanged)
        {
            StateChangeFrame = Time.frameCount;
            Debug.Log($"[GameStateManager] Estado alterado de {previousState} para {currentState}");
            OnStateChanged?.Invoke(previousState, currentState);
        }

        ApplyStateEffects(newState);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateUIVisibility(newState);
        }
    }

    /// <summary>
    /// Alterna entre Pausado e o estado anterior (ou Playing).
    /// Não permite pausar enquanto em Diálogo, Morto ou em Menus abertos (como Loja/Inventário).
    /// </summary>
    public void TogglePause()
    {
        if (currentState == GameState.Dialogue || currentState == GameState.Dead || currentState == GameState.Menu || currentState == GameState.Cutscene) return;

        if (currentState == GameState.Paused)
        {
            SetState(previousState == GameState.Paused ? GameState.Playing : previousState);
        }
        else
        {
            SetState(GameState.Paused);
        }
    }

    // Métodos auxiliares públicos para atalhos de alteração de estado
    public void SetPlaying() => SetState(GameState.Playing);
    public void SetPaused() => SetState(GameState.Paused);
    public void SetMenu() => SetState(GameState.Menu);
    public void SetDialogue() => SetState(GameState.Dialogue);
    public void SetCutscene() => SetState(GameState.Cutscene);
    public void SetDead() => SetState(GameState.Dead);

    /// <summary>
    /// Efeitos colaterais por estado (ex: congelar/descongelar o tempo).
    /// </summary>
    private void ApplyStateEffects(GameState state)
    {
        if (pauseTimeScaleOnPause)
        {
            if (state == GameState.Paused || state == GameState.Menu)
            {
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = 1f;
            }
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}

