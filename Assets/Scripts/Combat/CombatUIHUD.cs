using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MonoBehaviour responsável por gerenciar a HUD de combate (uGUI + TextMeshPro).
/// Exibe slots do lado direito (LMB e RMB) e slots do lado esquerdo (Q, E, R),
/// atualizando preenchimento de cooldown, textos de contagem e barra/brilho da Ultimate.
/// </summary>
public class CombatUIHUD : MonoBehaviour
{
    [Header("Referência ao Controller do Jogador")]
    [SerializeField] private PlayerCombatController playerCombat;

    [Header("Slots do Canto Inferior Direito (Ataques Básicos)")]
    [Tooltip("Ícone do Ataque Melee (Mouse Esquerdo).")]
    [SerializeField] private Image meleeIcon;
    [Tooltip("Overlay de Cooldown do Ataque Melee.")]
    [SerializeField] private Image meleeCooldownOverlay;

    [Tooltip("Ícone do Ataque Ranged (Mouse Direito).")]
    [SerializeField] private Image rangedIcon;
    [Tooltip("Overlay de Cooldown do Ataque Ranged.")]
    [SerializeField] private Image rangedCooldownOverlay;

    [Header("Slots do Canto Inferior Esquerdo (Habilidades Q e E)")]
    [Tooltip("Ícone do Slot Q.")]
    [SerializeField] private Image slotQIcon;
    [Tooltip("Overlay de Cooldown do Slot Q.")]
    [SerializeField] private Image slotQCooldownOverlay;
    [Tooltip("Texto numérico da contagem regressiva do Slot Q.")]
    [SerializeField] private TextMeshProUGUI slotQCooldownText;

    [Tooltip("Ícone do Slot E.")]
    [SerializeField] private Image slotEIcon;
    [Tooltip("Overlay de Cooldown do Slot E.")]
    [SerializeField] private Image slotECooldownOverlay;
    [Tooltip("Texto numérico da contagem regressiva do Slot E.")]
    [SerializeField] private TextMeshProUGUI slotECooldownText;

    [Header("Slot do Canto Inferior Esquerdo (R)")]
    [Tooltip("Ícone do Slot R.")]
    [SerializeField] private Image slotRIcon;
    [Tooltip("Overlay de Cooldown do Slot R.")]
    [SerializeField] private Image slotRCooldownOverlay;
    [Tooltip("Texto numérico da contagem regressiva do Slot R.")]

    [SerializeField] private TextMeshProUGUI slotRCooldownText;

    [SerializeField] Sprite emptySkillIcon;

    private void OnEnable()
    {
        if (playerCombat == null)
        {
            playerCombat = Object.FindAnyObjectByType<PlayerCombatController>();
        }

        if (playerCombat != null)
        {
            SubscribeEvents();
        }
    }

    private void OnDisable()
    {
        if (playerCombat != null)
        {
            UnsubscribeEvents();
        }
    }

    private void Start()
    {
        if (playerCombat == null)
        {
            playerCombat = Object.FindAnyObjectByType<PlayerCombatController>();
        }

        if (playerCombat != null)
        {
            SubscribeEvents();
        }

        SetupHoverHandlers();
    }

    private void SetupHoverHandlers()
    {
        AttachHoverHandler(meleeIcon != null ? meleeIcon.transform.parent.gameObject : null, PlayerCombatController.PendingActionType.Melee);
        AttachHoverHandler(rangedIcon != null ? rangedIcon.transform.parent.gameObject : null, PlayerCombatController.PendingActionType.Ranged);
        AttachHoverHandler(slotQIcon != null ? slotQIcon.transform.parent.gameObject : null, PlayerCombatController.PendingActionType.AbilityQ);
        AttachHoverHandler(slotEIcon != null ? slotEIcon.transform.parent.gameObject : null, PlayerCombatController.PendingActionType.AbilityE);
        AttachHoverHandler(slotRIcon != null ? slotRIcon.transform.parent.gameObject : null, PlayerCombatController.PendingActionType.AbilityR);
    }

    private void AttachHoverHandler(GameObject slotObj, PlayerCombatController.PendingActionType actionType)
    {
        if (slotObj == null) return;
        SkillSlotHoverHandler handler = slotObj.GetComponent<SkillSlotHoverHandler>();
        if (handler == null)
        {
            handler = slotObj.AddComponent<SkillSlotHoverHandler>();
        }
        handler.Setup(playerCombat, actionType);
    }

    private void SubscribeEvents()
    {
        UnsubscribeEvents(); // Evita inscrições duplicadas

        playerCombat.OnBasicCooldownsUpdated += HandleBasicCooldowns;
        playerCombat.OnAbilityCooldownUpdated += HandleAbilityCooldown;
        playerCombat.OnEquippedAbilitiesChanged += UpdateEquippedAbilities;
        playerCombat.OnSlotUnlockStateChanged += HandleSlotUnlockStateChanged;
    }

