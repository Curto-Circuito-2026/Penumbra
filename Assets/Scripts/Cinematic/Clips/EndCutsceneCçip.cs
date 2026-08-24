using PrimeTween;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EndCutsceneClip : ICinematicClip
{
    int curScene;

    [Header("Scenes Global")]
    [SerializeField] List<GameObject> scenes;
    [SerializeField] List<GameObject> dacingObjects;

    [Header("Dialogue")]
    [SerializeField] GameObject dialogBox;
    [SerializeField] TMP_Text dialogText;
    [SerializeField] float typeSpeed = 0.06f;

    [Header("Audio & Voice (Dublagem)")]
    [SerializeField] private AudioClip BGMusic;
    [Tooltip("Lista de clipes de dublagem correspondentes a cada cena (opcional).")]
    [SerializeField] private List<AudioClip> sceneVoices;

    [Header("Scene 1 Objects")]
    [Header("Scene 2 Objects")]
    [Header("Scene 3 Objects")]
    [SerializeField] RectTransform Naia;
    [SerializeField] RectTransform Enemy_Left;
    [SerializeField] RectTransform Enemy_BottomRight;
    [SerializeField] RectTransform Enemy_TopRight;

    [Header("Scene 4 Objects")]

    [Header("Scene 5 Objects")]

    [Header("Prompt de Pular / Avançar")]
    [SerializeField] private TMP_Text skipPromptText;

    private List<string> sceneText = new List<string>()
    {
        "Assim, eu retorno aos céus, minha luz volta a refletir nas águas e pratas da noite.",
        "A pequena Naiá sempre sonhou em ser uma estrela.",
        "Na Penumbra, ela provou que não precisa da minha coreografia, ela mostrou com cada compasso em cada ataque que é de fato, estrela.",
        "Mas eu percebo que o mar e a terra também precisam de estrelas, Naiá.",
        "Dance com as ondas e gire com os ventos, Vitória-Régia.",
    };

    private List<Func<IEnumerator>> sceneFuncs;
    private bool isTyping = false;
    private bool skipTyping = false;
    private bool skipSceneDelay = false;
    private bool isSkipped = false;

    private void Awake()
    {
        sceneFuncs = new List<Func<IEnumerator>>()
        {
            SceneOne,
            SceneTwo,
            SceneThree,
            SceneFour,
            SceneFive
        };
    }

    public override void BindActors() {}

    private void Update()
    {
        if (isSkipped) return;

        bool advancePressed = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) ||
                              (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                              (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        // Pular a cutscene inteira: apenas no ESC
        bool skipAllPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (skipAllPressed)
        {
            SkipEntireCutscene();
            return;
        }

        if (advancePressed)
        {
            if (isTyping)
            {
                skipTyping = true;
            }
            else
            {
                skipSceneDelay = true;
            }
        }
    }

    private void EnsureSkipPromptUI()
    {
        if (skipPromptText != null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
        Transform targetParent = parentCanvas != null ? parentCanvas.transform : transform;

        GameObject promptObj = new GameObject("Cutscene_SkipPrompt");
        promptObj.transform.SetParent(targetParent, false);

        RectTransform rt = promptObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-28f, 22f);
        rt.sizeDelta = new Vector2(600f, 40f);

        skipPromptText = promptObj.AddComponent<TextMeshProUGUI>();
        skipPromptText.text = "[Espaço / Clique] Avançar  •  [ESC] Pular";
        skipPromptText.fontSize = 17f;
        skipPromptText.alignment = TextAlignmentOptions.BottomRight;
        skipPromptText.color = new Color(1f, 1f, 1f, 0.75f);

        // Adiciona sombra sutil para legibilidade
        Shadow shadow = promptObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private IEnumerator TypeText(string textToType)
    {
        isTyping = true;
        skipTyping = false;

        if (dialogText != null)
        {
            dialogText.text = textToType;
            dialogText.maxVisibleCharacters = 0;
        }

        for (int i = 0; i <= textToType.Length; i++)
        {
            if (skipTyping || isSkipped)
            {
                if (dialogText != null) dialogText.maxVisibleCharacters = textToType.Length;
                break;
            }

            if (dialogText != null) dialogText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
    }

    private void ChangeScene()
    {
        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopVoice();
        }

        if (curScene < scenes.Count && scenes[curScene] != null)
        {
            scenes[curScene].SetActive(false);
        }
        curScene += 1;
    }

    private IEnumerator PlayScene()
    {
        if (isSkipped) yield break;

        skipSceneDelay = false;
        skipTyping = false;

        if (curScene < scenes.Count && scenes[curScene] != null)
        {
            scenes[curScene].SetActive(true);
        }

        // Toca a dublagem da cena se configurada
        if (sceneVoices != null && curScene < sceneVoices.Count && sceneVoices[curScene] != null && AudioController.Instance != null)
        {
            AudioController.Instance.PlayVoice(sceneVoices[curScene]);
        }

        if (curScene < sceneText.Count)
        {
            StartCoroutine(TypeText(sceneText[curScene]));
        }

        if (curScene < sceneFuncs.Count)
        {
            StartCoroutine(sceneFuncs[curScene]());
        }

        // Aguarda a digitação terminar ou ser acelerada pelo jogador
        while ((isTyping || AudioController.Instance.GetVoiceBusy()) && !isSkipped)
        {
            if (!isTyping && skipSceneDelay) { break; }
            yield return null;
        }

        // Aguarda 2 segundos ou avanço imediato se o jogador pressionar Espaço/Clique
        float waitTimer = 2.0f;
        while (waitTimer > 0f && !skipSceneDelay && !isSkipped)
        {
            waitTimer -= Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator SceneOne()
    {
        yield return null;
    }

    private IEnumerator SceneTwo()
    {
        yield return null;
    }

    private IEnumerator SceneThree()
    {

        if (Naia != null) Tween.UIAnchoredPositionY(Naia, -71, 2f);
        float timer = 2f;
        while (timer > 0f && !isSkipped)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        if (Enemy_Left != null) Tween.UIAnchoredPositionX(Enemy_Left, -683, 2f);
        if (Enemy_BottomRight != null) Tween.UIAnchoredPosition(Enemy_BottomRight, new Vector2(758, -298), 2f);
        if (Enemy_TopRight != null) yield return Tween.UIAnchoredPosition(Enemy_TopRight, new Vector2(715, 127), 2f).ToYieldInstruction();

        yield return null;
    }

    private IEnumerator SceneFour()
    {
        yield return null;
    }

    private IEnumerator SceneFive()
    {
        yield return null;
    }

    public void SkipEntireCutscene()
    {
        if (isSkipped) return;
        isSkipped = true;
        Debug.Log("[CutsceneClip] Cutscene pulada pelo jogador (Skip).");

        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopVoice();
            if (BGMusic) AudioController.Instance.StopBGM();
        }

        StopAllCoroutines();

        // Para tweens ativos
        if (dacingObjects != null)
        {
            foreach (var item in dacingObjects)
            {
                if (item != null)
                {
                    RectTransform rt = item.GetComponent<RectTransform>();
                    if (rt != null) Tween.StopAll(rt);
                }
            }
        }

        if (Naia != null) Tween.StopAll(Naia);
        if (Enemy_Left != null) Tween.StopAll(Enemy_Left);
        if (Enemy_TopRight != null) Tween.StopAll(Enemy_TopRight);
        if (Enemy_BottomRight != null) Tween.StopAll(Enemy_BottomRight);

        // Desativa cenas ativas
        if (scenes != null)
        {
            foreach (var sc in scenes)
            {
                if (sc != null) sc.SetActive(false);
            }
        }

        if (parent != null && parent.gameStateManager != null)
        {
            parent.gameStateManager.SetState(GameState.Playing);
        }
        else if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Playing);
        }

        if (parent != null && parent.onEnd != null)
        {
            var endCallback = parent.onEnd;
            parent.onEnd = null;
            endCallback.Invoke();
        }

        Destroy(gameObject);
    }

    public override IEnumerator Play()
    {
        Debug.Log("[CutsceneClip] Iniciando Cutscene...");
        if(BGMusic) AudioController.Instance.PlayBGM(BGMusic);
        EnsureSkipPromptUI();

        if (dacingObjects != null)
        {
            foreach (var item in dacingObjects)
            {
                if (item == null) continue;
                RectTransform rectTransform = item.GetComponent<RectTransform>();
                if (rectTransform == null) continue;

                Tween.StopAll(rectTransform);
                Tween.UIAnchoredPosition(
                    target: rectTransform,
                    endValue: rectTransform.anchoredPosition + new Vector2(0, 15f),
                    duration: 0.6f,
                    ease: Ease.InOutSine,
                    cycles: -1,
                    cycleMode: CycleMode.Yoyo
                );

                Tween.LocalRotation(
                    target: rectTransform,
                    startValue: Quaternion.Euler(0, 0, -4f),
                    endValue: Quaternion.Euler(0, 0, 4f),
                    duration: 0.8f,
                    ease: Ease.InOutSine,
                    cycles: -1,
                    cycleMode: CycleMode.Yoyo
                );
            }
        }

        for (var i = 0; i < sceneFuncs.Count; i++)
        {
            if (isSkipped) yield break;
            yield return StartCoroutine(PlayScene());
            if (isSkipped) yield break;
            ChangeScene();
        }

        if (!isSkipped)
        {
            if (AudioController.Instance != null)
            {
                AudioController.Instance.StopVoice();
                if (BGMusic) AudioController.Instance.StopBGM();
            }

            if (parent != null && parent.gameStateManager != null) { parent.gameStateManager.SetState(GameState.Playing); }
            else if (GameStateManager.Instance != null) { GameStateManager.Instance.SetState(GameState.Playing); }

            if (parent != null && parent.onEnd != null)
            {
                var endCallback = parent.onEnd;
                parent.onEnd = null;
                endCallback.Invoke();
            }

            Destroy(gameObject);
        }
    }
}
