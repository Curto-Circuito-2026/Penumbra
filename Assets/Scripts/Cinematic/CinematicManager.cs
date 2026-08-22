using PrimeTween;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
[DefaultExecutionOrder(-100)]
public class CinematicManager : MonoBehaviour
{
    public static CinematicManager Instance { get; private set; }

    [SerializeField] public Camera cam;

    public CameraManager camManager;
    [SerializeField] public GameObject CanvasObject;

    [SerializeField] public GameObject TopBar;
    private RectTransform topBarRect;
    [SerializeField] public GameObject BottomBar;
    private RectTransform bottomBarRect;

    [SerializeField] public GameObject TitleContainer;
    [SerializeField] private CanvasGroup titleContainerGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;

    public Action onEnd;

    [SerializeField] public GameStateManager gameStateManager;

    private void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            topBarRect = TopBar.GetComponent<RectTransform>();
            bottomBarRect = BottomBar.GetComponent<RectTransform>();

            camManager = cam.GetComponent<CameraManager>();
        }
        else
        {
            Destroy(gameObject);
        }

    }

    public Tween ToggleBars(bool show)
    {
        if (show) {
            Tween.UIAnchoredPositionY(topBarRect, 0f, 1f, Ease.InOutSine);
            return Tween.UIAnchoredPositionY(bottomBarRect, 0f, 1f, Ease.InOutSine);
        }
        else
        {
            Tween.UIAnchoredPositionY(topBarRect, 180f, 1f, Ease.InOutSine);
            return Tween.UIAnchoredPositionY(bottomBarRect, -180f, 1f, Ease.InOutSine);
        }
    }

    public void PlayClip(GameObject clip)
    {
        if (gameStateManager == null) gameStateManager = GameStateManager.Instance;
        if (gameStateManager != null) gameStateManager.SetState(GameState.Cutscene);

        GameObject i = Instantiate(clip, transform);
        ICinematicClip c = i.GetComponent<ICinematicClip>();
        c.SetParent(this);
        StartCoroutine(c.Play());
    }

    public void ShowTitle(string title, string subTitle, bool independent = false)
    {
        if(independent)
        {
            gameStateManager.SetState(GameState.Cutscene);
        }
        titleText.SetText(title);
        subtitleText.SetText(subTitle);
        TitleContainer.SetActive(true);
        ToggleBars(true).OnComplete(() =>
        {
            Tween.Alpha(titleContainerGroup, 1f, 1f, Ease.InOutSine).OnComplete(() => { StartCoroutine(CloseTitleCoroutine()); });
        });

    }

    private IEnumerator CloseTitleCoroutine()
    {
        yield return new WaitForSeconds(1f);
        ToggleBars(false);
        Tween.Alpha(titleContainerGroup, 0f, 1f, Ease.InOutSine).OnComplete(() => {TitleContainer.SetActive(false);});
        gameStateManager.SetState(GameState.Playing);
    }
}