    private void UnsubscribeEvents()
    {
        if (playerCombat == null) return;

        playerCombat.OnBasicCooldownsUpdated -= HandleBasicCooldowns;
        playerCombat.OnAbilityCooldownUpdated -= HandleAbilityCooldown;
        playerCombat.OnEquippedAbilitiesChanged -= UpdateEquippedAbilities;
        playerCombat.OnSlotUnlockStateChanged -= HandleSlotUnlockStateChanged;
    }

    private void HandleSlotUnlockStateChanged(int slotIndex, bool isUnlocked)
    {
        if (playerCombat == null) return;
        UpdateEquippedAbilities(
            playerCombat.GetEquippedAbility(0),
            playerCombat.GetEquippedAbility(1),
            playerCombat.GetEquippedAbility(2)
        );
    }

    /// <summary>
    /// Atualiza os ícones e a exibição das habilidades equipadas nos slots Q, E e R.
    /// Suporta habilidades trocadas dinamicamente, slots vazios (null) e slots bloqueados.
    /// </summary>
    public void UpdateEquippedAbilities(Ability abilityQ, Ability abilityE, Ability abilityR)
    {
        bool isEUnlocked = playerCombat == null || playerCombat.IsSlotUnlocked(1);
        bool isRUnlocked = playerCombat == null || playerCombat.IsSlotUnlocked(2);

        if (slotQIcon != null)
        {
            slotQIcon.sprite = abilityQ != null ? abilityQ.Icon : null;
            slotQIcon.gameObject.SetActive(abilityQ != null && abilityQ.Icon != null);
            if (abilityQ == null)
            {
                if (slotQCooldownOverlay != null) slotQCooldownOverlay.fillAmount = 0f;
                if (slotQCooldownText != null) slotQCooldownText.gameObject.SetActive(false);
            }
        }

        if (slotEIcon != null)
        {
            slotEIcon.sprite = (isEUnlocked && abilityE != null) ? abilityE.Icon : null;
            slotEIcon.gameObject.SetActive(isEUnlocked && abilityE != null && abilityE.Icon != null);
            if (!isEUnlocked || abilityE == null)
            {
                if (slotECooldownOverlay != null) slotECooldownOverlay.fillAmount = 0f;
                if (slotECooldownText != null) slotECooldownText.gameObject.SetActive(false);
            }
        }

        if (slotRIcon != null)
        {
            slotRIcon.sprite = (isRUnlocked && abilityR != null) ? abilityR.Icon : null;
            slotRIcon.gameObject.SetActive(isRUnlocked && abilityR != null && abilityR.Icon != null);
            if (!isRUnlocked || abilityR == null)
            {
                if (slotRCooldownOverlay != null) slotRCooldownOverlay.fillAmount = 0f;
                if (slotRCooldownText != null) slotRCooldownText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Atualiza overlays de cooldown dos ataques básicos (LMB e RMB).
    /// </summary>
    private void HandleBasicCooldowns(float meleeRem, float meleeMax, float rangedRem, float rangedMax)
    {
        if (meleeCooldownOverlay != null)
        {
            meleeCooldownOverlay.fillAmount = meleeMax > 0f ? (meleeRem / meleeMax) : 0f;
        }

        if (rangedCooldownOverlay != null)
        {
            rangedCooldownOverlay.fillAmount = rangedMax > 0f ? (rangedRem / rangedMax) : 0f;
        }
    }

    /// <summary>
    /// Atualiza overlay de cooldown e texto regressivo para o slot da habilidade indicada.
    /// </summary>
    private void HandleAbilityCooldown(int slotIndex, float remaining, float max)
    {
        float fill = max > 0f ? (remaining / max) : 0f;
        bool isCooldown = remaining > 0.05f;

        switch (slotIndex)
        {
            case 0: // Slot Q
                if (slotQCooldownOverlay != null) slotQCooldownOverlay.fillAmount = fill;
                if (slotQCooldownText != null)
                {
                    slotQCooldownText.gameObject.SetActive(isCooldown);
                    slotQCooldownText.text = isCooldown ? $"{remaining:F1}" : "";
                }
                break;

            case 1: // Slot E
                if (slotECooldownOverlay != null) slotECooldownOverlay.fillAmount = fill;
                if (slotECooldownText != null)
                {
                    slotECooldownText.gameObject.SetActive(isCooldown);
                    slotECooldownText.text = isCooldown ? $"{remaining:F1}" : "";
                }
                break;

            case 2: // Slot R (Ultimate)
                if (slotRCooldownOverlay != null) slotRCooldownOverlay.fillAmount = fill;
                if (slotRCooldownText != null)
                {
                    slotRCooldownText.gameObject.SetActive(isCooldown);
                    slotRCooldownText.text = isCooldown ? $"{remaining:F1}" : "";
                }
                break;
        }
    }

   
}
