using UnityEngine;

/// <summary>
/// Componente simples e leve para animar sequências de sprites (flipbook 2D) em NPCs, objetos de cenário e efeitos.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SimpleSpriteAnimator : MonoBehaviour
{
    [Header("Configurações de Animação")]
    [Tooltip("Lista de frames da animação em ordem.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Velocidade da animação em frames por segundo (FPS).")]
    [SerializeField] private float frameRate = 6f;

    [Tooltip("Se verdadeiro, repete a animação continuamente.")]
    [SerializeField] private bool loop = true;

    [Tooltip("Se verdadeiro, inicia a animação assim que o objeto for ativado.")]
    [SerializeField] private bool playOnAwake = true;

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float timer = 0f;
    private bool isPlaying = false;

    public Sprite[] Frames
    {
        get => frames;
        set
        {
            frames = value;
            currentFrame = 0;
            timer = 0f;
            if (frames != null && frames.Length > 0 && spriteRenderer != null)
            {
                spriteRenderer.sprite = frames[0];
            }
        }
    }

    public float FrameRate
    {
        get => frameRate;
        set => frameRate = Mathf.Max(0.1f, value);
    }

    public bool IsPlaying => isPlaying;
    public Sprite CurrentSprite => (frames != null && currentFrame >= 0 && currentFrame < frames.Length) ? frames[currentFrame] : null;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (playOnAwake && frames != null && frames.Length > 0)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!isPlaying || frames == null || frames.Length == 0) return;

        timer += Time.deltaTime;
        float frameDuration = 1f / frameRate;

        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = frames.Length - 1;
                    isPlaying = false;
                }
            }

            if (spriteRenderer != null && currentFrame < frames.Length)
            {
                spriteRenderer.sprite = frames[currentFrame];
            }
        }
    }

    /// <summary>
    /// Inicia ou retoma a reprodução da animação.
    /// </summary>
    public void Play()
    {
        isPlaying = true;
    }

    /// <summary>
    /// Pausa a reprodução da animação mantendo o frame atual.
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>
    /// Para a animação e reseta para o primeiro frame.
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        currentFrame = 0;
        timer = 0f;
        if (spriteRenderer != null && frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }
    }
}
