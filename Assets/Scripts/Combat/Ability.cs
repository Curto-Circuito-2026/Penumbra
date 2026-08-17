using UnityEngine;

/// <summary>
/// Classe base abstrata para habilidades do sistema de combate modular.
/// Armazenada como ScriptableObject para facilidade de criação e balanceamento via Inspector.
/// </summary>
public abstract class Ability : ScriptableObject
{
    [Header("Informações Gerais")]
    [Tooltip("Nome da habilidade.")]
    [SerializeField] protected string abilityName = "Nova Habilidade";

    [Tooltip("Descrição detalhada do efeito/o que a habilidade faz.")]
    [TextArea(3, 5)]
    [SerializeField] protected string description = "Descrição da habilidade.";

    [Tooltip("Ícone exibido na HUD para esta habilidade.")]
    [SerializeField] protected Sprite icon;

    [Header("Atributos de Balanceamento")]
    [Tooltip("Tempo de recarga (cooldown) em segundos.")]
    [SerializeField] protected float cooldown = 5f;

    [Tooltip("Alcance máximo de conjuração em unidades do mundo.")]
    [SerializeField] protected float range = 5f;

    [Tooltip("Dano causado pela habilidade.")]
    [SerializeField] protected float damage = 25f;

    [Tooltip("Custo de Mana para conjurar a habilidade.")]
    [SerializeField] protected float manaCost = 0f;

    [Tooltip("Custo de energia/estamina (ou carga necessária).")]
    [SerializeField] protected float cost = 0f;

    // Propriedades públicas para leitura
    public string AbilityName => abilityName;
    public string Description => description;
    public Sprite Icon => icon;
    public float Cooldown => cooldown;
    public float Range => range;
    public float Damage => damage;
    public float ManaCost => manaCost;
    public float Cost => cost;

    /// <summary>
    /// Permite alterar programaticamente as informações da habilidade (nome, ícone, descrição, recarga, mana e dano).
    /// </summary>
    public void SetSkillDetails(string newName, Sprite newIcon, string newDescription, float newCooldown = -1f, float newManaCost = -1f, float newDamage = -1f)
    {
        if (!string.IsNullOrEmpty(newName)) abilityName = newName;
        if (newIcon != null) icon = newIcon;
        if (!string.IsNullOrEmpty(newDescription)) description = newDescription;
        if (newCooldown >= 0f) cooldown = newCooldown;
        if (newManaCost >= 0f) manaCost = newManaCost;
        if (newDamage >= 0f) damage = newDamage;
    }

    /// <summary>
    /// Método abstrato invocado quando a habilidade é conjurada.
    /// </summary>
    /// <param name="caster">O GameObject do jogador/entidade que lançou a habilidade.</param>
    /// <param name="targetPosition">A posição no mundo onde a habilidade foi mirada.</param>
    /// <param name="targetEntity">Entidade alvo atingida/mirada (se houver).</param>
    /// <returns>Verdadeiro se a habilidade foi executada com sucesso.</returns>
    public abstract bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity);
}

