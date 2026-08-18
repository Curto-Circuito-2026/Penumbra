using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente para NPCs Mestres de Bênçãos, Magias ou Acordos Folclóricos (ex: Iara, Caipora e Saci).
/// Ao interagir ou encerrar diálogo, abre a interface de seleção de 3 habilidades com Re-roll.
/// </summary>
public class AbilitySwapNPC : MonoBehaviour
{
    [Header("Identidade do Mestre de Bênçãos")]
    [Tooltip("Título exibido no topo da tela quando a troca for aberta.")]
    [SerializeField] private string swapTitle = "TRUQUES DO SACI";

    [Tooltip("Subtítulo/descrição inspiradora do mestre.")]
    [SerializeField] private string swapSubtitle = "O vento traz segredos, rapidez e travessuras mortais.";

    [Tooltip("Ilustração do personagem exibida na lateral esquerda.")]
    [SerializeField] private Sprite characterIllustration;

    [Header("Pool de Bênçãos deste Mestre")]
    [Tooltip("Lista de AbilityBoonSO disponíveis para sorteio com este NPC.")]
    [SerializeField] private List<AbilityBoonSO> availableBoons = new List<AbilityBoonSO>();

    [Header("Eventos")]
    [Tooltip("GameEvent opcional para acionar a abertura.")]
    [SerializeField] private GameEvent openSwapEvent;

    public string SwapTitle => swapTitle;
    public string SwapSubtitle => swapSubtitle;
    public Sprite CharacterIllustration => characterIllustration;
    public List<AbilityBoonSO> AvailableBoons => availableBoons;

    private void OnEnable()
    {
        if (openSwapEvent != null)
        {
            openSwapEvent.OnEventRaised += OpenThisSwap;
        }
    }

    private void OnDisable()
    {
        if (openSwapEvent != null)
        {
            openSwapEvent.OnEventRaised -= OpenThisSwap;
        }
    }

    /// <summary>
    /// Abre a tela de troca de bênçãos com a arte, título e pool exclusivos deste NPC.
    /// </summary>
    public void OpenThisSwap()
    {
        Debug.Log($"[AbilitySwapNPC] '{gameObject.name}' abrindo tela de bênçãos '{swapTitle}' com {availableBoons.Count} opções no pool.");

        AbilitySwapUI targetUI = AbilitySwapUI.Instance;
        if (targetUI == null)
        {
            targetUI = Object.FindAnyObjectByType<AbilitySwapUI>(FindObjectsInactive.Include);
        }

        if (targetUI != null)
        {
            targetUI.OpenSwap(swapTitle, swapSubtitle, availableBoons, characterIllustration);
        }
        else
        {
            Debug.LogError($"[AbilitySwapNPC] AbilitySwapUI não encontrado na cena ao tentar abrir '{swapTitle}'!");
        }
    }
}
