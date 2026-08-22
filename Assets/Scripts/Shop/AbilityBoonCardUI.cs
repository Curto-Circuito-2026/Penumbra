using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla um Card individual de Bênção/Habilidade na interface de Troca estilo Hades II.
/// </summary>
public class AbilityBoonCardUI : MonoBehaviour
{
    [Header("Elementos de Texto")]
    [SerializeField] private TextMeshProUGUI boonNameText;
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI statDetailText;
    [SerializeField] private TextMeshProUGUI costText;

    [Header("Elementos Visuais")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image cardBackgroundImage;

    [SerializeField] private Sprite StarFullSprite;
    [SerializeField] private Sprite StarEmptySprite;
    [SerializeField] private Image StarElement;


    [Header("Controles")]
    [SerializeField] private Button selectButton;
    [SerializeField] private TextMeshProUGUI buyButtonText;

    private AbilityBoonSO currentBoon;
    private AbilitySwapUI parentUI;

    public AbilityBoonSO CurrentBoon => currentBoon;

    /// <summary>
    /// Configura o card com base no ScriptableObject da bênção e vincula ao gerenciador de troca.
    /// </summary>
    public void Setup(AbilityBoonSO boon, AbilitySwapUI swapUI)
    {
        currentBoon = boon;
        parentUI = swapUI;

        if (boon == null) return;

        string rarityColorHex = boon.GetRarityHexColor();

        // 1. Nome da Bênção
        if (boonNameText != null)
        {
            boonNameText.text = $"<color={rarityColorHex}><b>{boon.BoonName}</b></color>";
        }

        // 2. Tag de Raridade
        if (rarityText != null)
        {
            rarityText.text = $"<color={rarityColorHex}><b>[{boon.GetRarityDisplayName()}]</b></color>";
        }

        // 3. Descrição
        if (descriptionText != null)
        {
            descriptionText.text = boon.Description;
        }

        // 4. Detalhe Estatístico (Benefício em Verde + Contrapartida em Vermelho se for um Acordo)
        if (statDetailText != null)
        {
            string detail = $"<color=#66FFAA>{boon.StatDetail}</color>";
            if (boon.HasDebuff)
            {
                detail += $"   <color=#FF6666><b>{boon.DebuffDetail}</b></color>";
            }
            statDetailText.text = detail;
        }

        // 5. Ícone
        if (iconImage != null)
        {
            if (boon.Icon != null)
            {
                iconImage.sprite = boon.Icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

       

        // 7. Botão de Seleção
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        // 8. Atualiza disponibilidade com base no saldo de estrelas
        UpdateAffordability();
    }

    /// <summary>
    /// Atualiza o texto de custo e o estado interativo do card conforme as estrelas do jogador.
    /// </summary>
    public void UpdateAffordability()
    {
        if (currentBoon == null) return;

        PlayerCurrency currency = PlayerCurrency.Instance ?? Object.FindAnyObjectByType<PlayerCurrency>();
        int playerStars = currency != null ? currency.Stars : 0;
        bool canAfford = playerStars >= currentBoon.StarCost;

        if (StarElement != null)
        {
            if (canAfford && StarFullSprite != null)
            {
                StarElement.sprite = StarFullSprite;
            }
            else if (!canAfford && StarEmptySprite != null)
            {
                StarElement.sprite = StarEmptySprite;
            }
        }

        if (costText != null)
        {
            string costLabel = currentBoon.StarCost.ToString();
            costText.text = $"{costLabel}";
        }
    }

    public void OnSelectClicked()
    {
        if (parentUI != null && currentBoon != null)
        {
            parentUI.SelectBoon(currentBoon);
        }
    }
}
