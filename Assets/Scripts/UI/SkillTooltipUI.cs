using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Componente responsável por gerenciar a exibição da caixa de dica (Tooltip)
/// quando o cursor do mouse passa por cima de qualquer slot de habilidade ou ataque básico na HUD.
/// Exibe: Nome, Descrição, Cooldown (Recarga) e Custo de Mana.
/// </summary>
public class SkillTooltipUI : MonoBehaviour
{
    private static SkillTooltipUI instance;

    public static SkillTooltipUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<SkillTooltipUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
    }

    [Header("UI References")]
    [Tooltip("Painel container do visual do Tooltip (será ativado/desativado).")]
    [SerializeField] private GameObject tooltipPanel;

    [Tooltip("Texto do Nome da Habilidade.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Texto da Descrição da Habilidade.")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Tooltip("Texto do Tempo de Recarga (Cooldown).")]
    [SerializeField] private TextMeshProUGUI cooldownText;

    [Tooltip("Texto do Custo de Mana.")]
    [SerializeField] private TextMeshProUGUI manaCostText;

    [Tooltip("Ícone opcional da Habilidade na janela do Tooltip.")]
    [SerializeField] private Image skillIcon;

    [Header("Configurações de Posicionamento")]
    [Tooltip("Deslocamento (Offset) do Tooltip em relação à posição do cursor do mouse.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(15f, -15f);

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private TMP_FontAsset basicFont;

    private void EnsureBasicFont()
    {
        if (basicFont == null)
        {
            basicFont = Resources.Load<TMP_FontAsset>("Fonts/Basic");
        }

        if (basicFont != null)
        {
            if (titleText != null)
            {
                titleText.font = basicFont;
                titleText.fontSize = 24f;
                titleText.fontStyle = FontStyles.Bold;
            }
            if (descriptionText != null)
            {
                descriptionText.font = basicFont;
                descriptionText.fontSize = 18f;
            }
            if (cooldownText != null)
            {
                cooldownText.font = basicFont;
                cooldownText.fontSize = 17f;
            }
            if (manaCostText != null)
            {
                manaCostText.font = basicFont;
                manaCostText.fontSize = 17f;
            }

            TMP_Text[] childTexts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var txt in childTexts)
            {
                if (txt != null) txt.font = basicFont;
            }
        }

        // Desativa qualquer Image filha (ícone/moldura/quadrado) para deixar apenas os textos visíveis
        if (tooltipPanel != null)
        {
            Image[] images = tooltipPanel.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != tooltipPanel && img.gameObject != gameObject)
                {
                    img.gameObject.SetActive(false);
                }
                else
                {
                    // Fundo escuro semi-transparente elegante (sem azul)
                    img.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
                }
            }
        }

        if (skillIcon != null)
        {
            skillIcon.gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (tooltipPanel == null)
        {
            tooltipPanel = gameObject;
        }

        rectTransform = tooltipPanel.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = tooltipPanel.AddComponent<RectTransform>();
        }

        parentCanvas = GetComponentInParent<Canvas>();

        EnsureBasicFont();

        // Oculta a janela de conteúdo do tooltip no início
        HideTooltip();
    }

    private void Update()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            // Esconde se o jogo for pausado ou entrar em diálogo
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Playing)
            {
                HideTooltip();
                return;
            }

            FollowMouseCursor();
        }
    }

    /// <summary>
    /// Exibe a janela de Tooltip com os dados da habilidade indicada.
    /// </summary>
    public void ShowTooltip(string title, string description, float cooldown, float manaCost, Sprite icon = null)
    {
        EnsureBasicFont();

        if (titleText != null)
        {
            titleText.text = title;
            titleText.fontSize = 24f;
            titleText.fontStyle = FontStyles.Bold;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
            descriptionText.fontSize = 18f;
        }

        if (cooldownText != null)
        {
            cooldownText.text = cooldown > 0f ? $"<color=#FFCC00>Recarga:</color> {cooldown:F1}s" : "<color=#FFCC00>Recarga:</color> Instantânea";
            cooldownText.fontSize = 17f;
        }

        if (manaCostText != null)
        {
            manaCostText.gameObject.SetActive(false);
        }

        if (skillIcon != null)
        {
            skillIcon.gameObject.SetActive(false);
        }

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(true);
            FollowMouseCursor();
        }
    }

    /// <summary>
    /// Oculta a janela de conteúdo do Tooltip.
    /// </summary>
    public void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Atualiza a posição do Tooltip para acompanhar o cursor do mouse, mantendo-o dentro dos limites da tela.
    /// </summary>
    private void FollowMouseCursor()
    {
        if (Mouse.current == null || rectTransform == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 targetPos = mousePos + cursorOffset;

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
        {
            // Ajusta o pivô/posição para que o tooltip não saia da tela
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            float tooltipWidth = rectTransform.rect.width * parentCanvas.scaleFactor;
            float tooltipHeight = rectTransform.rect.height * parentCanvas.scaleFactor;

            // Se for passar da borda direita da tela, inverte o offset para a esquerda
            if (targetPos.x + tooltipWidth > screenWidth)
            {
                targetPos.x = mousePos.x - tooltipWidth - 10f;
            }

            // Se for passar da borda inferior da tela, ajusta para cima
            if (targetPos.y - tooltipHeight < 0f)
            {
                targetPos.y = mousePos.y + tooltipHeight + 10f;
            }

            rectTransform.position = targetPos;
        }
        else
        {
            rectTransform.position = targetPos;
        }
    }
}

