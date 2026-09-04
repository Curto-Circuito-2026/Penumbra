using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla a Barra de Vida do Chefe (Boss Health Bar) no centro superior da tela (Top-Center HUD).
/// Possui barra de vida principal (laranja/vermelho fogo), barra de dano fantasma suave (ghost bar)
/// e animações de introdução e vitória com PrimeTween.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("Componentes de Interface")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Image ghostFillImage;
    [SerializeField] private TextMeshProUGUI healthNumbersText;

    [Header("Cores e Efeitos")]
    [SerializeField] private Color healthColor = new Color(1f, 0.35f, 0.1f, 1f); // Fogo ardente
    [SerializeField] private Color ghostColor = new Color(1f, 0.85f, 0.2f, 0.7f); // Dourado fantasma

    private float currentMaxHp = 100f;
    private float currentHp = 100f;
    private Coroutine ghostCoroutine;
    private bool isVisible = false;
    private Sequence activeHideSequence;
    private Tween activeHideTween;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Começa invisível até o boss ser ativado
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void OnEnable()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }
        PlayerStats.OnAnyPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }
        PlayerStats.OnAnyPlayerDied -= HandlePlayerDied;
        activeHideSequence.Stop();
        activeHideTween.Stop();
    }

    private void HandleGameStateChanged(GameState previousState, GameState newState)
    {
        if (newState == GameState.Dead || newState == GameState.Menu)
        {
            HideImmediate();
        }
    }

    private void HandlePlayerDied()
    {
        HideImmediate();
    }

    /// <summary>
    /// Oculta instantaneamente a barra de vida do Boss sem delay ou animação.
    /// </summary>
    public void HideImmediate()
    {
        isVisible = false;
        activeHideSequence.Stop();
        activeHideTween.Stop();
        if (ghostCoroutine != null)
        {
            StopCoroutine(ghostCoroutine);
            ghostCoroutine = null;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Exibe e inicializa a barra de vida do Boss com animação de descida e fade in.
    /// </summary>
    public void ShowBoss(string bossName, float initialHp, float maxHp)
    {
        activeHideSequence.Stop();
        activeHideTween.Stop();

        currentHp = initialHp;
        currentMaxHp = maxHp;
        isVisible = true;

        if (ghostCoroutine != null)
        {
            StopCoroutine(ghostCoroutine);
            ghostCoroutine = null;
        }

        if (bossNameText != null) bossNameText.text = bossName;
        UpdateFillDirect(currentHp / currentMaxHp);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            activeHideTween = Tween.Alpha(canvasGroup, 1f, 0.6f, Ease.OutQuad);

            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                Vector2 originalPos = rect.anchoredPosition;
                rect.anchoredPosition = new Vector2(originalPos.x, originalPos.y + 40f);
                Tween.UIAnchoredPosition(rect, originalPos, 0.6f, Ease.OutBack);
            }
        }
    }

    /// <summary>
    /// Atualiza a barra de vida com perda suave de vida e rastro fantasma.
    /// </summary>
    public void UpdateHealth(float newHp, float maxHp)
    {
        currentMaxHp = maxHp;
        currentHp = Mathf.Clamp(newHp, 0f, maxHp);
        float targetFill = currentHp / currentMaxHp;

        if (!gameObject.activeInHierarchy)
        {
            activeHideSequence.Stop();
            activeHideTween.Stop();
            gameObject.SetActive(true);
            isVisible = true;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        if (healthNumbersText != null)
        {
            healthNumbersText.text = $"{currentHp:F0} / {currentMaxHp:F0}";
        }

        // 1. Atualiza a barra principal instantaneamente / suavemente
        if (healthFillImage != null)
        {
            Tween.UIFillAmount(healthFillImage, targetFill, 0.2f, Ease.OutQuad);
        }

        // 2. Anima a barra fantasma com leve atraso se ativo
        if (ghostFillImage != null && isActiveAndEnabled)
        {
            if (ghostCoroutine != null) StopCoroutine(ghostCoroutine);
            ghostCoroutine = StartCoroutine(AnimateGhostBar(targetFill));
        }
    }

    private IEnumerator AnimateGhostBar(float targetFill)
    {
        yield return new WaitForSeconds(0.4f);

        if (ghostFillImage != null)
        {
            Tween.UIFillAmount(ghostFillImage, targetFill, 0.6f, Ease.OutQuad);
        }
    }

    private void UpdateFillDirect(float fill)
    {
        if (healthFillImage != null) healthFillImage.fillAmount = fill;
        if (ghostFillImage != null) ghostFillImage.fillAmount = fill;
        if (healthNumbersText != null) healthNumbersText.text = $"{currentHp:F0} / {currentMaxHp:F0}";
    }

    /// <summary>
    /// Oculta a barra de vida do Boss com animação suave de fade out.
    /// </summary>
    public void HideBoss(bool victory = false)
    {
        if (!isVisible) return;
        isVisible = false;

        activeHideSequence.Stop();
        activeHideTween.Stop();
        if (ghostCoroutine != null)
        {
            StopCoroutine(ghostCoroutine);
            ghostCoroutine = null;
        }

        if (canvasGroup != null)
        {
            float delay = victory ? 1.5f : 0.2f;
            activeHideSequence = Sequence.Create()
                .ChainDelay(delay)
                .Chain(Tween.Alpha(canvasGroup, 0f, 0.8f, Ease.InQuad))
                .ChainCallback(() =>
                {
                    gameObject.SetActive(false);
                });
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
