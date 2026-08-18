using UnityEngine;

public enum ShopItemEffectType
{
    HealHealth,           // Restaura vida instantaneamente
    IncreaseMaxHealth,    // Aumenta a vida máxima do jogador
    IncreaseMaxMana,      // Aumenta a mana máxima do jogador
    IncreaseManaRegen,    // Aumenta a velocidade de regeneração de mana
    IncreaseDamage,       // Aumenta o dano de combate
    IncreaseMoveSpeed     // Aumenta a velocidade de caminhada e corrida
}

public enum CurrencyType
{
    Stars,          // Moeda permanente (Estrelas ⭐)
    StarFragments   // Moeda da fase (Fragmentos ★)
}

/// <summary>
/// ScriptableObject para definir um item/melhoria à venda na loja.
/// </summary>
[CreateAssetMenu(fileName = "NewShopItem", menuName = "Shop/Shop Item")]
public class ShopItemSO : ScriptableObject
{
    [Header("Informações do Item")]
    [SerializeField] private string itemName = "Bênção Estelar";
    [TextArea(2, 4)]
    [SerializeField] private string itemDescription = "Restaura 50 pontos de vida.";
    [SerializeField] private Sprite itemIcon;

    [Header("Preço e Moeda")]
    [SerializeField] private int price = 1;
    [SerializeField] private CurrencyType currency = CurrencyType.Stars;

    [Header("Efeito no Jogador")]
    [SerializeField] private ShopItemEffectType effectType = ShopItemEffectType.HealHealth;
    [SerializeField] private float effectValue = 50f;

    [Header("Limites de Compra")]
    [Tooltip("0 para compras ilimitadas; número maior que 0 para limitar a quantidade de compras.")]
    [SerializeField] private int maxPurchases = 0;

    public string ItemName => itemName;
    public string ItemDescription => itemDescription;
    public Sprite ItemIcon => itemIcon;
    public int Price => price;
    public CurrencyType Currency => currency;
    public ShopItemEffectType EffectType => effectType;
    public float EffectValue => effectValue;
    public int MaxPurchases => maxPurchases;

    /// <summary>
    /// Aplica o efeito do item no jogador e dispara feedback visual.
    /// </summary>
    public bool ApplyEffect(GameObject player)
    {
        if (player == null) return false;

        PlayerStats stats = player.GetComponent<PlayerStats>();
        CharacterController2D character = player.GetComponent<CharacterController2D>();
        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();

        string floatingText = "";
        Color effectColor = Color.green;

        switch (effectType)
        {
            case ShopItemEffectType.HealHealth:
                if (stats != null)
                {
                    stats.Heal(effectValue);
                    floatingText = $"+{effectValue:F0} Vida!";
                    effectColor = new Color(0.2f, 1f, 0.4f);
                }
                break;

            case ShopItemEffectType.IncreaseMaxHealth:
                if (stats != null)
                {
                    stats.IncreaseMaxHealth(effectValue);
                    floatingText = $"+{effectValue:F0} Vida Máxima!";
                    effectColor = new Color(0.2f, 1f, 0.2f);
                }
                break;

            case ShopItemEffectType.IncreaseMaxMana:
                if (stats != null)
                {
                    stats.IncreaseMaxMana(effectValue);
                    floatingText = $"+{effectValue:F0} Mana Máxima!";
                    effectColor = new Color(0.2f, 0.7f, 1f);
                }
                break;

            case ShopItemEffectType.IncreaseManaRegen:
                if (stats != null)
                {
                    stats.IncreaseManaRegen(effectValue);
                    floatingText = $"+{effectValue:F1} Regen de Mana!";
                    effectColor = new Color(0.3f, 0.8f, 1f);
                }
                break;

            case ShopItemEffectType.IncreaseDamage:
                if (combat != null)
                {
                    combat.IncreaseDamage(effectValue);
                    floatingText = $"+{effectValue:F0} Dano!";
                    effectColor = new Color(1f, 0.3f, 0.3f);
                }
                break;

            case ShopItemEffectType.IncreaseMoveSpeed:
                if (character != null)
                {
                    character.IncreaseMovementSpeed(effectValue);
                    floatingText = $"+{effectValue:F1} Velocidade!";
                    effectColor = new Color(1f, 0.9f, 0.2f);
                }
                break;
        }

        if (CombatVisualEffects.Instance != null && !string.IsNullOrEmpty(floatingText))
        {
            CombatVisualEffects.Instance.SpawnFloatingText(player.transform.position + Vector3.up * 1.5f, floatingText, effectColor, 4.5f);
            CombatVisualEffects.Instance.PlayImpactBurst(player.transform.position, effectColor, 1.2f);
        }

        Debug.Log($"[ShopItemSO] Item '{itemName}' aplicado com sucesso! ({effectType}: {effectValue})");
        return true;
    }
}
