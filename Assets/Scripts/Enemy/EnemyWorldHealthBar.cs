using System.Collections;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Barra de Vida no Mundo (World-Space) flutuante sobre os inimigos normais.
/// Totalmente independente de Canvas (utiliza SpriteRenderers de alta performance),
/// com barra principal, rastro de dano fantasma (ghost bar) e visibilidade inteligente.
/// </summary>
public class EnemyWorldHealthBar : MonoBehaviour
{
    [Header("Configuração de Dimensões")]
    [Tooltip("Largura total da barra em unidades do mundo.")]
    [SerializeField] private float barWidth = 0.9f;

    [Tooltip("Altura da barra em unidades do mundo.")]
    [SerializeField] private float barHeight = 0.12f;

    [Tooltip("Espessura da borda/fundo.")]
    [SerializeField] private float borderPadding = 0.04f;

    [Tooltip("Deslocamento vertical acima do inimigo.")]
    [SerializeField] private float yOffset = 0.85f;

    [Header("Comportamento e Visibilidade")]
    [Tooltip("Se verdadeiro, a barra fica invisível com 100% de vida e só aparece quando o inimigo toma dano.")]
    [SerializeField] private bool hideWhenFull = true;

    [Tooltip("Tempo em segundos para a barra ocultar se não tomar dano por um tempo (0 = nunca oculta após ferido).")]
    [SerializeField] private float autoHideTime = 0f;

    [Tooltip("Suaviza a cor de acordo com a porcentagem de vida (Verde -> Amarelo -> Vermelho).")]
    [SerializeField] private bool useDynamicHealthColor = true;

