using PrimeTween;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CutsceneClip : ICinematicClip
{
    int curScene;

    [Header("Scenes Global")]
    [SerializeField] List<GameObject> scenes;
    [SerializeField] List<GameObject> dacingObjects;

    [Header("Dialogue")]
    [SerializeField] GameObject dialogBox;
    [SerializeField] TMP_Text dialogText;
    [SerializeField] float typeSpeed = 0.06f;

    [Header("Scene 1 Objects")]
    [Header("Scene 2 Objects")]
    [Header("Scene 3 Objects")]
    [SerializeField] RectTransform Cuca;
    [SerializeField] RectTransform Matinta;
    [SerializeField] RectTransform Boitata;

    [Header("Scene 4 Objects")]

    [Header("Scene 5 Objects")]
    [SerializeField] RectTransform Naia;
    [SerializeField] RectTransform Enemy_Left;
    [SerializeField] RectTransform Enemy_Center;
    [SerializeField] RectTransform Enemy_Right;


    List<string> sceneText = new List<string>()
    {
        "Antes da noite eterna, o céu era populado por brilhantes dançarinas em formato de estrelas, selecionadas a dedo pela Lua, que iluminava as coreografias com sua luz, acompanhando e protegendo os viajantes na madrugada.",
        "Eu sempre sonhei em ser uma delas, desses brilhos no céu, ser escolhida por Jaci, pela Lua.",
        "Mas um dia isso mudou. As forças que envenenam nossa terra se juntaram, corromperam nossos protetores e roubaram a lua do céu.",
        "Das estrelas que sobraram, a Mãe d'Ouro me escolheu. ",
        "Meu nome é Naiá, eu vou provar o meu valor, resgatar Jaci e garantir meu lugar no céu.",

    };
    List<Func<IEnumerator>> sceneFuncs;
    private bool isTyping = false;

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

    private IEnumerator TypeText(string textToType)
    {
        isTyping = true;
        dialogText.text = textToType;
        dialogText.maxVisibleCharacters = 0; 

        for (int i = 0; i <= textToType.Length; i++)
        {
            dialogText.maxVisibleCharacters = i;

            yield return new WaitForSeconds(typeSpeed);
        }
        isTyping = false;
    }
    void ChangeScene()
    {
        scenes[curScene].SetActive(false);
        curScene += 1;
    }
    private IEnumerator PlayScene()
    {
        scenes[curScene].SetActive(true);
        StartCoroutine(TypeText(sceneText[curScene]));
        yield return StartCoroutine(sceneFuncs[curScene]());
        while (isTyping) {yield return null;}
        yield return new WaitForSeconds(2);
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
        Tween.UIAnchoredPositionX(Cuca.GetComponent<RectTransform>(), 0, 2f);
        Tween.UIAnchoredPositionX(Matinta.GetComponent<RectTransform>(), 0, 2f);
        yield return null;
    }

    private IEnumerator SceneFour()
    {
        yield return null;
    }

    private IEnumerator SceneFive()
    {
        Tween.UIAnchoredPositionY(Naia.GetComponent<RectTransform>(), -260, 2f);
        yield return new WaitForSeconds(2f);
        Tween.UIAnchoredPositionX(Enemy_Right.GetComponent<RectTransform>(), 708, 2f);
        Tween.UIAnchoredPositionY(Enemy_Center.GetComponent<RectTransform>(), 210, 2f);
        yield return Tween.UIAnchoredPositionX(Enemy_Left.GetComponent<RectTransform>(), -633, 2f).ToYieldInstruction();

    }

    public override IEnumerator Play() {
        Debug.Log("playinggg");
        foreach (var item in dacingObjects)
        {
            RectTransform rectTransform = item.GetComponent<RectTransform>();
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


        for (var i = 0; i < sceneFuncs.Count; i++)
        {
            yield return StartCoroutine(PlayScene());
            ChangeScene();
        }

        if (parent != null && parent.gameStateManager != null) {parent.gameStateManager.SetState(GameState.Playing);}
        else if (GameStateManager.Instance != null) {GameStateManager.Instance.SetState(GameState.Playing);}

        if (parent.onEnd != null){parent.onEnd();}
        Destroy(gameObject);

    }
}
