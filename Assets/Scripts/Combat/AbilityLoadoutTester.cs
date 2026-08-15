using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Componente auxiliar para testar a troca dinâmica de habilidades (Q, E, R) em tempo de execução.
/// Permite equipar e desequipar habilidades pressionando as teclas numéricas 1, 2 e 3.
/// </summary>
public class AbilityLoadoutTester : MonoBehaviour
{
    [Header("Referência ao Controller")]
    [SerializeField] private PlayerCombatController combatController;

    [Header("Biblioteca de Habilidades Disponíveis")]
    [SerializeField] private Ability optionQ;
    [SerializeField] private Ability optionE;
    [SerializeField] private Ability optionR;

    private void Start()
    {
        if (combatController == null)
        {
            combatController = GetComponent<PlayerCombatController>();
            if (combatController == null)
            {
                combatController = Object.FindAnyObjectByType<PlayerCombatController>();
            }
        }
    }

    private void Update()
    {
        if (combatController == null) return;

        // Tecla 1 - Alterna/Equipa no Slot Q (0)
        if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            ToggleSlot(0, optionQ);
        }

        // Tecla 2 - Alterna/Equipa no Slot E (1)
        if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            ToggleSlot(1, optionE);
        }

        // Tecla 3 - Alterna/Equipa no Slot R (2)
        if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            ToggleSlot(2, optionR);
        }
    }

    private void ToggleSlot(int slotIndex, Ability targetAbility)
    {
        Ability current = combatController.GetEquippedAbility(slotIndex);

        if (current == null)
        {
            combatController.EquipAbility(slotIndex, targetAbility);
        }
        else
        {
            // Se já tiver uma habilidade equipada, deixa o slot vazio (null)
            combatController.UnequipAbility(slotIndex);
        }
    }
}