    [Header("Cores")]
    [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.85f, 0.35f, 1f); // Verde Esmeralda
    [SerializeField] private Color midHealthColor = new Color(1f, 0.75f, 0.1f, 1f);    // Amarelo Dourado
    [SerializeField] private Color lowHealthColor = new Color(0.95f, 0.2f, 0.2f, 1f);   // Vermelho Carmesim
    [SerializeField] private Color ghostColor = new Color(1f, 0.9f, 0.4f, 0.8f);       // Rastro fantasma
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.85f); // Fundo escuro

    [Header("Componentes Gerados")]
    [SerializeField] private EnemyStats targetStats;

    private Transform barContainer;
    private SpriteRenderer bgRenderer;
    private SpriteRenderer ghostRenderer;
    private SpriteRenderer fillRenderer;

    private float currentFillRatio = 1f;
    private float ghostFillRatio = 1f;
    private Coroutine ghostCoroutine;
    private Coroutine hideCoroutine;
    private bool isDead = false;
    private static Sprite _pixelSprite;
    private static Sprite _leftPivotSprite;

    public void Configure(float offsetY, bool hideOnFull)
    {
        yOffset = offsetY;
        hideWhenFull = hideOnFull;
        if (targetStats != null)
        {
            UpdateHealthBar(targetStats.CurrentHealth, targetStats.MaxHealth, instant: true);
        }
    }

    private void Awake()
    {
        if (targetStats == null)
        {
            targetStats = GetComponentInParent<EnemyStats>();
            if (targetStats == null) targetStats = GetComponent<EnemyStats>();
        }

        BuildVisualHierarchy();
    }

    private void Start()
    {
        if (targetStats != null)
        {
            // Ajusta o yOffset se o colisor do inimigo for grande
            if (targetStats.TryGetComponent(out Collider2D col))
            {
                float boundsTop = col.bounds.extents.y;
                if (boundsTop > 0.4f)
                {
                    yOffset = Mathf.Max(yOffset, boundsTop + 0.35f);
                }
            }

            targetStats.OnHealthChanged += HandleHealthChanged;
            targetStats.OnEnemyDied += HandleEnemyDied;

            // Inicializa valores
            UpdateHealthBar(targetStats.CurrentHealth, targetStats.MaxHealth, instant: true);
        }
    }

    public void SetVisible(bool visible)
    {
        if (barContainer != null)
        {
            barContainer.gameObject.SetActive(visible);
        }
    }

    private void OnEnable()
    {
        SetVisible(true);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (targetStats != null)
        {
            targetStats.OnHealthChanged -= HandleHealthChanged;
            targetStats.OnEnemyDied -= HandleEnemyDied;
        }
    }

    private void LateUpdate()
    {
        if (barContainer == null || isDead) return;

        // Mantém a barra posicionada acima do inimigo, nivelada no mundo (sem rotacionar ou inverter se o pai fizer flip)
        Vector3 targetPos = transform.position + Vector3.up * yOffset;
        barContainer.position = targetPos;
        barContainer.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Constrói os GameObjects e SpriteRenderers necessários para renderizar a barra.
    /// </summary>
    private void BuildVisualHierarchy()
    {
        if (barContainer != null) return;

        EnsureSprites();

        // 1. Container Raiz
        GameObject containerObj = new GameObject("HealthBar_WorldRoot");
        containerObj.transform.SetParent(transform, false);
        containerObj.transform.localPosition = new Vector3(0f, yOffset, 0f);
        barContainer = containerObj.transform;

        // 2. Fundo (Background)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(barContainer, false);
        bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = _pixelSprite;
        bgRenderer.color = backgroundColor;
        bgRenderer.sortingLayerName = "Default";
        bgRenderer.sortingOrder = 24;
        bgObj.transform.localScale = new Vector3(barWidth + borderPadding * 2f, barHeight + borderPadding * 2f, 1f);

        // 3. Barra Fantasma (Ghost Fill)
        GameObject ghostObj = new GameObject("Ghost_Fill");
        ghostObj.transform.SetParent(barContainer, false);
        ghostObj.transform.localPosition = new Vector3(-barWidth * 0.5f, 0f, 0f);
        ghostRenderer = ghostObj.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = _leftPivotSprite;
        ghostRenderer.color = ghostColor;
        ghostRenderer.sortingLayerName = "Default";
        ghostRenderer.sortingOrder = 25;
        ghostObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);

        // 4. Barra de Vida Principal (Main Fill)
        GameObject fillObj = new GameObject("Health_Fill");
        fillObj.transform.SetParent(barContainer, false);
        fillObj.transform.localPosition = new Vector3(-barWidth * 0.5f, 0f, 0f);
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = _leftPivotSprite;
        fillRenderer.color = fullHealthColor;
        fillRenderer.sortingLayerName = "Default";
        fillRenderer.sortingOrder = 26;
        fillObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);

        // Define visibilidade inicial
        SetAlpha(hideWhenFull ? 0f : 1f);
    }

    private void HandleHealthChanged(float currentHp, float maxHp)
    {
        UpdateHealthBar(currentHp, maxHp, instant: false);
    }

    private void HandleEnemyDied()
    {
        isDead = true;
        if (ghostCoroutine != null) StopCoroutine(ghostCoroutine);
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);

        // Animação suave de fade out ao morrer
        if (barContainer != null)
        {
            FadeAlpha(0f, 0.25f);
        }
    }

    public void UpdateHealthBar(float currentHp, float maxHp, bool instant = false)
    {
        if (maxHp <= 0f || fillRenderer == null) return;

        float targetRatio = Mathf.Clamp01(currentHp / maxHp);
        currentFillRatio = targetRatio;

        // Se estiver com vida cheia e hideWhenFull estiver ativo
        if (targetRatio >= 0.999f && hideWhenFull)
        {
            SetAlpha(0f);
        }
        else
        {
            // Mostra a barra ao receber dano
            SetAlpha(1f);

            if (autoHideTime > 0f)
            {
                if (hideCoroutine != null) StopCoroutine(hideCoroutine);
                hideCoroutine = StartCoroutine(AutoHideRoutine());
            }
        }

        // 1. Atualiza escala do Fill Principal
        Vector3 fillScale = fillRenderer.transform.localScale;
        fillScale.x = barWidth * targetRatio;
        fillRenderer.transform.localScale = fillScale;

        // 2. Atualiza cor dinâmica
        if (useDynamicHealthColor)
        {
            if (targetRatio > 0.5f)
            {
                float t = (targetRatio - 0.5f) * 2f;
                fillRenderer.color = Color.Lerp(midHealthColor, fullHealthColor, t);
            }
            else
            {
                float t = targetRatio * 2f;
                fillRenderer.color = Color.Lerp(lowHealthColor, midHealthColor, t);
            }
        }

        // 3. Atualiza barra fantasma (Ghost Bar)
        if (instant)
        {
            ghostFillRatio = targetRatio;
            if (ghostRenderer != null)
            {
                Vector3 ghostScale = ghostRenderer.transform.localScale;
                ghostScale.x = barWidth * targetRatio;
                ghostRenderer.transform.localScale = ghostScale;
            }
        }
        else
        {
            if (ghostCoroutine != null) StopCoroutine(ghostCoroutine);
            ghostCoroutine = StartCoroutine(AnimateGhostBar(targetRatio));
        }
    }

    private IEnumerator AnimateGhostBar(float targetRatio)
    {
        // Aguarda breve pausa de impacto
        yield return new WaitForSeconds(0.18f);

        float startRatio = ghostFillRatio;
        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t; // Ease In Quad

            ghostFillRatio = Mathf.Lerp(startRatio, targetRatio, t);

            if (ghostRenderer != null)
            {
                Vector3 ghostScale = ghostRenderer.transform.localScale;
                ghostScale.x = barWidth * ghostFillRatio;
                ghostRenderer.transform.localScale = ghostScale;
            }

            yield return null;
        }

        ghostFillRatio = targetRatio;
        if (ghostRenderer != null)
        {
            Vector3 ghostScale = ghostRenderer.transform.localScale;
            ghostScale.x = barWidth * targetRatio;
            ghostRenderer.transform.localScale = ghostScale;
        }
    }

    private IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSeconds(autoHideTime);
        if (!isDead && currentFillRatio > 0f)
        {
            FadeAlpha(0f, 0.4f);
        }
    }

    private void SetAlpha(float alpha)
    {
        if (bgRenderer != null)
        {
            Color c = bgRenderer.color;
            c.a = backgroundColor.a * alpha;
            bgRenderer.color = c;
        }
        if (ghostRenderer != null)
        {
            Color c = ghostRenderer.color;
            c.a = ghostColor.a * alpha;
            ghostRenderer.color = c;
        }
        if (fillRenderer != null)
        {
            Color c = fillRenderer.color;
            c.a = alpha;
            fillRenderer.color = c;
        }
    }

    private void FadeAlpha(float targetAlpha, float duration)
    {
        if (bgRenderer != null) Tween.Color(bgRenderer, new Color(bgRenderer.color.r, bgRenderer.color.g, bgRenderer.color.b, backgroundColor.a * targetAlpha), duration);
        if (ghostRenderer != null) Tween.Color(ghostRenderer, new Color(ghostRenderer.color.r, ghostRenderer.color.g, ghostRenderer.color.b, ghostColor.a * targetAlpha), duration);
        if (fillRenderer != null) Tween.Color(fillRenderer, new Color(fillRenderer.color.r, fillRenderer.color.g, fillRenderer.color.b, targetAlpha), duration);
    }

    private static void EnsureSprites()
    {
        if (_pixelSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        if (_leftPivotSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _leftPivotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        }
    }
}
