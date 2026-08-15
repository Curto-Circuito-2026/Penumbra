using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueTrigger : MonoBehaviour
{
    [Header("NPC Profile (Opcional)")]
    [Tooltip("Nome do NPC exibido na caixa de diálogo.")]
    [SerializeField] private string npcName;

    [Tooltip("Retrato do NPC exibido no diálogo. Se for nulo, oculta o retrato na UI.")]
    [SerializeField] private Sprite npcPortrait;

    [Header("Dialogue Configuration")]
    [Tooltip("Sequência de diálogo a ser disparada.")]
    [SerializeField] private DialogueSequence dialogueToTrigger;

    [Header("Trigger Options")]
    [Tooltip("Se verdadeiro, permite interagir clicando com o mouse diretamente no NPC.")]
    [SerializeField] private bool allowMouseClick = true;

    [Tooltip("Se verdadeiro, o diálogo já foi falado e não será exibido novamente.")]
    [SerializeField] private bool alreadyTalk = false;

    [Tooltip("Se verdadeiro, dispara o diálogo automaticamente assim que o jogador entra no Collider 2D.")]
    [SerializeField] private bool triggerOnEnter2D = false;

    [Tooltip("Se verdadeiro, exige pressionar a tecla de interação quando o jogador estiver no Collider 2D.")]
    [SerializeField] private bool requireInteractButton = true;

    private bool isPlayerInZone = false;

    private void Update()
    {
        // Se estiver na área de colisão e requerer botão de confirmação/interação
        if (isPlayerInZone && requireInteractButton && !triggerOnEnter2D)
        {
            // Evita disparar novamente se o diálogo já estiver sendo exibido
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

            bool interactPressed = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) ||
                                   (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            if (interactPressed)
            {
                alreadyTalk = true;
                TriggerDialogue();
            }
        }
    }

    /// <summary>
    /// Detecta o clique do mouse diretamente no Collider do NPC.
    /// </summary>
    private void OnMouseDown()
    {
        if (!allowMouseClick) return;

        // Evita disparar se o diálogo já estiver sendo exibido
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

        alreadyTalk = true;
        TriggerDialogue();
    }

    /// <summary>
    /// Inicia o diálogo chamando o DialogueManager, passando o nome e o retrato do NPC.
    /// </summary>
    public void TriggerDialogue()
    {
        if (dialogueToTrigger == null)
        {
            Debug.LogWarning($"[DialogueTrigger] Nenhum DialogueSequence atribuído no objeto: {gameObject.name}");
            return;
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialogueToTrigger, npcName, npcPortrait);
        }
        else
        {
            Debug.LogError("[DialogueTrigger] Instância do DialogueManager não foi encontrada na cena!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = true;

            if (triggerOnEnter2D && !alreadyTalk)
            {
                alreadyTalk = true;
                TriggerDialogue();
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = false;
        }
    }
}

