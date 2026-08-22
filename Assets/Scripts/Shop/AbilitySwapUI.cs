using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Gerenciador de Interface da Troca de Habilidades, Bênçãos e Acordos Folclóricos.
/// Apresenta 3 opções sorteadas verticalmente à direita, com arte do mestre à esquerda e sistema de Re-roll com Estrelas.
/// </summary>
public class AbilitySwapUI : MonoBehaviour
{
    public static AbilitySwapUI Instance { get; private set; }

    [Header("Painéis e Estrutura")]
    [SerializeField] private GameObject swapPanel;
    [SerializeField] private Image characterBackdropImage;
    [SerializeField] private Sprite defaultBackdropSprite;
    [SerializeField] private RectTransform cardsContainer;
    [SerializeField] private GameObject cardPrefab;

    [Header("Cabeçalho")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Rodapé e Controles")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TextMeshProUGUI rerollButtonText;
    [SerializeField] private TextMeshProUGUI starsBalanceText;
    [SerializeField] private Button closeButton;

    [Header("Configuração de Re-roll e Pool Padrão")]
    [Tooltip("Custo em Estrelas para cada Re-roll de habilidades.")]
    [SerializeField] private int rerollCostStars = 1;

    [Tooltip("Pool padrão de bênçãos disponíveis caso o NPC não passe uma lista personalizada.")]
    [SerializeField] private List<AbilityBoonSO> defaultBoonPool = new List<AbilityBoonSO>();

    private readonly List<AbilityBoonCardUI> activeCards = new List<AbilityBoonCardUI>();
    private List<AbilityBoonSO> currentPool = new List<AbilityBoonSO>();
    private bool isOpen = false;
    private int openFrame = 0;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureReferences();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseSwap);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(RerollOptions);
        }

        if (swapPanel != null)
        {
            swapPanel.SetActive(false);
        }
    }

    private void EnsureReferences()
    {
        if (swapPanel == null)
        {
            Transform p = transform.Find("AbilitySwap_Modal_Panel");
            if (p != null) swapPanel = p.gameObject;
        }

        if (cardsContainer == null)
        {
            Transform found = transform.Find("AbilitySwap_Modal_Panel/Swap_Window/Cards_Container");
            if (found == null) found = transform.Find("Swap_Window/Cards_Container");
            if (found != null) cardsContainer = found.GetComponent<RectTransform>();
        }

        if (cardPrefab == null)
        {
            cardPrefab = Resources.Load<GameObject>("AbilityBoonCard") ?? Resources.Load<GameObject>("Prefabs/UI/AbilityBoonCard");
        }

        if (closeButton == null)
        {
            Transform cb = transform.Find("AbilitySwap_Modal_Panel/Swap_Window/Bottom_Bar/Close_Button");
            if (cb != null) closeButton = cb.GetComponent<Button>();
        }

        if (rerollButton == null)
        {
            Transform rb = transform.Find("AbilitySwap_Modal_Panel/Swap_Window/Bottom_Bar/Reroll_Button");
            if (rb != null) rerollButton = rb.GetComponent<Button>();
        }

        if (characterBackdropImage == null)
        {
            Transform bi = transform.Find("AbilitySwap_Modal_Panel/Character_Illustration");
            if (bi != null) characterBackdropImage = bi.GetComponent<Image>();
        }
    }

    private void Update()
    {
        if (!isOpen) return;

        // Evita fechar no mesmo frame de abertura
        if (Time.frameCount == openFrame) return;

        // Tecla ESC para fechar
        bool closePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (closePressed)
        {
            // Se o modal de seleção de slot estiver aberto, fecha apenas o modal primeiro!
            if (SkillEquipModalUI.Instance != null && SkillEquipModalUI.Instance.IsOpen)
            {
                SkillEquipModalUI.Instance.CloseModal();
                return;
            }

            CloseSwap();
        }
    }

    /// <summary>
    /// Abre a tela de troca de habilidades com título, subtítulo, pool de bênçãos e arte do mestre.
    /// </summary>
    public void OpenSwap(string title, string subtitle, List<AbilityBoonSO> customPool, Sprite characterArt)
    {
        EnsureReferences();
        isOpen = true;
        openFrame = Time.frameCount;

        if (titleText != null && !string.IsNullOrEmpty(title))
        {
            titleText.text = title;
        }

        if (subtitleText != null && !string.IsNullOrEmpty(subtitle))
        {
            subtitleText.text = subtitle;
        }

        // Arte do Personagem em Tela Inteira 1920x1080
        if (characterBackdropImage != null)
        {
            Sprite targetSprite = characterArt != null ? characterArt : (characterBackdropImage.sprite != null ? characterBackdropImage.sprite : defaultBackdropSprite);
            if (targetSprite != null)
            {
                characterBackdropImage.sprite = targetSprite;
                characterBackdropImage.preserveAspect = false;
                characterBackdropImage.gameObject.SetActive(true);
            }
            else
            {
                characterBackdropImage.gameObject.SetActive(false);
            }
        }

        if (swapPanel != null)
        {
            swapPanel.SetActive(true);
        }

        // Bloqueia movimentação do jogador (Estado Menu)
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetMenu();
        }

        // Define o pool de bênçãos atual
        currentPool = (customPool != null && customPool.Count > 0) ? new List<AbilityBoonSO>(customPool) : new List<AbilityBoonSO>(defaultBoonPool);

        DrawAndDisplayBoons(3);
        RefreshBalances();
        SubscribeCurrencyEvents();

        Debug.Log($"[AbilitySwapUI] Painel de Bênçãos '{title}' aberto com sucesso!");
    }

    /// <summary>
    /// Sorteia 'count' bênçãos distintas do pool e instancia os cards na tela.
    /// </summary>
    private void DrawAndDisplayBoons(int count = 3)
    {
        EnsureReferences();
        activeCards.Clear();

        if (cardsContainer == null || cardPrefab == null)
        {
            Debug.LogError("[AbilitySwapUI] cardsContainer ou cardPrefab não configurados!");
            return;
        }

        // Limpa cards antigos
        foreach (Transform child in cardsContainer)
        {
            Destroy(child.gameObject);
        }

        if (currentPool == null || currentPool.Count == 0)
        {
            Debug.LogWarning("[AbilitySwapUI] O pool de bênçãos está vazio!");
            return;
        }

        // Sorteia até 3 bênçãos sem repetição
        List<AbilityBoonSO> available = new List<AbilityBoonSO>(currentPool);
        List<AbilityBoonSO> drawn = new List<AbilityBoonSO>();

        int drawAmount = Mathf.Min(count, available.Count);
        for (int i = 0; i < drawAmount; i++)
        {
            int randomIndex = Random.Range(0, available.Count);
            drawn.Add(available[randomIndex]);
            available.RemoveAt(randomIndex);
        }

        // Instancia os cards
        foreach (var boon in drawn)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
            AbilityBoonCardUI cardUI = cardObj.GetComponent<AbilityBoonCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(boon, this);
                activeCards.Add(cardUI);
            }
        }

        Debug.Log($"[AbilitySwapUI] {activeCards.Count} bênçãos sorteadas e exibidas nos cards.");
    }

    /// <summary>
    /// Executa o Re-roll gastando estrelas do jogador e sorteando novas opções.
    /// </summary>
    public void RerollOptions()
    {
        PlayerCurrency currency = PlayerCurrency.Instance ?? Object.FindAnyObjectByType<PlayerCurrency>();
        if (currency == null)
        {
            Debug.LogWarning("[AbilitySwapUI] PlayerCurrency não encontrado!");
            return;
        }

        if (currency.Stars < rerollCostStars)
        {
            Debug.Log($"[AbilitySwapUI] Estrelas insuficientes para Re-roll! Custo: {rerollCostStars}, Atual: {currency.Stars}");
            return;
        }

        // Gasta as estrelas do Re-roll
        if (currency.SpendStars(rerollCostStars))
        {
            Debug.Log($"[AbilitySwapUI] Re-roll realizado com sucesso! (-{rerollCostStars} Estrelas)");
            DrawAndDisplayBoons(3);
            RefreshBalances();
        }
    }

    /// <summary>
    /// Chamado quando o jogador clica para escolher uma bênção ou habilidade.
    /// Abre o modal de escolha de slot (Q, E, R) onde a compra e equipamento são confirmados.
    /// </summary>
    public void SelectBoon(AbilityBoonSO chosenBoon)
    {
        if (chosenBoon == null) return;

        SkillEquipModalUI equipModal = SkillEquipModalUI.Instance ?? Object.FindAnyObjectByType<SkillEquipModalUI>(FindObjectsInactive.Include);
        if (equipModal != null)
        {
            equipModal.OpenModal(chosenBoon, this);
            return;
        }

        // Fallback caso o modal não esteja na cena
        PlayerCurrency currency = PlayerCurrency.Instance ?? Object.FindAnyObjectByType<PlayerCurrency>();
        if (currency == null || currency.Stars < chosenBoon.StarCost) return;

        if (currency.SpendStars(chosenBoon.StarCost))
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (chosenBoon.GrantedAbility != null)
            {
                PlayerCombatController combat = Object.FindAnyObjectByType<PlayerCombatController>();
                if (combat != null) combat.EquipAbility(0, chosenBoon.GrantedAbility);
            }
            else
            {
                chosenBoon.ApplyBoon(player);
            }
            CloseSwap();
        }
    }

    /// <summary>
    /// Fecha a tela de troca de habilidades e restaura o jogo para Playing.
    /// </summary>
    public void CloseSwap()
    {
        if (!isOpen) return;
        isOpen = false;

        if (swapPanel != null)
        {
            swapPanel.SetActive(false);
        }

        UnsubscribeCurrencyEvents();
        DialogueTrigger.NotifyDialogueOrShopClosed();

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Menu)
        {
            GameStateManager.Instance.SetPlaying();
        }

        Debug.Log("[AbilitySwapUI] Painel de Troca de Habilidades fechado!");
    }

    /// <summary>
    /// Atualiza os textos de saldo de estrelas e o botão de Re-roll.
    /// </summary>
    public void RefreshBalances()
    {
        PlayerCurrency currency = PlayerCurrency.Instance ?? Object.FindAnyObjectByType<PlayerCurrency>();
        int currentStars = currency != null ? currency.Stars : 0;

        if (starsBalanceText != null)
        {
            starsBalanceText.text = $"Estrelas: {currentStars}";
        }

        if (rerollButton != null)
        {
            rerollButton.interactable = currentStars >= rerollCostStars;
        }

        if (rerollButtonText != null)
        {
            bool canAfford = currentStars >= rerollCostStars;
            string costColor = canAfford ? "#FFD700" : "#FF6666";
            string starUnit = rerollCostStars == 1 ? "Estrela" : "Estrelas";
            rerollButtonText.text = $"<b>Re-rollar</b> (<color={costColor}>{rerollCostStars} {starUnit}</color>)";
        }

        if (rerollButton != null)
        {
            rerollButton.interactable = currentStars >= rerollCostStars;
        }

        // Atualiza a disponibilidade de todos os cards exibidos na tela
        foreach (var card in activeCards)
        {
            if (card != null)
            {
                card.UpdateAffordability();
            }
        }
    }

    private void SubscribeCurrencyEvents()
    {
        PlayerCurrency currency = PlayerCurrency.Instance ?? Object.FindAnyObjectByType<PlayerCurrency>();
        if (currency != null)
        {
            currency.OnStarsChanged += OnCurrencyChanged;
        }
    }

    private void UnsubscribeCurrencyEvents()
    {
        PlayerCurrency currency = PlayerCurrency.Instance ?? Object.FindAnyObjectByType<PlayerCurrency>();
        if (currency != null)
        {
            currency.OnStarsChanged -= OnCurrencyChanged;
        }
    }

    private void OnCurrencyChanged(int newAmount)
    {
        RefreshBalances();
    }
}
