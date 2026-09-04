using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla visualmente e funcionalmente um card/slot de item na grade da Loja.
/// </summary>
public class ShopItemSlotUI : MonoBehaviour
{
    [Header("Elementos de UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI buyButtonText;
    [SerializeField] private GameObject soldOutBanner;

    private ShopItemSO currentItem;
    private ShopUI shopUI;
    private int purchaseCount = 0;

    public ShopItemSO CurrentItem => currentItem;
    public bool IsSoldOut => currentItem != null && currentItem.MaxPurchases > 0 && purchaseCount >= currentItem.MaxPurchases;

    /// <summary>
    /// Inicializa e configura os dados do slot com base no ShopItemSO e no histórico de compras atual.
    /// </summary>
    public void Setup(ShopItemSO item, ShopUI parentShop, int currentBought = 0)
    {
        currentItem = item;
        shopUI = parentShop;
        purchaseCount = currentBought;

        if (nameText != null)
        {
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 11f;
            nameText.fontSizeMax = 16f;
            nameText.textWrappingMode = TextWrappingModes.Normal;

            if (item.MaxPurchases == 1)
            {
                nameText.text = $"<b>{item.ItemName}</b> <size=11><color=#88CCFF>[Único]</color></size>";
            }
            else if (item.MaxPurchases > 1)
            {
                int remaining = Mathf.Max(0, item.MaxPurchases - purchaseCount);
                nameText.text = $"<b>{item.ItemName}</b> <size=11><color=#88CCFF>[{remaining}x]</color></size>";
            }
            else
            {
                nameText.text = $"<b>{item.ItemName}</b>";
            }
        }

        if (descriptionText != null)
        {
            descriptionText.enableAutoSizing = true;
            descriptionText.fontSizeMin = 10f;
            descriptionText.fontSizeMax = 13f;
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.text = item.ItemDescription;
        }

        if (iconImage != null)
        {
            if (item.ItemIcon != null)
            {
                iconImage.sprite = item.ItemIcon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        UpdateVisuals();
    }

    private void OnBuyClicked()
    {
        if (shopUI != null && currentItem != null && !IsSoldOut)
        {
            bool success = shopUI.TryBuyItem(currentItem);
            if (success)
            {
                purchaseCount = ShopUI.GetPurchaseCount(currentItem);
                UpdateVisuals();
                if (shopUI != null) shopUI.RefreshAllSlots();
            }
        }
    }

    /// <summary>
    /// Atualiza o estado visual do botão, limites e cores de preço conforme o saldo do jogador.
    /// </summary>
    public void UpdateAffordability(int currentFragments, int currentStars, int boughtCount = -1)
    {
        if (currentItem == null) return;
        if (boughtCount >= 0) purchaseCount = boughtCount;

        bool canAfford = currentItem.Currency == CurrencyType.Stars 
            ? currentStars >= currentItem.Price 
            : currentFragments >= currentItem.Price;

        string currencyLabel = currentItem.Currency == CurrencyType.Stars 
            ? (currentItem.Price == 1 ? "Estrela" : "Estrelas") 
            : (currentItem.Price == 1 ? "Fragmento" : "Fragmentos");

        if (nameText != null)
        {
            if (currentItem.MaxPurchases == 1)
            {
                nameText.text = $"{currentItem.ItemName} <size=12><color=#88CCFF>[Único]</color></size>";
            }
            else if (currentItem.MaxPurchases > 1)
            {
                int remaining = Mathf.Max(0, currentItem.MaxPurchases - purchaseCount);
                nameText.text = $"{currentItem.ItemName} <size=12><color=#88CCFF>[Estoque: {remaining}]</color></size>";
            }
        }

        if (IsSoldOut)
        {
            if (priceText != null) priceText.text = "<color=#888888>ESGOTADO</color>";
            if (buyButton != null) buyButton.interactable = false;
            if (buyButtonText != null) buyButtonText.text = "Esgotado";
            if (soldOutBanner != null) soldOutBanner.SetActive(true);
        }
        else
        {
            if (soldOutBanner != null) soldOutBanner.SetActive(false);

            if (priceText != null)
            {
                string priceColor = canAfford ? "#FFD700" : "#FF5555";
                priceText.text = $"<color={priceColor}>{currentItem.Price} {currencyLabel}</color>";
            }

            if (buyButton != null)
            {
                buyButton.interactable = canAfford;
            }

            if (buyButtonText != null)
            {
                buyButtonText.text = canAfford ? "Comprar" : "Sem saldo";
            }
        }
    }

    private void UpdateVisuals()
    {
        if (PlayerCurrency.Instance != null)
        {
            UpdateAffordability(PlayerCurrency.Instance.StarFragments, PlayerCurrency.Instance.Stars, purchaseCount);
        }
    }
}
