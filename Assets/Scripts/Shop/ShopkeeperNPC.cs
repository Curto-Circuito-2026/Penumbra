using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente modular para qualquer NPC Vendedor no jogo.
/// Permite definir o título da loja, subtítulo, ilustração de fundo e lista exclusiva de itens à venda no Inspector.
/// </summary>
public class ShopkeeperNPC : MonoBehaviour
{
    [Header("Identidade da Loja")]
    [Tooltip("Título exibido no topo da janela quando este NPC abrir a loja.")]
    [SerializeField] private string shopTitle = "✦ SANTUÁRIO DA SEREIA IARA ✦";

    [Tooltip("Subtítulo/descrição do comerciante.")]
    [SerializeField] private string shopSubtitle = "Troque suas estrelas e fragmentos por encantos e relíquias das águas";

    [Tooltip("Ilustração/Arte de fundo do personagem exibida na base da tela atrás da janela da loja.")]
    [SerializeField] private Sprite shopkeeperIllustration;

    [Header("Itens à Venda deste NPC")]
    [Tooltip("Lista de ScriptableObjects (ShopItemSO) que este comerciante específico vende.")]
    [SerializeField] private List<ShopItemSO> itemsForSale = new List<ShopItemSO>();

    [Header("Eventos e Diálogo")]
    [Tooltip("GameEvent opcional que dispara a abertura da loja deste NPC.")]
    [SerializeField] private GameEvent openShopEvent;

    public string ShopTitle => shopTitle;
    public string ShopSubtitle => shopSubtitle;
    public Sprite ShopkeeperIllustration => shopkeeperIllustration;
    public List<ShopItemSO> ItemsForSale => itemsForSale;

    private void OnEnable()
    {
        if (openShopEvent != null)
        {
            openShopEvent.OnEventRaised += OpenThisShop;
        }
    }

    private void OnDisable()
    {
        if (openShopEvent != null)
        {
            openShopEvent.OnEventRaised -= OpenThisShop;
        }
    }

    /// <summary>
    /// Abre a interface da loja com os dados, ilustração e catálogo exclusivos deste NPC.
    /// Pode ser chamado via UnityEvent, GameEvent (onEnd de diálogo) ou diretamente por código.
    /// </summary>
    public void OpenThisShop()
    {
        Debug.Log($"[ShopkeeperNPC] '{gameObject.name}' abrindo loja '{shopTitle}' com {itemsForSale.Count} itens configurados.");
        
        ShopUI targetShop = ShopUI.Instance;
        if (targetShop == null)
        {
            targetShop = Object.FindAnyObjectByType<ShopUI>(FindObjectsInactive.Include);
        }

        if (targetShop != null)
        {
            targetShop.OpenShop(shopTitle, shopSubtitle, itemsForSale, shopkeeperIllustration);
        }
        else
        {
            Debug.LogError($"[ShopkeeperNPC] ShopUI não foi encontrado na cena ao tentar abrir '{shopTitle}'!");
        }
    }
}
