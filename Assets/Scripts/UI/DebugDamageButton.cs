using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Script de Debug para causar dano ao jogador via botão na UI ou atalho de teclado.
/// </summary>
public class DebugDamageButton : MonoBehaviour
{
    [Header("Configurações do Dano")]
    [SerializeField] private float damageAmount = 50f;
    [SerializeField] private PlayerStats playerStats;

    [Header("Referência de UI")]
    [SerializeField] private Button debugButton;

    private void Awake()
    {
        if (debugButton == null)
        {
            debugButton = GetComponent<Button>();
        }

        if (debugButton != null)
        {
            debugButton.onClick.RemoveAllListeners();
            debugButton.onClick.AddListener(TakeDebugDamage);
        }
    }

    private void Start()
    {
        FindPlayerStats();
    }

    private void Update()
    {
        // Atalho opcional de teclado (Tecla H ou K) para testar rapidamente
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current.kKey.wasPressedThisFrame)
        {
            TakeDebugDamage();
        }
    }

    private void FindPlayerStats()
    {
        if (playerStats == null)
        {
            playerStats = Object.FindAnyObjectByType<PlayerStats>();
        }
    }

    public void TakeDebugDamage()
    {
        FindPlayerStats();

        if (playerStats != null)
        {
            Debug.Log($"[DebugDamageButton] Botão clicado! Aplicando {damageAmount} de dano ao jogador.");
            playerStats.TakeDamage(damageAmount, Vector3.zero);
        }
        else
        {
            Debug.LogWarning("[DebugDamageButton] PlayerStats não encontrado na cena!");
        }
    }

    public void SetDamageAmount(float amount)
    {
        damageAmount = amount;
    }

    public void SetButtonReference(Button btn)
    {
        debugButton = btn;
        if (debugButton != null)
        {
            debugButton.onClick.RemoveAllListeners();
            debugButton.onClick.AddListener(TakeDebugDamage);
        }
    }
}
