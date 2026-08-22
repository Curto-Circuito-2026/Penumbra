using System;
using UnityEngine;

/// <summary>
/// Raridades das Bênçãos e Magias dos Mestres Folclóricos.
/// </summary>
public enum BoonRarity
{
    Comum,
    Rara,
    Epica,
    Heroica,
    Lendaria
}

/// <summary>
/// Define uma Bênção, Magia ou Acordo Folclórico com benefícios e contrapartidas opcionais.
/// </summary>
[CreateAssetMenu(fileName = "NewBoon", menuName = "Praia Games/Bênçãos/Nova Bênção (Boon)", order = 1)]
public class AbilityBoonSO : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Nome da bênção ou acordo exibido em destaque no card.")]
    [SerializeField] private string boonName = "Passos do Redemoinho";

    [Tooltip("Raridade da bênção (Comum, Rara, Épica, Heroica, Lendária).")]
    [SerializeField] private BoonRarity rarity = BoonRarity.Rara;

    [Tooltip("Ícone ilustrativo exibido na moldura esquerda do card.")]
    [SerializeField] private Sprite icon;

    [Header("Textos Descritivos")]
    [Tooltip("Descrição detalhada do efeito e funcionamento da habilidade.")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "Seu Dash cria uma rajada de vento cortante que empurra e fere inimigos na área.";

    [Tooltip("Texto com marcadores de benefício (ex: '▸ Dano do Ataque: +25').")]
    [SerializeField] private string statDetail = "▸ Dano do Ataque: +25";

    [Header("Efeito Positivo (Benefício)")]
    [Tooltip("Tipo de efeito que esta bênção aplica aos status ou combate do jogador.")]
    [SerializeField] private ShopItemEffectType effectType = ShopItemEffectType.IncreaseDamage;

    [Tooltip("Valor numérico do benefício (ex: +25 de dano, +1.5 de velocidade, etc.).")]
    [SerializeField] private float effectValue = 25f;

    [Header("Contrapartida / Penalidade (Opcional - Usado em Acordos do Saci)")]
    [Tooltip("Tipo de penalidade associada a este acordo.")]
    [SerializeField] private ShopItemEffectType debuffEffectType = ShopItemEffectType.IncreaseMaxHealth;

    [Tooltip("Valor numérico da penalidade aplicada ao jogador (ex: 30 de vida máxima reduzida).")]
    [SerializeField] private float debuffValue = 0f;

    [Tooltip("Texto explicativo da penalidade exibido em vermelho no card (ex: '- Vida Máxima: -30').")]
    [SerializeField] private string debuffDetail = "";

    [Header("Custo em Estrelas (Moeda da Loja)")]
    [Tooltip("Preço em Estrelas para comprar/adquirir esta habilidade ou acordo.")]
    [SerializeField] private int starCost = 1;

    [Header("Habilidade Ativa Vinculada (Opcional)")]
    [Tooltip("Habilidade ativa que será equipada nos slots Q, E ou R.")]
    [SerializeField] private Ability grantedAbility;

    public string BoonName => boonName;
    public BoonRarity Rarity => rarity;
    public int StarCost => starCost;
    public Ability GrantedAbility => grantedAbility;
    public Sprite Icon => icon;
    public string Description => description;
    public string StatDetail => statDetail;
    public ShopItemEffectType EffectType => effectType;
    public float EffectValue => effectValue;

    public ShopItemEffectType DebuffEffectType => debuffEffectType;
    public float DebuffValue => debuffValue;
    public string DebuffDetail => debuffDetail;
    public bool HasDebuff => debuffValue > 0f && !string.IsNullOrEmpty(debuffDetail);

    /// <summary>
    /// Retorna o código de cor hexadecimal para a raridade correspondente.
    /// </summary>
    public string GetRarityHexColor()
    {
        return rarity switch
        {
            BoonRarity.Comum => "#E0E0E0",     // Branco/Prata
            BoonRarity.Rara => "#4FC3F7",      // Ciano / Águas e Ventos
            BoonRarity.Epica => "#BA68C8",     // Roxo Místico
            BoonRarity.Heroica => "#FF8A65",   // Laranja Flamejante
            BoonRarity.Lendaria => "#FFD54F",  // Dourado Radiante
            _ => "#FFFFFF"
        };
    }

    /// <summary>
    /// Retorna a tag de exibição da raridade formatada em maiúsculas.
    /// </summary>
    public string GetRarityDisplayName()
    {
        return rarity switch
        {
            BoonRarity.Comum => "COMUM",
            BoonRarity.Rara => "RARA",
            BoonRarity.Epica => "ÉPICA",
            BoonRarity.Heroica => "HERÓICA",
            BoonRarity.Lendaria => "LENDÁRIA",
            _ => "BÊNÇÃO"
        };
    }

    /// <summary>
    /// Aplica o efeito da bênção selecionada (e penalidades se houver) diretamente no jogador.
    /// </summary>
    public bool ApplyBoon(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("[AbilityBoonSO] Jogador não encontrado para aplicar a bênção!");
            return false;
        }

        PlayerStats stats = player.GetComponent<PlayerStats>();
        CharacterController2D character = player.GetComponent<CharacterController2D>();
        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();

        string floatingText = "";
        Color effectColor = Color.cyan;

        // 1. Aplica o Benefício
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
                    floatingText = $"+{effectValue:F0} Vida Máx!";
                    effectColor = new Color(0.2f, 1f, 0.2f);
                }
                break;

            case ShopItemEffectType.IncreaseMaxMana:
                if (stats != null)
                {
                    stats.IncreaseMaxMana(effectValue);
                    floatingText = $"+{effectValue:F0} Mana Máx!";
                    effectColor = new Color(0.2f, 0.7f, 1f);
                }
                break;

            case ShopItemEffectType.IncreaseManaRegen:
                if (stats != null)
                {
                    stats.IncreaseManaRegen(effectValue);
                    floatingText = $"+{effectValue:F1} Regen Mana!";
                    effectColor = new Color(0.3f, 0.8f, 1f);
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

            case ShopItemEffectType.IncreaseDamage:
                if (combat != null)
                {
                    combat.IncreaseDamage(effectValue);
                    floatingText = $"+{effectValue:F0} Dano!";
                    effectColor = new Color(1f, 0.3f, 0.3f);
                }
                break;

            default:
                Debug.LogWarning($"[AbilityBoonSO] Tipo de efeito '{effectType}' não tratado em ApplyBoon.");
                break;
        }

        if (CombatVisualEffects.Instance != null && !string.IsNullOrEmpty(floatingText))
        {
            CombatVisualEffects.Instance.SpawnFloatingText(player.transform.position + Vector3.up * 1.5f, floatingText, effectColor, 4.5f);
            CombatVisualEffects.Instance.PlayImpactBurst(player.transform.position, effectColor, 1.2f);
        }

        // 2. Aplica a Contrapartida / Penalidade (se for um Acordo)
        if (HasDebuff)
        {
            string penaltyText = "";
            Color penaltyColor = new Color(1f, 0.3f, 0.3f);

            switch (debuffEffectType)
            {
                case ShopItemEffectType.IncreaseMaxHealth:
                    if (stats != null)
                    {
                        stats.IncreaseMaxHealth(-debuffValue);
                        penaltyText = $"-{debuffValue:F0} Vida Máx!";
                    }
                    break;

                case ShopItemEffectType.IncreaseMaxMana:
                    if (stats != null)
                    {
                        stats.IncreaseMaxMana(-debuffValue);
                        penaltyText = $"-{debuffValue:F0} Mana Máx!";
                    }
                    break;

                case ShopItemEffectType.IncreaseManaRegen:
                    if (stats != null)
                    {
                        stats.IncreaseManaRegen(-debuffValue);
                        penaltyText = $"-{debuffValue:F1} Regen Mana!";
                    }
                    break;

                case ShopItemEffectType.IncreaseMoveSpeed:
                    if (character != null)
                    {
                        character.IncreaseMovementSpeed(-debuffValue);
                        penaltyText = $"-{debuffValue:F1} Velocidade!";
                    }
                    break;

                case ShopItemEffectType.IncreaseDamage:
                    if (combat != null)
                    {
                        combat.IncreaseDamage(-debuffValue);
                        penaltyText = $"-{debuffValue:F0} Dano!";
                    }
                    break;

                case ShopItemEffectType.HealHealth:
                    if (stats != null)
                    {
                        stats.TakeDamage(debuffValue, Vector3.zero);
                        penaltyText = $"-{debuffValue:F0} Vida!";
                    }
                    break;
            }

            if (CombatVisualEffects.Instance != null && !string.IsNullOrEmpty(penaltyText))
            {
                CombatVisualEffects.Instance.SpawnFloatingText(player.transform.position + Vector3.up * 2.2f, penaltyText, penaltyColor, 4.5f);
            }
        }

        Debug.Log($"[AbilityBoonSO] Bênção/Acordo '{boonName}' aplicado com sucesso! Benefício: ({effectType}: {effectValue})" + (HasDebuff ? $" | Penalidade: ({debuffEffectType}: -{debuffValue})" : ""));
        return true;
    }
}
