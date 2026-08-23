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
    private System.Action currentOnCompleteCallback;

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

        // Entrada do Jogador: Pular diálogo inteiro com ESC ou F
        bool skipAllPressed = (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)) ||
                              (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (skipAllPressed)
        {
            SkipEntireDialogue();
            return;
        }

        // Entrada do Jogador: Avança o diálogo ao pressionar Espaço, Enter, E, Clique do Mouse ou Botão A do controle
        bool confirmPressed = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) ||
                              (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                              (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (confirmPressed)
        {
            DisplayNextNode();
        }
    }

    /// <summary>
    /// Pula a sequência inteira de diálogo e finaliza imediatamente.
    /// </summary>
    public void SkipEntireDialogue()
    {
        if (!isDialogueActive) return;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        DialogueNode nodeEnding = currentNode;
        var callback = currentOnCompleteCallback;
        currentOnCompleteCallback = null;

        EndDialogue();

        if (nodeEnding != null && nodeEnding.onEnd != null)
        {
            nodeEnding.onEnd.Raise();
        }

        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopVoice();
        }

        callback?.Invoke();
    }

    /// <summary>
    /// Ativa a UI, reseta o estado e começa a digitar o nó inicial da sequência.
    /// Permite passar opcionalmente o nome e retrato do NPC para a UI e um callback de término.
    /// </summary>
    /// <param name="sequence">A sequência de diálogo a ser exibida.</param>
    /// <param name="overrideName">Nome do NPC (opcional).</param>
    /// <param name="overridePortrait">Retrato do NPC (opcional).</param>
    /// <param name="onComplete">Ação disparada ao encerrar este diálogo (opcional).</param>
    public void StartDialogue(DialogueSequence sequence, string overrideName = null, Sprite overridePortrait = null, System.Action onComplete = null)
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
        currentOnCompleteCallback = onComplete;

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
        currentNode = node;
        isTyping = true;

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }

        // Toca o áudio de dublagem se atribuído no nó
        if (node != null && node.VoiceClip != null && AudioController.Instance != null)
        {
            AudioController.Instance.PlayVoice(node.VoiceClip);
        }

        // Configura o nome do personagem: dá preferência à sobrescrita se definida, senão usa o nome do nó
        if (nameText != null)
        {
            string finalSpeakerName = !string.IsNullOrEmpty(currentOverrideName) ? currentOverrideName : node.SpeakerName;
            nameText.text = finalSpeakerName;
            nameText.gameObject.SetActive(!string.IsNullOrEmpty(finalSpeakerName));
        }

        // Configura o retrato: dá preferência à sobrescrita se definida, senão usa o do nó
        if (portraitImage != null)
        {
            Sprite finalPortrait = currentOverridePortrait != null ? currentOverridePortrait : node.SpeakerPortrait;
            if (finalPortrait != null)
            {
                portraitImage.sprite = finalPortrait;
                portraitImage.gameObject.SetActive(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }

        // Dispara o evento de início do nó se configurado
        if (node.onStart != null)
        {
            node.onStart.Raise();
        }

        // Efeito Typewriter
        string fullText = node.DialogueText;
        foreach (char letter in fullText)
        {
            if (dialogueText != null)
            {
                dialogueText.text += letter;
            }

            // Toca o efeito sonoro de digitação se atribuído
            if (audioSource != null && typingSound != null && !char.IsWhiteSpace(letter))
            {
                audioSource.PlayOneShot(typingSound);
            }

            yield return new WaitForSeconds(typingSpeed);
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
            DialogueNode nodeEnding = currentNode;

            // Para a voz anterior antes de trocar de nó
            if (AudioController.Instance != null)
            {
                AudioController.Instance.StopVoice();
            }

            // Avança para a próxima fala se existir
            if (currentNode.NextNode != null)
            {
                if (nodeEnding.onEnd != null) { Debug.Log("raising onEnd"); nodeEnding.onEnd.Raise(); }
                typingCoroutine = StartCoroutine(TypeText(currentNode.NextNode));
            }
            else
            {
                var callback = currentOnCompleteCallback;
                currentOnCompleteCallback = null;
                EndDialogue();
                if (nodeEnding != null && nodeEnding.onEnd != null)
                {
                    Debug.Log("raising onEnd on dialogue completion");
                    nodeEnding.onEnd.Raise();
                }
                callback?.Invoke();
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

        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopVoice();
        }

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
