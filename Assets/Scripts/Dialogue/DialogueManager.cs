using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    private static DialogueManager instance;

    public static DialogueManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<DialogueManager>();
            }
            return instance;
        }
    }

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

    // Sobrescritas opcionais vindas do NPC / DialogueTrigger
    private string currentOverrideName;
    private Sprite currentOverridePortrait;

    // Propriedade para verificar se o diálogo está ativo (útil para bloquear movimento do jogador)
    public bool IsDialogueActive => isDialogueActive;

    private void Awake()
    {
        // Padrão Singleton
        if (instance == null)
        {
            instance = this;
            
        }
        else if (instance != this)
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
    /// Permite passar opcionalmente o nome e retrato do NPC para a UI.
    /// </summary>
    /// <param name="sequence">A sequência de diálogo a ser exibida.</param>
    /// <param name="overrideName">Nome do NPC (opcional).</param>
    /// <param name="overridePortrait">Retrato do NPC (opcional).</param>
    public void StartDialogue(DialogueSequence sequence, string overrideName = null, Sprite overridePortrait = null)
    {
        if (sequence == null || sequence.StartingNode == null)
        {
            Debug.LogWarning("[DialogueManager] Sequência de diálogo ou nó inicial inválido!");
            return;
        }

        isDialogueActive = true;
        justStartedThisFrame = true;
        currentOverrideName = overrideName;
        currentOverridePortrait = overridePortrait;

        // Atualiza o estado do jogo para Dialogue
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Dialogue);
        }

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
        if (node.onStart != null) { Debug.Log("raising onStart"); node.onStart.Raise(); }

        currentNode = node;
        isTyping = true;

        // Determina o nome a exibir: prioriza o nome definido no NPC (currentOverrideName); se vazio, usa o do nó
        string displayName = !string.IsNullOrEmpty(currentOverrideName) ? currentOverrideName : node.SpeakerName;

        // Atribui o nome do palestrante
        if (nameText != null)
        {
            nameText.text = displayName;
            // Exibe a caixa de nome apenas se houver um nome preenchido
            nameText.gameObject.SetActive(!string.IsNullOrEmpty(displayName));
        }

        // Lógica de Retratos Opcionais:
        // Determina o retrato a exibir: prioriza o do NPC; se nulo, usa o do nó.
        Sprite displayPortrait = currentOverridePortrait != null ? currentOverridePortrait : node.SpeakerPortrait;

        if (portraitImage != null)
        {
            if (displayPortrait != null)
            {
                portraitImage.sprite = displayPortrait;
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
            if (currentNode.onEnd != null){ Debug.Log("raising onEnd");  currentNode.onEnd.Raise();}

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

        // Restaura o estado do jogo para Playing
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Dialogue)
        {
            GameStateManager.Instance.SetState(GameState.Playing);
        }
    }
}
