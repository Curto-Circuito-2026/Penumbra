using UnityEngine;
using TMPro;

public class GameStateUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject pausePanel;

    private bool isSubscribed = false;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        if (GameStateManager.Instance != null)
        {
            UpdateUI(GameStateManager.Instance.CurrentState);
        }
    }

    private void Update()
    {
        // Se ainda não se inscreveu (caso o GameStateManager tenha inicializado depois)
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
            UpdateUI(GameStateManager.Instance.CurrentState);
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
        UpdateUI(newState);
    }

    private void UpdateUI(GameState state)
    {
        if (statusText != null)
        {
            switch (state)
            {
                case GameState.Playing:
                    statusText.text = "<color=#00FF88>Status: JOGANDO</color>";
                    break;
                case GameState.Paused:
                    statusText.text = "<color=#FFCC00>Status: PAUSADO</color>";
                    break;
                case GameState.Menu:
                    statusText.text = "<color=#00CCFF>Status: MENU</color>";
                    break;
                case GameState.Dialogue:
                    statusText.text = "<color=#FF6699>Status: EM DIÁLOGO</color>";
                    break;
            }
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(state == GameState.Paused);
        }
    }
}

