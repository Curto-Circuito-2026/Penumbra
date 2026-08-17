using UnityEngine;
using TMPro;

/// <summary>
/// Porta / Portal de Transição de Fase (Roguelike Stage Door).
/// Fica bloqueada até que todos os inimigos da fase atual sejam derrotados.
/// Quando o jogador entra na porta aberta, ele avança para o próximo nível.
/// </summary>

[RequireComponent(typeof(Collider2D))]
public class StageDoor : MonoBehaviour
{
    [Header("Visual da Porta")]
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private TextMeshPro statusLabel;
    [SerializeField] private Color lockedColor = new Color(0.8f, 0.2f, 0.2f, 1f); // Vermelho
    [SerializeField] private Color unlockedColor = new Color(0.2f, 0.9f, 0.3f, 1f); // Verde Bright

    [Header("Estado")]
    [SerializeField] private bool isUnlocked = false;

    private Collider2D doorCollider;

    public bool IsUnlocked => isUnlocked;

    private void Awake()
    {
        doorCollider = GetComponent<Collider2D>();
        doorCollider.isTrigger = true;

        if (doorRenderer == null) doorRenderer = GetComponent<SpriteRenderer>();

        if (statusLabel == null)
        {
            statusLabel = GetComponentInChildren<TextMeshPro>();
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Desbloqueia ou bloqueia a porta da fase.
    /// </summary>
    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
        UpdateVisuals();

        if (isUnlocked)
        {
            Debug.Log("<color=#00FF88>[StageDoor] A porta da fase foi DESBLOQUEADA! O caminho para o próximo nível está aberto.</color>");
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 1.2f, "SAÍDA LIBERADA!", new Color(0.2f, 1f, 0.4f), 5f);
                CombatVisualEffects.Instance.PlayImpactBurst(transform.position, new Color(0.2f, 1f, 0.4f), 2f);
            }
        }
    }

    private void UpdateVisuals()
    {
        if (doorRenderer != null)
        {
            doorRenderer.color = isUnlocked ? unlockedColor : lockedColor;
        }

        if (statusLabel != null)
        {
            statusLabel.text = isUnlocked ? "<color=#00FF88>PORTA ABERTA\n[PRÓXIMA FASE]</color>" : "<color=#FF3333>BLOQUEADA\n[DERROTE OS INIMIGOS]</color>";
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Se o Player tocar na porta quando estiver desbloqueada:
        if (isUnlocked && (other.CompareTag("Player") || other.GetComponent<PlayerStats>() != null))
        {
            Debug.Log("[StageDoor] Player entrou na porta! Avançando para o próximo nível...");

            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.8f, "AVANÇANDO...", new Color(1f, 0.85f, 0.2f), 5f);
            }

            // Avança para a próxima fase no StageManager
            if (StageManager.Instance != null)
            {
                StageManager.Instance.NextStage();
            }
        }
        else if (!isUnlocked && (other.CompareTag("Player") || other.GetComponent<PlayerStats>() != null))
        {
            Debug.Log("[StageDoor] Porta bloqueada! Derrote todos os inimigos da fase para abrir.");
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.8f, "DERROTE OS INIMIGOS!", new Color(1f, 0.3f, 0.2f), 4f);
            }
        }
    }
}
