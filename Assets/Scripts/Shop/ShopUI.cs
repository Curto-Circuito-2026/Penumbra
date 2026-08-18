using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Gerenciador Central da Interface da Loja.
/// Abre a janela da loja com catálogo dinâmico de qualquer NPC, processa compras,
/// controla limites de compra por item (itens únicos/limitados) e pausa/restaura o estado do jogo.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    [Header("Estrutura da Loja")]
    [Tooltip("Painel principal da interface da loja (janela/modal).")]
    [SerializeField] private GameObject shopPanel;

    [Tooltip("Transform do container com layout de grade onde os slots serão instanciados.")]
    [SerializeField] private Transform itemsContainer;

    [Tooltip("Prefab do card de item da loja (ShopItemSlotUI).")]
    [SerializeField] private GameObject slotPrefab;

    [Header("Ilustração de Fundo do Comerciante (Backdrop)")]
    [Tooltip("Imagem da ilustração/arte do NPC exibida no fundo da tela atrás da janela da loja.")]
    [SerializeField] private Image shopkeeperBackdropImage;

    [Tooltip("Sprite padrão da ilustração de fundo caso o comerciante não defina um sprite exclusivo.")]
    [SerializeField] private Sprite defaultBackdropSprite;

    [Header("Textos de Cabeçalho da Loja")]
    [SerializeField] private TextMeshProUGUI shopTitleText;
    [SerializeField] private TextMeshProUGUI shopSubtitleText;

    [Header("Catálogo Padrão (Fallback)")]
    [SerializeField] private List<ShopItemSO> defaultCatalog = new List<ShopItemSO>();

    [Header("Exibição de Saldo do Jogador")]
    [SerializeField] private TextMeshProUGUI fragmentsBalanceText;
    [SerializeField] private TextMeshProUGUI starsBalanceText;

    [Header("Botões e Controles")]
    [SerializeField] private Button closeButton;

    [Header("Eventos e Gatilhos")]
    [Tooltip("GameEvent que dispara a abertura da loja automaticamente (ex: onEnd do diálogo).")]
    [SerializeField] private GameEvent openShopEvent;

    private readonly List<ShopItemSlotUI> activeSlots = new List<ShopItemSlotUI>();
    private bool isOpen = false;

    // Histórico de compras nesta sessão/run para itens com limite de compras (maxPurchases)
    private static readonly Dictionary<string, int> sessionPurchases = new Dictionary<string, int>();

    public bool IsOpen => isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetSessionPurchasesOnNewRun()
    {
        sessionPurchases.Clear();
    }

    private void OnEnable()
    {
        if (openShopEvent != null)
        {
            openShopEvent.OnEventRaised += OpenShop;
        }
    }

    private void OnDisable()
    {
        if (openShopEvent != null)
        {
            openShopEvent.OnEventRaised -= OpenShop;
        }
    }

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

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseShop);
        }

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    private void Start()
    {
        if (shopPanel != null && isOpen)
        {
            RefreshBalances();
        }
    }

    private int openFrame = 0;

    private void Update()
    {
        if (!isOpen) return;

        // Evita fechar no mesmo frame em que a loja foi aberta
        if (Time.frameCount == openFrame) return;

        // Fecha a loja ao pressionar a tecla ESC ou botão Cancel do controle
        bool closePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (closePressed)
        {
            CloseShop();
        }
    }

    /// <summary>
    /// Abre a loja usando o catálogo padrão.
    /// </summary>
    public void OpenShop()
    {
        OpenShop("✦ LOJA DAS ESTRELAS ✦", "Troque suas estrelas e fragmentos por bênçãos sagradas", defaultCatalog, null);
    }

    /// <summary>
    /// Abre a loja com um catálogo customizado de itens.
    /// </summary>
    public void OpenShop(List<ShopItemSO> catalog)
    {
        OpenShop("✦ LOJA DAS ESTRELAS ✦", "Troque suas estrelas e fragmentos por bênçãos sagradas", catalog, null);
    }

    /// <summary>
    /// Abre a loja com título, subtítulo e catálogo customizados para qualquer NPC comerciante.
    /// </summary>
    public void OpenShop(string title, string subtitle, List<ShopItemSO> catalog)
    {
        OpenShop(title, subtitle, catalog, null);
    }

    /// <summary>
    /// Abre a loja com título, subtítulo, catálogo e ilustração de fundo customizados.
    /// </summary>
    public void OpenShop(string title, string subtitle, List<ShopItemSO> catalog, Sprite illustration)
    {
        isOpen = true;
        openFrame = Time.frameCount;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseShop);
        }

        if (shopTitleText != null && !string.IsNullOrEmpty(title))
        {
            shopTitleText.text = title;
        }

        if (shopSubtitleText != null && !string.IsNullOrEmpty(subtitle))
        {
            shopSubtitleText.text = subtitle;
        }

        // Atualiza a ilustração de fundo do personagem (backdrop)
        if (shopkeeperBackdropImage != null)
        {
            Sprite targetSprite = illustration != null ? illustration : (shopkeeperBackdropImage.sprite != null ? shopkeeperBackdropImage.sprite : defaultBackdropSprite);
            if (targetSprite != null)
            {
                shopkeeperBackdropImage.sprite = targetSprite;
                shopkeeperBackdropImage.preserveAspect = true;
                shopkeeperBackdropImage.gameObject.SetActive(true);
            }
            else
            {
                shopkeeperBackdropImage.gameObject.SetActive(false);
            }
        }

        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }

        // Altera o estado do jogo para Menu (impede movimentação do jogador durante as compras)
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetMenu();
        }

        PopulateCatalog(catalog != null && catalog.Count > 0 ? catalog : defaultCatalog);
        RefreshBalances();
        SubscribeCurrencyEvents();

        Debug.Log($"[ShopUI] Loja '{title}' aberta com sucesso!");
    }

    /// <summary>
    /// Fecha a interface da loja e restaura a movimentação do jogador.
    /// </summary>
    public void CloseShop()
    {
        if (!isOpen) return;
        isOpen = false;

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        UnsubscribeCurrencyEvents();

        // Notifica o DialogueTrigger para evitar reabrir diálogo acidentalmente
        DialogueTrigger.NotifyDialogueOrShopClosed();

        // Restaura o estado do jogo para Playing
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Menu)
        {
            GameStateManager.Instance.SetPlaying();
        }

        Debug.Log("[ShopUI] Loja fechada!");
    }

    /// <summary>
    /// Popula a grade de itens com base na lista fornecida.
    /// </summary>
    private void PopulateCatalog(List<ShopItemSO> items)
    {
        activeSlots.Clear();

        if (itemsContainer == null || slotPrefab == null) return;

        // Limpa slots antigos
        foreach (Transform child in itemsContainer)
        {
            Destroy(child.gameObject);
        }

        if (items == null) return;

        foreach (var item in items)
        {
            if (item == null) continue;

            GameObject slotObj = Instantiate(slotPrefab, itemsContainer);
            ShopItemSlotUI slot = slotObj.GetComponent<ShopItemSlotUI>();
            if (slot != null)
            {
                int currentBought = GetPurchaseCount(item);
                slot.Setup(item, this, currentBought);
                activeSlots.Add(slot);
            }
        }

        RefreshAllSlots();
    }

    /// <summary>
    /// Tenta realizar a compra de um item.
    /// </summary>
    public bool TryBuyItem(ShopItemSO item)
    {
        if (item == null) return false;

        // Verifica limite de compras (maxPurchases)
        int currentBought = GetPurchaseCount(item);
        if (item.MaxPurchases > 0 && currentBought >= item.MaxPurchases)
        {
            Debug.Log($"[ShopUI] O item '{item.ItemName}' já atingiu o limite de compras!");
            return false;
        }

        PlayerCurrency currency = PlayerCurrency.Instance;
        if (currency == null)
        {
            currency = Object.FindAnyObjectByType<PlayerCurrency>();
        }

        if (currency == null)
        {
            Debug.LogWarning("[ShopUI] Componente PlayerCurrency não encontrado!");
            return false;
        }

        // Verifica e debita as moedas
        bool spentSuccess = false;
        if (item.Currency == CurrencyType.Stars)
        {
            spentSuccess = currency.SpendStars(item.Price);
        }
        else
        {
            spentSuccess = currency.SpendStarFragments(item.Price);
        }

        if (!spentSuccess)
        {
            Debug.Log($"[ShopUI] Saldo insuficiente para comprar '{item.ItemName}'!");
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.SpawnFloatingText(
                    Vector3.zero, 
                    "Saldo Insuficiente!", 
                    new Color(1f, 0.3f, 0.3f), 
                    3.5f
                );
            }
            return false;
        }

        // Registra compra no histórico
        RecordPurchase(item);

        // Aplica o efeito no Jogador
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            item.ApplyEffect(playerObj);
        }

        RefreshBalances();
        RefreshAllSlots();
        return true;
    }

    public static int GetPurchaseCount(ShopItemSO item)
    {
        if (item == null) return 0;
        string key = item.name;
        return sessionPurchases.TryGetValue(key, out int count) ? count : 0;
    }

    public static void RecordPurchase(ShopItemSO item)
    {
        if (item == null) return;
        string key = item.name;
        if (sessionPurchases.ContainsKey(key))
        {
            sessionPurchases[key]++;
        }
        else
        {
            sessionPurchases[key] = 1;
        }
    }

    public void RefreshAllSlots()
    {
        int frags = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.StarFragments : 0;
        int stars = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Stars : 0;

        foreach (var slot in activeSlots)
        {
            if (slot != null)
            {
                int bought = GetPurchaseCount(slot.CurrentItem);
                slot.UpdateAffordability(frags, stars, bought);
            }
        }
    }

    private void RefreshBalances()
    {
        int frags = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.StarFragments : 0;
        int stars = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Stars : 0;

        if (fragmentsBalanceText != null)
        {
            fragmentsBalanceText.text = $"★ {frags}";
        }

        if (starsBalanceText != null)
        {
            starsBalanceText.text = $"⭐ {stars}";
        }
    }

    private void SubscribeCurrencyEvents()
    {
        if (PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.OnStarFragmentsChanged += HandleCurrencyChanged;
            PlayerCurrency.Instance.OnStarsChanged += HandleCurrencyChanged;
        }
    }

    private void UnsubscribeCurrencyEvents()
    {
        if (PlayerCurrency.Instance != null)
        {
            PlayerCurrency.Instance.OnStarFragmentsChanged -= HandleCurrencyChanged;
            PlayerCurrency.Instance.OnStarsChanged -= HandleCurrencyChanged;
        }
    }

    private void HandleCurrencyChanged(int _)
    {
        RefreshBalances();
        RefreshAllSlots();
    }
}
