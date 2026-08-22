using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gerencia a Vida, Mana e Estado de Morte do Jogador.
/// Implementa IDamageable para receber dano de inimigos.
/// </summary>
public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Atributos de Vida")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Atributos de Mana")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float currentMana = 100f;
    [SerializeField] private float manaRegenRate = 8f;

    [Header("Feedback de Dano")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float flashDuration = 0.15f;

    private Color originalColor = Color.white;
    private Coroutine flashCoroutine;
    private CharacterController2D characterController;

    // Sistema de Escudo e Buff de Defesa
    private float currentShield = 0f;
    private float damageReductionPercent = 0f;
    private Coroutine defenseBuffCoroutine;
    private Coroutine shieldCoroutine;
    private GameObject activeShieldVisual;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;
    public float CurrentShield => currentShield;
    public bool HasShield => currentShield > 0f;
    public bool IsDead { get; private set; }

    // Eventos para atualização da UI
    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action<float, float> OnManaChanged;   // (current, max)
    public event Action<float, float> OnStaminaChanged; // (current, max)
    public event Action<float> OnShieldChanged;        // (current)
    public event Action OnPlayerDied;
    public event Action OnPlayerRespawned;

    // Eventos Estáticos Globais para sistemas sem referência direta
    public static event Action OnAnyPlayerDied;
    public static event Action OnAnyPlayerRespawned;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        characterController = GetComponent<CharacterController2D>();
        currentHealth = maxHealth;
        currentMana = maxMana;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
    }

    private void Update()
    {
        if (IsDead) return;

        // Regeneração contínua de Mana
        if (currentMana < maxMana)
        {
            currentMana += manaRegenRate * Time.deltaTime;
            if (currentMana > maxMana) currentMana = maxMana;
            OnManaChanged?.Invoke(currentMana, maxMana);
        }

        // Transmite atualização de Stamina para a UI
        if (characterController != null)
        {
            OnStaminaChanged?.Invoke(characterController.CurrentStamina, characterController.MaxStamina);
        }
    }

    /// <summary>
    /// Aplica buff de defesa (redução percentual de dano recebido) por uma duração em segundos.
    /// </summary>
    public void ApplyDefenseBuff(float reductionPercentage, float duration)
    {
        if (defenseBuffCoroutine != null) StopCoroutine(defenseBuffCoroutine);
        defenseBuffCoroutine = StartCoroutine(DefenseBuffRoutine(reductionPercentage, duration));
    }

    private IEnumerator DefenseBuffRoutine(float reductionPercentage, float duration)
    {
        damageReductionPercent = Mathf.Clamp(reductionPercentage, 0f, 0.9f);
        Debug.Log($"[PlayerStats] Pele de Carvalho ativada! Redução de dano: {damageReductionPercent * 100:F0}% por {duration:F1}s.");

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 0.5f, new Color(0.3f, 0.8f, 0.2f), 2.0f);
        }

        yield return new WaitForSeconds(duration);

        damageReductionPercent = 0f;
        defenseBuffCoroutine = null;
        Debug.Log("[PlayerStats] Pele de Carvalho expirou.");
    }

    /// <summary>
    /// Concede um escudo de absorção de dano por uma duração em segundos.
    /// </summary>
    public void ApplyShield(float shieldAmount, float duration, GameObject shieldVisualPrefab = null)
    {
        if (shieldCoroutine != null) StopCoroutine(shieldCoroutine);
        shieldCoroutine = StartCoroutine(ShieldRoutine(shieldAmount, duration, shieldVisualPrefab));
    }

    private IEnumerator ShieldRoutine(float shieldAmount, float duration, GameObject shieldVisualPrefab)
    {
        currentShield = shieldAmount;
        OnShieldChanged?.Invoke(currentShield);
        Debug.Log($"[PlayerStats] Escudo Bolha ativado com {shieldAmount} de absorção por {duration:F1}s.");

        if (activeShieldVisual != null) Destroy(activeShieldVisual);
        if (shieldVisualPrefab != null)
        {
            activeShieldVisual = Instantiate(shieldVisualPrefab, transform);
            activeShieldVisual.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        }
        else
        {
            activeShieldVisual = CreateBubbleShieldVisual();
        }

        float timer = duration;
        float baseScale = 1.55f;

        while (timer > 0f && currentShield > 0f)
        {
            timer -= Time.deltaTime;

            // Animação orgânica de ondulação/respiração da bolha de água
            if (activeShieldVisual != null)
            {
                float wobbleX = Mathf.Sin(Time.time * 4.5f) * 0.08f;
                float wobbleY = Mathf.Cos(Time.time * 3.8f) * 0.06f;
                activeShieldVisual.transform.localScale = new Vector3(baseScale + wobbleX, baseScale + wobbleY, 1f);
            }

            yield return null;
        }

        if (currentShield <= 0f)
        {
            Debug.Log("[PlayerStats] Escudo Bolha foi estourado pelo dano!");
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayWaterBurst(transform.position + Vector3.up * 0.45f, 2.2f);
                CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 1.1f, "💥 Bolha Estourou!", new Color(0.35f, 0.85f, 1f), 3.8f);
            }
        }
        else
        {
            Debug.Log("[PlayerStats] Escudo Bolha expirou normalmente.");
            if (CombatVisualEffects.Instance != null)
            {
                CombatVisualEffects.Instance.PlayWaterBurst(transform.position + Vector3.up * 0.45f, 1.8f);
            }
        }

        currentShield = 0f;
        OnShieldChanged?.Invoke(currentShield);

        if (activeShieldVisual != null)
        {
            Destroy(activeShieldVisual);
            activeShieldVisual = null;
        }

        shieldCoroutine = null;
    }

    /// <summary>
    /// Cria proceduralmente uma esfera/bolha de água azul transparente ao redor do player.
    /// </summary>
    private GameObject CreateBubbleShieldVisual()
    {
        GameObject bubble = new GameObject("VFX_BubbleShield");
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        bubble.transform.localScale = new Vector3(1.55f, 1.55f, 1f);

        // 1. Película da Bolha (Azul Ciano Translúcido)
        SpriteRenderer sr = bubble.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrCreateBubbleSprite();
        sr.color = new Color(0.20f, 0.68f, 1.0f, 0.42f);
        sr.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 10) + 1;

        // 2. Reflexo de Luz Aquático Superior (Specular Glint)
        GameObject highlight = new GameObject("Bubble_Glint");
        highlight.transform.SetParent(bubble.transform, false);
        highlight.transform.localPosition = new Vector3(0.22f, 0.26f, 0f);
        highlight.transform.localScale = new Vector3(0.32f, 0.32f, 1f);
        SpriteRenderer srHigh = highlight.AddComponent<SpriteRenderer>();
        srHigh.sprite = GetOrCreateBubbleSprite();
        srHigh.color = new Color(0.92f, 0.98f, 1.0f, 0.70f);
        srHigh.sortingOrder = sr.sortingOrder + 1;

        return bubble;
    }

    private static Sprite bubbleCachedSprite;
    private static Sprite GetOrCreateBubbleSprite()
    {
        if (bubbleCachedSprite != null) return bubbleCachedSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float radius = (size - 1) / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float normalizedDist = dist / radius; // 0 no centro, 1 na borda
                    // Borda mais espessa e brilhante, centro sutilmente translúcido
                    float edgeIntensity = Mathf.Pow(normalizedDist, 1.6f);
                    float alpha = Mathf.Lerp(0.28f, 0.95f, edgeIntensity);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        bubbleCachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return bubbleCachedSprite;
    }

    /// <summary>
    /// Aplica dano ao jogador, processando redução de defesa, escudo e dano à vida.
    /// </summary>
    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        if (IsDead) return;

        // 1. Aplica redução percentual de defesa (Pele de Carvalho)
        if (damageReductionPercent > 0f)
        {
            float reduced = amount * (1f - damageReductionPercent);
            Debug.Log($"[PlayerStats] Dano reduzido de {amount:F1} para {reduced:F1} pela Pele de Carvalho.");
            amount = reduced;
        }

        // 2. Absorve com o escudo (Escudo Bolha)
        if (currentShield > 0f)
        {
            if (amount <= currentShield)
            {
                currentShield -= amount;
                amount = 0f;
                Debug.Log($"[PlayerStats] Dano totalmente absorvido pelo escudo! Escudo restante: {currentShield:F1}");
                if (CombatVisualEffects.Instance != null)
                {
                    CombatVisualEffects.Instance.PlayWaterBurst(transform.position + Vector3.up * 0.45f, 1.2f);
                }
            }
            else
            {
                amount -= currentShield;
                Debug.Log($"[PlayerStats] Escudo absorveu {currentShield:F1} de dano e quebrou! Dano restante: {amount:F1}");
                currentShield = 0f;
            }
            OnShieldChanged?.Invoke(currentShield);
        }

        if (amount <= 0f) return;

        currentHealth -= amount;
        if (currentHealth < 0f) currentHealth = 0f;

        Debug.Log($"[PlayerStats] Jogador recebeu {amount:F1} de dano! Vida restante: {currentHealth:F1}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashDamageColor());
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Cura o jogador na quantidade especificada sem ultrapassar a vida máxima.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Debug.Log($"[PlayerStats] Jogador curado em {amount:F1}! Vida atual: {currentHealth:F1}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position + Vector3.up * 0.5f, new Color(0.2f, 1f, 0.4f), 1.2f);
        }
    }

    /// <summary>
    /// Aumenta a vida máxima do jogador e restaura a vida na mesma proporção.
    /// </summary>
    public void IncreaseMaxHealth(float amount)
    {
        if (amount <= 0f) return;

        maxHealth += amount;
        currentHealth += amount;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Aumenta a mana máxima do jogador.
    /// </summary>
    public void IncreaseMaxMana(float amount)
    {
        if (amount <= 0f) return;

        maxMana += amount;
        currentMana += amount;
        OnManaChanged?.Invoke(currentMana, maxMana);
    }

    /// <summary>
    /// Aumenta a taxa de regeneração de mana por segundo.
    /// </summary>
    public void IncreaseManaRegen(float amount)
    {
        if (amount <= 0f) return;
        manaRegenRate += amount;
    }

    /// <summary>
    /// Verifica se o jogador possui Mana suficiente para conjurar uma habilidade.
    /// </summary>
    public bool HasEnoughMana(float amount)
    {
        return currentMana >= amount;
    }

    /// <summary>
    /// Consome Mana do jogador se houver quantidade suficiente.
    /// </summary>
    public bool UseMana(float amount)
    {
        if (amount <= 0f) return true;

        if (currentMana >= amount)
        {
            currentMana -= amount;
            OnManaChanged?.Invoke(currentMana, maxMana);
            return true;
        }

        return false;
    }

    private void Die()
    {
        if (IsDead) return;

        IsDead = true;
        Debug.Log("[PlayerStats] O jogador morreu!");

        // Notifica eventos de Morte
        OnPlayerDied?.Invoke();
        OnAnyPlayerDied?.Invoke();

        // Altera o estado do jogo para Dead no GameStateManager
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetDead();
        }
    }

    /// <summary>
    /// Reinicia a vida, mana e estado do jogador, alterando o estado do jogo de volta para Playing.
    /// </summary>
    public void RestartPlayer()
    {
        IsDead = false;
        currentHealth = maxHealth;
        currentMana = maxMana;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // Altera o estado do jogo para Playing no GameStateManager PRIMEIRO
        // para que a UI de Recursos fique ativa e receba as atualizações de Vida/Mana
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetPlaying();
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
        OnPlayerRespawned?.Invoke();
        OnAnyPlayerRespawned?.Invoke();

        Debug.Log("[PlayerStats] Jogador reiniciado com sucesso! Status alterado para JOGANDO.");
    }

    private IEnumerator FlashDamageColor()
    {
        spriteRenderer.color = damageFlashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}
