using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Exibe os Fragmentos de Estrela e Estrelas no HUD da Interface.
/// Atualiza dinamicamente ouvindo os eventos do PlayerCurrency.
/// Fica visível APENAS durante o estado de gameplay (GameState.Playing),
/// ocultando-se automaticamente em Diálogos, Menus, Pausa ou Morte.
/// </summary>
[RequireComponent(typeof(UIStateVisibility))]
public class CurrencyUIHUD : MonoBehaviour
{
    [Header("Referência da Carteira")]
    [SerializeField] private PlayerCurrency playerCurrency;

    [Header("Elementos de Texto")]
    [Tooltip("Texto para exibir os fragmentos atuais (ex: 7/10).")]
    [SerializeField] private TextMeshProUGUI fragmentsText;

    [Tooltip("Texto para exibir as estrelas totais (ex: 3).")]
    [SerializeField] private TextMeshProUGUI starsText;

    [Header("Formatação de Exibição")]
    [SerializeField] private string fragmentsFormat = "Frag: {0}/{1}";
    [SerializeField] private string starsFormat = "Estrelas: {0}";

    [Header("Controle de Visibilidade por Estado")]
    [Tooltip("Se verdadeiro, gerencia a ativação/desativação automática deste GameObject conforme o GameState.")]
    [SerializeField] private bool autoManageVisibility = true;
    [SerializeField] private List<GameState> visibleStates = new List<GameState> { GameState.Playing };

    private UIStateVisibility uiStateVisibility;

    private void Awake()
    {
        uiStateVisibility = GetComponent<UIStateVisibility>();
        if (uiStateVisibility != null)
        {
            uiStateVisibility.SetVisibleStates(GameState.Playing);
        }

        FindPlayerCurrency();
    }

    private void OnEnable()
    {
        FindPlayerCurrency();
        SubscribeEvents();
        UpdateAllDisplays();
        CheckStateVisibility();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Start()
    {
        FindPlayerCurrency();
        SubscribeEvents();
        UpdateAllDisplays();
        CheckStateVisibility();
    }

    private void Update()
    {
        if (playerCurrency == null)
        {
            FindPlayerCurrency();
            if (playerCurrency != null)
            {
                SubscribeEvents();
                UpdateAllDisplays();
            }
        }
    }

    private void FindPlayerCurrency()
    {
        if (playerCurrency == null)
        {
            playerCurrency = PlayerCurrency.Instance != null 
                ? PlayerCurrency.Instance 
                : Object.FindAnyObjectByType<PlayerCurrency>();
        }
    }

    private void SubscribeEvents()
    {
        UnsubscribeEvents();

        if (playerCurrency != null)
        {
            playerCurrency.OnStarFragmentsChanged += UpdateFragmentsDisplay;
            playerCurrency.OnStarsChanged += UpdateStarsDisplay;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (playerCurrency != null)
        {
            playerCurrency.OnStarFragmentsChanged -= UpdateFragmentsDisplay;
            playerCurrency.OnStarsChanged -= UpdateStarsDisplay;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState previousState, GameState newState)
    {
        if (!autoManageVisibility) return;

        bool shouldBeVisible = visibleStates.Contains(newState);
        if (gameObject.activeSelf != shouldBeVisible)
        {
            gameObject.SetActive(shouldBeVisible);
        }
    }

    private void CheckStateVisibility()
    {
        if (!autoManageVisibility || GameStateManager.Instance == null) return;

        bool shouldBeVisible = visibleStates.Contains(GameStateManager.Instance.CurrentState);
        if (gameObject.activeSelf != shouldBeVisible)
        {
            gameObject.SetActive(shouldBeVisible);
        }
    }

    public void UpdateAllDisplays()
    {
        if (playerCurrency != null)
        {
            UpdateFragmentsDisplay(playerCurrency.StarFragments);
            UpdateStarsDisplay(playerCurrency.Stars);
        }
    }

    private void UpdateFragmentsDisplay(int fragments)
    {
        if (fragmentsText != null)
        {
            int maxReq = playerCurrency != null ? playerCurrency.FragmentsPerStar : 10;
            fragmentsText.text = string.Format(fragmentsFormat, fragments, maxReq);
        }
    }

    private void UpdateStarsDisplay(int stars)
    {
        if (starsText != null)
        {
            starsText.text = string.Format(starsFormat, stars);
        }
    }
}
