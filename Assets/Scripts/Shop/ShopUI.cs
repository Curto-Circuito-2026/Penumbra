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

    private void EnsureReferences()
    {
        if (shopPanel == null)
        {
            Transform p = transform.Find("Shop_Modal_Panel");
            if (p != null) shopPanel = p.gameObject;
        }

        if (itemsContainer == null)
        {
            Transform found = transform.Find("Shop_Modal_Panel/Shop_Window/Items_ScrollView/Viewport/Content");
            if (found == null) found = transform.Find("Shop_Window/Items_ScrollView/Viewport/Content");
            if (found != null) itemsContainer = found.GetComponent<RectTransform>();
        }

        if (slotPrefab == null)
        {
            slotPrefab = Resources.Load<GameObject>("ShopItemSlot") ?? Resources.Load<GameObject>("Prefabs/UI/ShopItemSlot");
        }

        if (closeButton == null)
        {
            closeButton = GetComponentInChildren<Button>(true);
        }

        if (fragmentsBalanceText == null)
        {
            Transform f = transform.Find("Shop_Modal_Panel/Shop_Window/BottomRight_Footer/Balances_Display/Fragments_Balance");
            if (f != null) fragmentsBalanceText = f.GetComponent<TextMeshProUGUI>();
        }

        if (starsBalanceText == null)
        {
            Transform s = transform.Find("Shop_Modal_Panel/Shop_Window/BottomRight_Footer/Balances_Display/Stars_Balance");
            if (s != null) starsBalanceText = s.GetComponent<TextMeshProUGUI>();
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

        EnsureReferences();

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
        EnsureReferences();
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
        OpenShop("LOJA DAS ESTRELAS", "Troque suas estrelas e fragmentos por bênçãos sagradas", defaultCatalog, null);
    }

    /// <summary>
    /// Abre a loja com um catálogo customizado de itens.
    /// </summary>
    public void OpenShop(List<ShopItemSO> catalog)
    {
        OpenShop("LOJA DAS ESTRELAS", "Troque suas estrelas e fragmentos por bênçãos sagradas", catalog, null);
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
        EnsureReferences();
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

        // Atualiza a ilustração de fundo do personagem (backdrop 1920x1080)
        if (shopkeeperBackdropImage != null)
        {
            Sprite targetSprite = illustration != null ? illustration : (shopkeeperBackdropImage.sprite != null ? shopkeeperBackdropImage.sprite : defaultBackdropSprite);
            if (targetSprite != null)
            {
                shopkeeperBackdropImage.sprite = targetSprite;
                shopkeeperBackdropImage.preserveAspect = false;
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

        List<ShopItemSO> finalCatalog = (catalog != null && catalog.Count > 0) ? catalog : defaultCatalog;
        PopulateCatalog(finalCatalog);
        RefreshBalances();
        SubscribeCurrencyEvents();

        Debug.Log($"[ShopUI] Loja '{title}' aberta com {finalCatalog?.Count ?? 0} itens!");
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
        EnsureReferences();
        activeSlots.Clear();

        if (itemsContainer == null)
        {
            Debug.LogError("[ShopUI] itemsContainer (Content) é NULO no ShopUI!");
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("[ShopUI] slotPrefab é NULO no ShopUI! Não foi possível instanciar os cards de itens.");
            return;
        }

        // Limpa slots antigos
        foreach (Transform child in itemsContainer)
        {
            Destroy(child.gameObject);
        }

        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("[ShopUI] A lista de itens recebida está vazia ou nula!");
            return;
        }

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
            else
            {
                Debug.LogError($"[ShopUI] ShopItemSlotUI não foi encontrado no slotPrefab '{slotPrefab.name}'!");
            }
        }

        RefreshAllSlots();
        Debug.Log($"[ShopUI] Grade populada com sucesso: {activeSlots.Count} itens disponíveis à venda.");
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
            fragmentsBalanceText.text = $"Fragmentos: {frags}";
        }

        if (starsBalanceText != null)
        {
            starsBalanceText.text = $"Estrelas: {stars}";
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
