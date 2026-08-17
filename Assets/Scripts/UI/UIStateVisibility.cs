using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente modular para gerenciar em quais estados do jogo (GameState) um elemento ou painel de UI deve ficar visível.
/// Registra-se automaticamente no UIManager ao ser habilitado/inicializado.
/// </summary>
public class UIStateVisibility : MonoBehaviour
{
    [Header("Configuração de Visibilidade por Estado")]
    [Tooltip("Lista de estados do jogo (GameState) em que este elemento deve ficar visível.")]
    [SerializeField] private List<GameState> visibleInStates = new List<GameState> { GameState.Playing };

    public List<GameState> VisibleInStates => visibleInStates;

    private void OnEnable()
    {
        RegisterWithUIManager();
    }

    private void Start()
    {
        RegisterWithUIManager();
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UnregisterPanel(gameObject);
        }
    }

    /// <summary>
    /// Registra o elemento no UIManager com os estados configurados.
    /// </summary>
    public void RegisterWithUIManager()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RegisterPanel(gameObject, visibleInStates);
        }
    }

    /// <summary>
    /// Permite alterar os estados de visibilidade programaticamente.
    /// </summary>
    public void SetVisibleStates(params GameState[] states)
    {
        visibleInStates = new List<GameState>(states);
        RegisterWithUIManager();
    }
}

