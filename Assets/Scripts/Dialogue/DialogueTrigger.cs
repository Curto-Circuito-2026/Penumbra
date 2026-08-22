using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Gatilho de interação para NPCs e objetos que iniciam sequências de diálogo.
/// Exibe feedback visual de proximidade (borda/outline de destaque e tecla [E] flutuante).
/// </summary>
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

    [Header("Feedback Visual de Proximidade")]
    [Tooltip("GameObject do ícone/indicador flutuante [E] sobre a cabeça do NPC.")]
    [SerializeField] private GameObject interactionPrompt;

    [Tooltip("SpriteRenderer de destaque/borda branca do NPC.")]
    [SerializeField] private SpriteRenderer highlightOutline;

    [Tooltip("Se verdadeiro, adiciona animação suave de flutuação e pulso no indicador [E].")]
    [SerializeField] private bool animatePrompt = true;

    private bool isPlayerInZone = false;
    private Vector3 initialPromptPos;
    private Vector3 initialPromptScale = Vector3.one;
    private SpriteRenderer mainSpriteRenderer;

    private void Awake()
    {
        mainSpriteRenderer = GetComponent<SpriteRenderer>();

        // Busca ou cria o indicador [E] se não estiver atribuído
        if (interactionPrompt == null)
        {
            Transform promptT = transform.Find("Interaction_Prompt");
            if (promptT != null)
            {
                interactionPrompt = promptT.gameObject;
            }
        }

        if (interactionPrompt != null)
        {
            initialPromptPos = interactionPrompt.transform.localPosition;
            initialPromptScale = interactionPrompt.transform.localScale;
            interactionPrompt.SetActive(false);
        }

        // Busca ou cria o Outline de destaque se não estiver atribuído
        if (highlightOutline == null)
        {
            Transform hlT = transform.Find("Highlight_Outline");
            if (hlT != null)
            {
                highlightOutline = hlT.GetComponent<SpriteRenderer>();
            }
            else if (mainSpriteRenderer != null)
            {
                GameObject hlObj = new GameObject("Highlight_Outline", typeof(SpriteRenderer));
                hlObj.transform.SetParent(transform, false);
                hlObj.transform.localPosition = Vector3.zero;
                hlObj.transform.localScale = new Vector3(1.15f, 1.15f, 1f);

                highlightOutline = hlObj.GetComponent<SpriteRenderer>();
                highlightOutline.sprite = mainSpriteRenderer.sprite;
                highlightOutline.color = Color.white;
                highlightOutline.sortingLayerID = mainSpriteRenderer.sortingLayerID;
                highlightOutline.sortingOrder = Mathf.Max(1, mainSpriteRenderer.sortingOrder - 1);
            }
        }

        if (highlightOutline != null)
        {
            Shader silhouetteShader = Shader.Find("Custom/SpriteSilhouette") ?? Shader.Find("GUI/Text Shader");
            if (silhouetteShader != null)
            {
                highlightOutline.material = new Material(silhouetteShader);
            }
            highlightOutline.gameObject.SetActive(false);
        }
    }

    private static float lastDialogueCloseTime = 0f;

    public static void NotifyDialogueOrShopClosed()
    {
        lastDialogueCloseTime = Time.time;
    }

    private void Update()
    {
        UpdateVisualFeedback();

        // Evita reabrir diálogo acidentalmente no mesmo frame ou logo após fechar uma janela
        if (Time.time - lastDialogueCloseTime < 0.3f) return;
        if (GameStateManager.Instance != null && Time.frameCount == GameStateManager.Instance.StateChangeFrame) return;

        // Se estiver na área de colisão e requerer botão de confirmação/interação
        if (isPlayerInZone && requireInteractButton && !triggerOnEnter2D)
        {
            // Permite interação APENAS se o jogo estiver no estado Playing (evita disparar com a loja ou menus abertos)
            if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove) return;

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
    /// Atualiza a visibilidade e animações do indicador [E] e da borda de destaque branca.
    /// </summary>
    private void UpdateVisualFeedback()
    {
        bool isDialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;
        bool canInteract = isPlayerInZone && (GameStateManager.Instance == null || GameStateManager.Instance.CanPlayerMove) && !isDialogueActive;

        // 1. Indicador [E] Flutuante
        if (interactionPrompt != null)
        {
            if (interactionPrompt.activeSelf != canInteract)
            {
                interactionPrompt.SetActive(canInteract);
            }

            if (canInteract && animatePrompt)
            {
                float bobOffset = Mathf.Sin(Time.time * 4.5f) * 0.08f;
                interactionPrompt.transform.localPosition = initialPromptPos + new Vector3(0f, bobOffset, 0f);

                float pulseScale = 1f + Mathf.Sin(Time.time * 3.5f) * 0.05f;
                interactionPrompt.transform.localScale = initialPromptScale * pulseScale;
            }
        }

        // 2. Destaque / Borda Branca (Outline)
        if (highlightOutline != null)
        {
            if (highlightOutline.gameObject.activeSelf != canInteract)
            {
                highlightOutline.gameObject.SetActive(canInteract);
            }

            if (canInteract)
            {
                if (mainSpriteRenderer != null)
                {
                    highlightOutline.sprite = mainSpriteRenderer.sprite;
                    highlightOutline.flipX = mainSpriteRenderer.flipX;
                    highlightOutline.flipY = mainSpriteRenderer.flipY;
                    highlightOutline.sortingLayerID = mainSpriteRenderer.sortingLayerID;
                    highlightOutline.sortingOrder = Mathf.Max(1, mainSpriteRenderer.sortingOrder - 1);
                }

                // Brilho pulsante suave na borda branca
                float alpha = 0.75f + Mathf.Sin(Time.time * 5f) * 0.25f;
                highlightOutline.color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }

    /// <summary>
    /// Detecta o clique do mouse diretamente no Collider do NPC.
    /// Só permite interação se o jogador estiver próximo ao NPC (isPlayerInZone).
    /// </summary>
    private void OnMouseDown()
    {
        if (!allowMouseClick) return;

        // Permite interação APENAS se o jogador estiver na zona de proximidade do NPC
        if (!isPlayerInZone) return;

        // Evita reabrir diálogo acidentalmente no mesmo frame ou logo após fechar uma janela
        if (Time.time - lastDialogueCloseTime < 0.3f) return;
        if (GameStateManager.Instance != null && Time.frameCount == GameStateManager.Instance.StateChangeFrame) return;

        // Permite interação APENAS se o jogo estiver no estado Playing
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove) return;

        // Evita disparar se o diálogo já estiver sendo exibido
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

        // Não dispara se o clique foi em um elemento de interface (UI)
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        alreadyTalk = true;
        TriggerDialogue();
    }

    [Header("Eventos ao Finalizar Diálogo")]
    [Tooltip("Ação disparada ao concluir a sequência de diálogo.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onDialogueFinished;

    public void SetDialogueSequence(DialogueSequence seq)
    {
        dialogueToTrigger = seq;
    }

    public void SetOnDialogueFinished(UnityEngine.Events.UnityAction action)
    {
        if (onDialogueFinished == null) onDialogueFinished = new UnityEngine.Events.UnityEvent();
        onDialogueFinished.AddListener(action);
    }

    /// <summary>
    /// Inicia o diálogo chamando o DialogueManager, passando o nome e o retrato do NPC.
    /// </summary>
    public void TriggerDialogue(System.Action customCallback = null)
    {
        if (dialogueToTrigger == null)
        {
            // Se for um NPC de Troca de Habilidades ou Comerciante sem diálogo, abre diretamente!
            var swapDirect = GetComponent<AbilitySwapNPC>();
            if (swapDirect != null)
            {
                swapDirect.OpenThisSwap();
                return;
            }

            var skDirect = GetComponent<ShopkeeperNPC>();
            if (skDirect != null)
            {
                skDirect.OpenThisShop();
                return;
            }

            Debug.LogWarning($"[DialogueTrigger] Nenhum DialogueSequence atribuído no objeto: {gameObject.name}");
            return;
        }

        if (DialogueManager.Instance != null)
        {
            var swapNPC = GetComponent<AbilitySwapNPC>();
            var shopNPC = GetComponent<ShopkeeperNPC>();

            System.Action onComplete = customCallback;
            if (onComplete == null)
            {
                if (swapNPC != null) onComplete = swapNPC.OpenThisSwap;
                else if (shopNPC != null) onComplete = shopNPC.OpenThisShop;
            }

            DialogueManager.Instance.StartDialogue(dialogueToTrigger, npcName, npcPortrait, () =>
            {
                onComplete?.Invoke();
                onDialogueFinished?.Invoke();
            });
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
