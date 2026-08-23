using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerenciador centralizado de áudio para músicas (BGM), efeitos sonoros (SFX) e dublagens (Voice).
/// Suporta canais de volume independentes (com PlayerPrefs), Dual BGM com crossfade suave,
/// pool de SFX e Audio Ducking dinâmico (atenuação de BGM durante a reprodução de falas/vozes).
/// </summary>
[DefaultExecutionOrder(-200)]
public class AudioController : MonoBehaviour
{
    private static AudioController instance;

    public static AudioController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<AudioController>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("AudioController");
                    instance = obj.AddComponent<AudioController>();
                }
            }
            return instance;
        }
    }

    #region Constants & PlayerPrefs Keys
    private const string KEY_MASTER_VOL = "Audio_MasterVolume";
    private const string KEY_MUSIC_VOL = "Audio_MusicVolume";
    private const string KEY_SFX_VOL = "Audio_SFXVolume";
    private const string KEY_VOICE_VOL = "Audio_VoiceVolume";
    #endregion

    #region Inspector Configuration

    [Header("Volume Configuration (0.0 a 1.0)")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float voiceVolume = 1f;

    [Header("Audio Ducking (Atenuação de BGM na Dublagem)")]
    [Tooltip("Multiplicador do volume da música enquanto uma voz/dublagem estiver tocando (ex: 0.25 = 25% do volume).")]
    [Range(0f, 1f)] [SerializeField] private float duckingMultiplier = 0.25f;
    [Tooltip("Tempo em segundos para a música diminuir e voltar ao volume normal.")]
    [SerializeField] private float duckingFadeDuration = 0.25f;

    [Header("Audio Sources (Opcional - Criados automaticamente se nulos)")]
    [SerializeField] private AudioSource bgmSourceA;
    [SerializeField] private AudioSource bgmSourceB;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private int sfxPoolSize = 10;

    #endregion

    #region Events
    /// <summary>
    /// Disparado sempre que qualquer canal de volume é alterado.
    /// Assinatura: Action(master, music, sfx, voice)
    /// </summary>
    public event Action<float, float, float, float> OnAudioVolumesChanged;
    #endregion

    #region Internal State
    private List<AudioSource> sfxPool = new List<AudioSource>();
    private int nextSfxIndex = 0;

    private AudioSource activeBgmSource;
    private AudioSource inactiveBgmSource;
    private Coroutine bgmCrossfadeCoroutine;
    private Coroutine bgmFadeOutCoroutine;

    private bool isDucking = false;
    private Coroutine duckingFadeCoroutine;
    private Coroutine voiceWatchCoroutine;

    private float currentBgmBaseTargetVolume = 1f;
    #endregion

    #region Properties
    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SFXVolume => sfxVolume;
    public float VoiceVolume => voiceVolume;

    public bool IsVoicePlaying => voiceSource != null && voiceSource.isPlaying;
    public bool IsMusicPlaying => activeBgmSource != null && activeBgmSource.isPlaying;
    #endregion

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadVolumeSettings();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    #region Initialization

    private void InitializeAudioSources()
    {
        // 1. Configuração de Dual BGM Sources
        if (bgmSourceA == null)
        {
            GameObject bgmObjA = new GameObject("BGM_Source_A");
            bgmObjA.transform.SetParent(transform, false);
            bgmSourceA = bgmObjA.AddComponent<AudioSource>();
        }
        ConfigureSource(bgmSourceA, loop: true);

        if (bgmSourceB == null)
        {
            GameObject bgmObjB = new GameObject("BGM_Source_B");
            bgmObjB.transform.SetParent(transform, false);
            bgmSourceB = bgmObjB.AddComponent<AudioSource>();
        }
        ConfigureSource(bgmSourceB, loop: true);

        activeBgmSource = bgmSourceA;
        inactiveBgmSource = bgmSourceB;

        // 2. Configuração do canal de Voz / Dublagem
        if (voiceSource == null)
        {
            GameObject voiceObj = new GameObject("Voice_Source");
            voiceObj.transform.SetParent(transform, false);
            voiceSource = voiceObj.AddComponent<AudioSource>();
        }
        ConfigureSource(voiceSource, loop: false);

        // 3. Configuração do Pool de SFX
        if (sfxPool == null) sfxPool = new List<AudioSource>();
        sfxPool.Clear();

        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject sfxObj = new GameObject($"SFX_Source_{i}");
            sfxObj.transform.SetParent(transform, false);
            AudioSource src = sfxObj.AddComponent<AudioSource>();
            ConfigureSource(src, loop: false);
            sfxPool.Add(src);
        }
    }

    private void ConfigureSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D por padrão
        source.volume = 0f;
    }

    #endregion

    #region Volume & Settings Management

    private void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOL, masterVolume);
        musicVolume = PlayerPrefs.GetFloat(KEY_MUSIC_VOL, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOL, sfxVolume);
        voiceVolume = PlayerPrefs.GetFloat(KEY_VOICE_VOL, voiceVolume);

        ApplyVolumes();
    }

    public void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(KEY_MASTER_VOL, masterVolume);
        PlayerPrefs.SetFloat(KEY_MUSIC_VOL, musicVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOL, sfxVolume);
        PlayerPrefs.SetFloat(KEY_VOICE_VOL, voiceVolume);
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
        SaveVolumeSettings();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
        SaveVolumeSettings();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
        SaveVolumeSettings();
    }

    public void SetVoiceVolume(float volume)
    {
        voiceVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
        SaveVolumeSettings();
    }

    public void SetAllVolumes(float master, float music, float sfx, float voice)
    {
        masterVolume = Mathf.Clamp01(master);
        musicVolume = Mathf.Clamp01(music);
        sfxVolume = Mathf.Clamp01(sfx);
        voiceVolume = Mathf.Clamp01(voice);
        ApplyVolumes();
        SaveVolumeSettings();
    }

    private void ApplyVolumes()
    {
        // Atualiza o volume efetivo da música ativa considerando o ducking
        if (activeBgmSource != null && activeBgmSource.isPlaying)
        {
            float targetMul = isDucking ? duckingMultiplier : 1f;
            activeBgmSource.volume = masterVolume * musicVolume * currentBgmBaseTargetVolume * targetMul;
        }

        // Atualiza o volume da voz
        if (voiceSource != null)
        {
            voiceSource.volume = masterVolume * voiceVolume;
        }

        OnAudioVolumesChanged?.Invoke(masterVolume, musicVolume, sfxVolume, voiceVolume);
    }

    #endregion

    #region Music (BGM) & Crossfade

    /// <summary>
    /// Toca uma trilha de música de fundo (BGM) com suporte opcional a crossfade suave.
    /// Se a mesma música já estiver tocando, mantém a reprodução.
    /// </summary>
    /// <param name="clip">AudioClip da música.</param>
    /// <param name="fadeDuration">Duração do crossfade em segundos (0 para troca instantânea).</param>
    /// <param name="loop">Se a música deve ficar em loop contínuo.</param>
    public void PlayBGM(AudioClip clip, float fadeDuration = 1f, bool loop = true)
    {
        if (clip == null) return;

        // Se o mesmo clipe já estiver tocando no source ativo, não reinicia
        if (activeBgmSource != null && activeBgmSource.clip == clip && activeBgmSource.isPlaying)
        {
            return;
        }

        if (bgmCrossfadeCoroutine != null)
        {
            StopCoroutine(bgmCrossfadeCoroutine);
        }
        if (bgmFadeOutCoroutine != null)
        {
            StopCoroutine(bgmFadeOutCoroutine);
        }

        bgmCrossfadeCoroutine = StartCoroutine(CrossfadeBGMCoroutine(clip, fadeDuration, loop));
    }

    private IEnumerator CrossfadeBGMCoroutine(AudioClip newClip, float duration, bool loop)
    {
        AudioSource incoming = inactiveBgmSource;
        AudioSource outgoing = activeBgmSource;

        incoming.clip = newClip;
        incoming.loop = loop;
        incoming.volume = 0f;
        incoming.Play();

        currentBgmBaseTargetVolume = 1f;
        float duckFactor = isDucking ? duckingMultiplier : 1f;
        float targetVolume = masterVolume * musicVolume * duckFactor;

        if (duration <= 0.01f)
        {
            incoming.volume = targetVolume;
            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
            }
        }
        else
        {
            float timer = 0f;
            float initialOutgoingVol = outgoing != null ? outgoing.volume : 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / duration);

                duckFactor = isDucking ? duckingMultiplier : 1f;
                targetVolume = masterVolume * musicVolume * duckFactor;

                incoming.volume = Mathf.Lerp(0f, targetVolume, progress);
                if (outgoing != null)
                {
                    outgoing.volume = Mathf.Lerp(initialOutgoingVol, 0f, progress);
                }

                yield return null;
            }

            incoming.volume = targetVolume;
            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
            }
        }

        // Alterna as referências de fontes ativas/inativas
        activeBgmSource = incoming;
        inactiveBgmSource = outgoing;
        bgmCrossfadeCoroutine = null;
    }

    /// <summary>
    /// Para a música atual com transição suave de fade out.
    /// </summary>
    public void StopBGM(float fadeDuration = 1f)
    {
        if (activeBgmSource == null || !activeBgmSource.isPlaying) return;

        if (bgmCrossfadeCoroutine != null)
        {
            StopCoroutine(bgmCrossfadeCoroutine);
            bgmCrossfadeCoroutine = null;
        }
        if (bgmFadeOutCoroutine != null)
        {
            StopCoroutine(bgmFadeOutCoroutine);
        }

        bgmFadeOutCoroutine = StartCoroutine(FadeOutBGMCoroutine(fadeDuration));
    }

    private IEnumerator FadeOutBGMCoroutine(float duration)
    {
        if (activeBgmSource == null) yield break;

        float timer = 0f;
        float startVol = activeBgmSource.volume;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            activeBgmSource.volume = Mathf.Lerp(startVol, 0f, timer / duration);
            yield return null;
        }

        activeBgmSource.Stop();
        activeBgmSource.volume = 0f;
        bgmFadeOutCoroutine = null;
    }

    /// <summary>
    /// Pausa a música de fundo instantaneamente.
    /// </summary>
    public void PauseBGM()
    {
        if (activeBgmSource != null && activeBgmSource.isPlaying)
        {
            activeBgmSource.Pause();
        }
    }

    /// <summary>
    /// Retoma a música de fundo pausada.
    /// </summary>
    public void ResumeBGM()
    {
        if (activeBgmSource != null && !activeBgmSource.isPlaying && activeBgmSource.clip != null)
        {
            activeBgmSource.UnPause();
        }
    }

    #endregion

    #region Voice & Dubbing (with Audio Ducking)

    /// <summary>
    /// Toca uma linha de voz/dublagem no canal dedicado e ativa o Audio Ducking na música de fundo.
    /// Se uma voz já estiver tocando, a substitui imediatamente.
    /// </summary>
    /// <param name="clip">AudioClip da fala/dublagem.</param>
    /// <param name="volumeScale">Escala opcional de volume (0 a 1).</param>
    public void PlayVoice(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || voiceSource == null) return;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.volume = masterVolume * voiceVolume * Mathf.Clamp01(volumeScale);
        voiceSource.Play();

        // Ativa o ducking na música
        ApplyDucking(true);

        // Monitora o término da reprodução da voz para restaurar a BGM
        if (voiceWatchCoroutine != null)
        {
            StopCoroutine(voiceWatchCoroutine);
        }
        voiceWatchCoroutine = StartCoroutine(WatchVoiceEndCoroutine(clip.length));
    }

    /// <summary>
    /// Interrompe imediatamente qualquer fala/dublagem em execução e restaura a música de fundo.
    /// Usado ao pular falas, avançar nós de diálogo ou encerrar cutscenes.
    /// </summary>
    public void StopVoice()
    {
        if (voiceSource == null) return;

        if (voiceWatchCoroutine != null)
        {
            StopCoroutine(voiceWatchCoroutine);
            voiceWatchCoroutine = null;
        }

        if (voiceSource.isPlaying)
        {
            voiceSource.Stop();
        }
        voiceSource.clip = null;

        // Restaura a música do ducking
        ApplyDucking(false);
    }

    private IEnumerator WatchVoiceEndCoroutine(float clipDuration)
    {
        // Aguarda a duração do clipe ou até a fonte parar de tocar
        float timer = 0f;
        while (timer < clipDuration && voiceSource != null && voiceSource.isPlaying)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fala finalizada: restaura o volume da BGM
        ApplyDucking(false);
        voiceWatchCoroutine = null;
    }

    private void ApplyDucking(bool enable)
    {
        if (isDucking == enable) return;
        isDucking = enable;

        if (duckingFadeCoroutine != null)
        {
            StopCoroutine(duckingFadeCoroutine);
        }
        duckingFadeCoroutine = StartCoroutine(DuckingFadeCoroutine(enable));
    }

    private IEnumerator DuckingFadeCoroutine(bool enableDucking)
    {
        if (activeBgmSource == null || !activeBgmSource.isPlaying) yield break;

        float startVol = activeBgmSource.volume;
        float targetFactor = enableDucking ? duckingMultiplier : 1f;
        float targetVol = masterVolume * musicVolume * currentBgmBaseTargetVolume * targetFactor;

        float timer = 0f;
        float duration = Mathf.Max(0.01f, duckingFadeDuration);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            activeBgmSource.volume = Mathf.Lerp(startVol, targetVol, progress);
            yield return null;
        }

        activeBgmSource.volume = targetVol;
        duckingFadeCoroutine = null;
    }

    #endregion

    #region Sound Effects (SFX)

    /// <summary>
    /// Toca um efeito sonoro 2D utilizando o pool dinâmico de AudioSources.
    /// </summary>
    /// <param name="clip">AudioClip a ser tocado.</param>
    /// <param name="volumeScale">Escala de volume (0 a 1).</param>
    /// <param name="pitchVariation">Variação aleatória sutil de pitch (ex: 0.05 para +/- 5%).</param>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f, float pitchVariation = 0f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        if (source == null) return;

        source.transform.position = transform.position;
        source.spatialBlend = 0f; // 2D
        source.clip = clip;
        source.volume = masterVolume * sfxVolume * Mathf.Clamp01(volumeScale);

        if (pitchVariation > 0f)
        {
            source.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        }
        else
        {
            source.pitch = 1f;
        }

        source.PlayOneShot(clip, source.volume);
    }

    /// <summary>
    /// Toca um efeito sonoro espacializado 3D em uma posição específica do mundo.
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        if (source == null) return;

        source.transform.position = position;
        source.spatialBlend = 0.5f; // 3D parcial para Top-Down 2D
        source.volume = masterVolume * sfxVolume * Mathf.Clamp01(volumeScale);
        source.pitch = 1f;

        source.PlayOneShot(clip, source.volume);
    }

    private AudioSource GetAvailableSFXSource()
    {
        if (sfxPool == null || sfxPool.Count == 0)
        {
            InitializeAudioSources();
        }

        // Tenta encontrar uma fonte que não esteja tocando
        for (int i = 0; i < sfxPool.Count; i++)
        {
            int index = (nextSfxIndex + i) % sfxPool.Count;
            if (sfxPool[index] != null && !sfxPool[index].isPlaying)
            {
                nextSfxIndex = (index + 1) % sfxPool.Count;
                return sfxPool[index];
            }
        }

        // Se todas estiverem ocupadas, reutiliza a mais antiga (round-robin)
        AudioSource fallback = sfxPool[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % sfxPool.Count;
        return fallback;
    }

    #endregion
}