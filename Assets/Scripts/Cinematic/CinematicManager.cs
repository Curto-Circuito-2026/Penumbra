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

            if (TopBar != null) topBarRect = TopBar.GetComponent<RectTransform>();
            if (BottomBar != null) bottomBarRect = BottomBar.GetComponent<RectTransform>();

            ResolveCameraReferences();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void ResolveCameraReferences()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null && (camManager == null || camManager.gameObject != cam.gameObject))
        {
            camManager = cam.GetComponent<CameraManager>() ?? UnityEngine.Object.FindAnyObjectByType<CameraManager>();
        }
        else if (camManager == null)
        {
            camManager = UnityEngine.Object.FindAnyObjectByType<CameraManager>();
        }
    }

    public Tween ToggleBars(bool show)
    {
        if (topBarRect == null && TopBar != null) topBarRect = TopBar.GetComponent<RectTransform>();
        if (bottomBarRect == null && BottomBar != null) bottomBarRect = BottomBar.GetComponent<RectTransform>();

        if (show) {
            if (topBarRect != null) Tween.UIAnchoredPositionY(topBarRect, 0f, 1f, Ease.InOutSine);
            if (bottomBarRect != null) return Tween.UIAnchoredPositionY(bottomBarRect, 0f, 1f, Ease.InOutSine);
            return default;
        }
        else
        {
            if (topBarRect != null) Tween.UIAnchoredPositionY(topBarRect, 180f, 1f, Ease.InOutSine);
            if (bottomBarRect != null) return Tween.UIAnchoredPositionY(bottomBarRect, -180f, 1f, Ease.InOutSine);
            return default;
        }
    }

    public void PlayClip(GameObject clip)
    {
        ResolveCameraReferences();

        if (gameStateManager == null) gameStateManager = GameStateManager.Instance;
        if (gameStateManager != null) gameStateManager.SetState(GameState.Cutscene);

        GameObject i = Instantiate(clip, transform);
        ICinematicClip c = i.GetComponent<ICinematicClip>();
        c.SetParent(this);
        StartCoroutine(c.Play());
    }

    public void ShowTitle(string title, string subTitle, bool independent = false)
    {
        if (gameStateManager == null) gameStateManager = GameStateManager.Instance;

        if (independent && gameStateManager != null)
        {
            gameStateManager.SetState(GameState.Cutscene);
        }

        try
        {
            if (titleText != null) titleText.SetText(title);
            if (subtitleText != null) subtitleText.SetText(subTitle);
            if (TitleContainer != null) TitleContainer.SetActive(true);

            ToggleBars(true).OnComplete(() =>
            {
                if (titleContainerGroup != null)
                {
                    Tween.Alpha(titleContainerGroup, 1f, 1f, Ease.InOutSine).OnComplete(() =>
                    {
                        StartCoroutine(CloseTitleCoroutine(independent));
                    });
                }
                else
                {
                    StartCoroutine(CloseTitleCoroutine(independent));
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CinematicManager] Falha ao exibir título: {ex.Message}");
            if (independent && gameStateManager != null)
            {
                gameStateManager.SetState(GameState.Playing);
            }
        }
    }

    private IEnumerator CloseTitleCoroutine(bool independent)
    {
        yield return new WaitForSeconds(1.5f);
        ToggleBars(false);
        if (titleContainerGroup != null)
        {
            yield return Tween.Alpha(titleContainerGroup, 0f, 1f, Ease.InOutSine).ToYieldInstruction();
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        if (TitleContainer != null) TitleContainer.SetActive(false);

        if (independent)
        {
            if (gameStateManager == null) gameStateManager = GameStateManager.Instance;
            if (gameStateManager != null)
            {
                gameStateManager.SetState(GameState.Playing);
            }
        }
    }
}
