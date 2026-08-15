using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueTrigger : MonoBehaviour
{
    [Header("Dialogue Configuration")]
    [Tooltip("Sequência de diálogo a ser disparada.")]
    [SerializeField] private DialogueSequence dialogueToTrigger;

    [Header("Trigger Options")]
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
    /// Inicia o diálogo chamando o DialogueManager.
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
            DialogueManager.Instance.StartDialogue(dialogueToTrigger);
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

