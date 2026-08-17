using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Componente anexado a um slot da HUD de combate para detectar a entrada/saída do cursor do mouse (hover)
/// e solicitar a exibição do alcance do ataque/habilidade e do Tooltip com nome/descrição/cooldown/mana.
/// </summary>
public class SkillSlotHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private PlayerCombatController.PendingActionType actionType;
    private PlayerCombatController combatController;

    public void Setup(PlayerCombatController controller, PlayerCombatController.PendingActionType type)
    {
        combatController = controller;
        actionType = type;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (combatController == null)
        {
            combatController = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        }

        if (combatController != null)
        {
            combatController.SetHudHoverAction(actionType);

            // Exibe o Tooltip com nome, descrição, cooldown e custo de mana
            combatController.GetSkillTooltipData(actionType, out string skillName, out string description, out float cooldown, out float manaCost, out Sprite icon);

            if (SkillTooltipUI.Instance != null)
            {
                SkillTooltipUI.Instance.ShowTooltip(skillName, description, cooldown, manaCost, icon);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (combatController == null)
        {
            combatController = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        }

        if (combatController != null)
        {
            combatController.ClearHudHoverAction();
        }

        if (SkillTooltipUI.Instance != null)
        {
            SkillTooltipUI.Instance.HideTooltip();
        }
    }
}

