using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Modal de Equipamento de Habilidade nos Slots Q, E ou R.
/// Permite ao jogador escolher em qual slot deseja equipar a habilidade selecionada na loja,
/// debitando as Estrelas somente após a confirmação final do slot.
/// </summary>
public class SkillEquipModalUI : MonoBehaviour
{
    public static SkillEquipModalUI Instance { get; private set; }

    public bool IsOpen => modalPanel != null && modalPanel.activeSelf;
    private int openFrame = -1;

    [Header("Painel Raiz")]
    [SerializeField] private GameObject modalPanel;

    [Header("Detalhes da Habilidade Selecionada")]
    [SerializeField] private Image selectedSkillIcon;
    [SerializeField] private TextMeshProUGUI selectedSkillNameText;
    [SerializeField] private TextMeshProUGUI selectedSkillRarityText;
    [SerializeField] private TextMeshProUGUI selectedSkillDescText;
    [SerializeField] private TextMeshProUGUI selectedSkillStatsText;

    [Header("Slots Interativos de Destino")]
    [SerializeField] private Button slotQButton;
    [SerializeField] private Image slotQIcon;
    [SerializeField] private TextMeshProUGUI slotQNameText;
    [SerializeField] private GameObject slotQHighlight;

    [SerializeField] private Button slotEButton;
    [SerializeField] private Image slotEIcon;
    [SerializeField] private TextMeshProUGUI slotENameText;
    [SerializeField] private GameObject slotEHighlight;

    [SerializeField] private Button slotRButton;
    [SerializeField] private Image slotRIcon;
    [SerializeField] private TextMeshProUGUI slotRNameText;
    [SerializeField] private GameObject slotRHighlight;

    [Header("Botões de Ação")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;
    [SerializeField] private Button cancelButton;

    private AbilityBoonSO pendingBoon;
    private AbilitySwapUI parentSwapUI;
    private int selectedSlotIndex = 0; // 0 = Q, 1 = E, 2 = R

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        if (slotQButton != null) slotQButton.onClick.AddListener(() => SelectSlot(0));
        if (slotEButton != null) slotEButton.onClick.AddListener(() => SelectSlot(1));
        if (slotRButton != null) slotRButton.onClick.AddListener(() => SelectSlot(2));

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmEquip);
        if (cancelButton != null) cancelButton.onClick.AddListener(CloseModal);

