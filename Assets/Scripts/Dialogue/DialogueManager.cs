using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References (TMP & uGUI)")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject dialoguePanel;

    [Header("Settings")]
    [Tooltip("Velocidade do efeito typewriter (tempo em segundos entre cada caractere).")]
    [SerializeField] private float typingSpeed = 0.04f;

    [Header("Audio (Opcional - Esqueleto)")]
    [Tooltip("AudioSource para tocar o efeito sonora por caractere.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Som curto tocado a cada letra digitada.")]
    [SerializeField] private AudioClip typingSound;

    // Estado interno
    private DialogueNode currentNode;
    private bool isTyping;
    private bool isDialogueActive;
    private bool justStartedThisFrame;
    private Coroutine typingCoroutine;

    // Propriedade para verificar se o diálogo está ativo (útil para bloquear movimento do jogador)
    public bool IsDialogueActive => isDialogueActive;

    private void Awake()
    {
        // Padrão Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Garante que o painel de diálogo comece desativado
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Se o diálogo não estiver ativo, não processa entrada de avanço
        if (!isDialogueActive) return;

        // Ignora a entrada no mesmo frame em que o diálogo foi aberto
        if (justStartedThisFrame)
        {
            justStartedThisFrame = false;
            return;
        }

        // Entrada do Jogador: Avança o diálogo ao pressionar Espaço, Enter, E ou Botão A do controle
        bool confirmPressed = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) ||
                             (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (confirmPressed)
        {
            DisplayNextNode();
        }
    }

    /// <summary>
    /// Ativa a UI, reseta o estado e começa a digitar o nó inicial da sequência.
    /// </summary>
    /// <param name="sequence">A sequência de diálogo a ser exibida.</param>
    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.StartingNode == null)
        {
            Debug.LogWarning("[DialogueManager] Sequência de diálogo ou nó inicial inválido!");
            return;
        }

        isDialogueActive = true;
        justStartedThisFrame = true;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        // Para qualquer digitação anterior
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        typingCoroutine = StartCoroutine(TypeText(sequence.StartingNode));
    }

    /// <summary>
    /// Corotina que processa o efeito typewriter letra por letra.
    /// </summary>
    private IEnumerator TypeText(DialogueNode node)
    {
        currentNode = node;
        isTyping = true;

        // Atribui o nome do palestrante
        if (nameText != null)
        {
            nameText.text = node.SpeakerName;
            // Exibe a caixa de nome apenas se houver um nome preenchido
            nameText.gameObject.SetActive(!string.IsNullOrEmpty(node.SpeakerName));
        }

        // Lógica de Retratos Opcionais:
        // Verifica se a fala tem um retrato associado.
        // Se tiver, atribui o sprite e exibe o componente Image.
        // Se for nulo, esconde o GameObject do retrato. Isso permite que componentes de layout
        // (como Horizontal Layout Group ou Anchors da UI) expandam o texto para ocupar o espaço.
        if (portraitImage != null)
        {
            if (node.SpeakerPortrait != null)
            {
                portraitImage.sprite = node.SpeakerPortrait;
                portraitImage.gameObject.SetActive(true);
            }
            else
            {
                portraitImage.sprite = null;
                portraitImage.gameObject.SetActive(false);
            }
        }

        // Reseta o texto e inicia a digitação caractere por caractere
        if (dialogueText != null)
        {
            dialogueText.text = "";

            foreach (char letter in node.DialogueText)
            {
                dialogueText.text += letter;

                // Esqueleto para tocar som por letra (ignora espaços em branco)
                if (audioSource != null && typingSound != null && !char.IsWhiteSpace(letter))
                {
                    audioSource.PlayOneShot(typingSound);
                }

                yield return new WaitForSeconds(typingSpeed);
            }
        }

        isTyping = false;
    }

    /// <summary>
    /// Chamado pela entrada do jogador.
    /// Se o texto ainda está digitando, completa o texto instantaneamente.
    /// Se o texto terminou de digitar, avança para o próximo nó (currentNode.nextNode) ou encerra o diálogo.
    /// </summary>
    public void DisplayNextNode()
    {
        if (currentNode == null) return;

        if (isTyping)
        {
            // Interrompe o efeito typewriter e mostra o texto completo
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }

            if (dialogueText != null)
            {
                dialogueText.text = currentNode.DialogueText;
            }

            isTyping = false;
        }
        else
        {
            // Avança para a próxima fala se existir
            if (currentNode.NextNode != null)
            {
                typingCoroutine = StartCoroutine(TypeText(currentNode.NextNode));
            }
            else
            {
                EndDialogue();
            }
        }
    }

    /// <summary>
    /// Desativa a UI e libera o controle/estado do diálogo.
    /// </summary>
    private void EndDialogue()
    {
        isDialogueActive = false;
        currentNode = null;
        isTyping = false;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }
}