        if (modalPanel != null) modalPanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount == openFrame) return;

        bool closePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (closePressed)
        {
            CloseModal();
        }
    }

    /// <summary>
    /// Abre o modal de escolha de slot com a bênção/habilidade selecionada.
    /// </summary>
    public void OpenModal(AbilityBoonSO boon, AbilitySwapUI swapUI)
    {
        pendingBoon = boon;
        parentSwapUI = swapUI;
        selectedSlotIndex = 0; // Default para Slot Q
        openFrame = Time.frameCount;

        transform.SetAsLastSibling();
        if (modalPanel != null)
        {
            modalPanel.SetActive(true);
            modalPanel.transform.SetAsLastSibling();
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null && modalPanel != null) canvas = modalPanel.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;
        }

        UpdateSkillDetails();
        RefreshSlotPreviews();
        SelectSlot(selectedSlotIndex);
    }

    private void UpdateSkillDetails()
    {
        if (pendingBoon == null) return;

        string rarityHex = pendingBoon.GetRarityHexColor();

        if (selectedSkillNameText != null)
        {
            selectedSkillNameText.text = $"<color={rarityHex}><b>{pendingBoon.BoonName}</b></color>";
        }

        if (selectedSkillRarityText != null)
        {
            selectedSkillRarityText.text = $"<color={rarityHex}>[{pendingBoon.GetRarityDisplayName()}]</color>";
        }

        if (selectedSkillDescText != null)
        {
            selectedSkillDescText.text = pendingBoon.Description;
        }

        if (selectedSkillIcon != null)
        {
            selectedSkillIcon.sprite = pendingBoon.Icon;
            selectedSkillIcon.gameObject.SetActive(pendingBoon.Icon != null);
        }

        if (selectedSkillStatsText != null)
        {
            if (pendingBoon.GrantedAbility != null)
            {
                Ability ab = pendingBoon.GrantedAbility;
                selectedSkillStatsText.text = $"<color=#66FFAA>Recarga: {ab.Cooldown:F1}s</color>  |  <color=#4FC3F7>Mana: {ab.ManaCost:F0}</color>  |  <color=#FFD54F>Dano: {ab.Damage:F0}</color>";
            }
            else
            {
                selectedSkillStatsText.text = $"<color=#66FFAA>{pendingBoon.StatDetail}</color>";
            }
        }
    }

    private void RefreshSlotPreviews()
    {
        PlayerCombatController combat = Object.FindAnyObjectByType<PlayerCombatController>();
        if (combat == null) return;

        bool isEUnlocked = combat.IsSlotUnlocked(1);
        bool isRUnlocked = combat.IsSlotUnlocked(2);

        // Slot Q (Sempre Desbloqueado)
        if (slotQButton != null) slotQButton.interactable = true;
        Ability q = combat.GetEquippedAbility(0);
        if (slotQNameText != null) slotQNameText.text = q != null ? q.AbilityName : "<color=#888888>Vazio</color>";
        if (slotQIcon != null)
        {
            slotQIcon.sprite = q != null ? q.Icon : null;
            slotQIcon.gameObject.SetActive(q != null && q.Icon != null);
        }

        // Slot E (Requer Desbloqueio via Curupira)
        if (slotEButton != null) slotEButton.interactable = isEUnlocked;
        if (isEUnlocked)
        {
            Ability e = combat.GetEquippedAbility(1);
            if (slotENameText != null) slotENameText.text = e != null ? e.AbilityName : "<color=#888888>Vazio</color>";
            if (slotEIcon != null)
            {
                slotEIcon.sprite = e != null ? e.Icon : null;
                slotEIcon.gameObject.SetActive(e != null && e.Icon != null);
            }
        }
        else
        {
            if (slotENameText != null) slotENameText.text = "<color=#FF7777>Bloqueado</color>";
            if (slotEIcon != null) slotEIcon.gameObject.SetActive(false);
        }

        // Slot R (Requer Desbloqueio via Curupira)
        if (slotRButton != null) slotRButton.interactable = isRUnlocked;
        if (isRUnlocked)
        {
            Ability r = combat.GetEquippedAbility(2);
            if (slotRNameText != null) slotRNameText.text = r != null ? r.AbilityName : "<color=#888888>Vazio</color>";
            if (slotRIcon != null)
            {
                slotRIcon.sprite = r != null ? r.Icon : null;
                slotRIcon.gameObject.SetActive(r != null && r.Icon != null);
            }
        }
        else
        {
            if (slotRNameText != null) slotRNameText.text = "<color=#FF7777>Bloqueado</color>";
            if (slotRIcon != null) slotRIcon.gameObject.SetActive(false);
        }
    }

    public void SelectSlot(int slotIndex)
    {
        PlayerCombatController combat = Object.FindAnyObjectByType<PlayerCombatController>();
        if (combat != null && !combat.IsSlotUnlocked(slotIndex))
        {
            Debug.LogWarning($"[SkillEquipModalUI] Slot {slotIndex} está bloqueado! Desbloqueie com o Curupira.");
            return;
        }

        selectedSlotIndex = Mathf.Clamp(slotIndex, 0, 2);

        if (slotQHighlight != null) slotQHighlight.SetActive(selectedSlotIndex == 0);
        if (slotEHighlight != null) slotEHighlight.SetActive(selectedSlotIndex == 1);
        if (slotRHighlight != null) slotRHighlight.SetActive(selectedSlotIndex == 2);

        string slotLetter = selectedSlotIndex == 0 ? "Q" : (selectedSlotIndex == 1 ? "E" : "R");
        int cost = pendingBoon != null ? pendingBoon.StarCost : 1;
        string starUnit = cost == 1 ? "Fragmento" : "Fragmentos";

        if (confirmButtonText != null)
        {
            confirmButtonText.text = $"<b>Equipar no Slot [{slotLetter}]</b>\n(<color=#4FC3F7>{cost} {starUnit}</color>)";
        }
    }

    private void OnConfirmEquip()
    {
        if (pendingBoon == null) return;

        PlayerCurrency currency = PlayerCurrency.Instance ?? Object.FindAnyObjectByType<PlayerCurrency>();
        if (currency == null)
        {
            Debug.LogWarning("[SkillEquipModalUI] PlayerCurrency não encontrado!");
            return;
        }

        if (currency.StarFragments < pendingBoon.StarCost)
        {
            Debug.LogWarning($"[SkillEquipModalUI] Fragmentos insuficientes! Custo: {pendingBoon.StarCost}, Atual: {currency.StarFragments}");
            return;
        }

        // 1. Debita as estrelas
        if (!currency.SpendStarFragments(pendingBoon.StarCost))
        {
            return;
        }

        // 2. Equipa a habilidade no slot selecionado e registra no combat
        PlayerCombatController combat = Object.FindAnyObjectByType<PlayerCombatController>();
        if (combat != null)
        {
            if (pendingBoon.GrantedAbility != null)
            {
                combat.EquipAbility(selectedSlotIndex, pendingBoon.GrantedAbility);
            }
            else
            {
                // Se for um buff passivo geral, aplica normalmente
                pendingBoon.ApplyBoon(combat.gameObject);
            }

            // Registra a bênção como temporária da fase atual
            combat.RecordStageBoonAcquisition(pendingBoon, selectedSlotIndex);
        }

        // Força atualização visual imediata na HUD de Combate
        CombatUIHUD combatHUD = Object.FindAnyObjectByType<CombatUIHUD>(FindObjectsInactive.Include);
        if (combatHUD != null && combat != null)
        {
            combatHUD.UpdateEquippedAbilities(
                combat.GetEquippedAbility(0),
                combat.GetEquippedAbility(1),
                combat.GetEquippedAbility(2)
            );
        }

        Debug.Log($"[SkillEquipModalUI] '{pendingBoon.BoonName}' comprada e equipada no Slot {selectedSlotIndex}!");

        AbilityBoonSO justBought = pendingBoon;

        // 3. Fecha o modal de seleção de slot
        CloseModal();
 
        // 4. Notifica a loja para fazer reroll do slot comprado, mantendo a loja aberta
        if (parentSwapUI != null)
        {
            parentSwapUI.OnBoonPurchased(justBought);
        }
    }

    public void CloseModal()
    {
        if (modalPanel != null)
        {
            modalPanel.SetActive(false);
        }
        pendingBoon = null;
    }
}
