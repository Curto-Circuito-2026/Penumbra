using PrimeTween;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

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
    private CanvasGroup titleContainerGroup;
    private TMP_Text titleText;
    private TMP_Text subtitleText;

    [SerializeField] public GameObject testPrefab;


    private void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            topBarRect = TopBar.GetComponent<RectTransform>();
            bottomBarRect = BottomBar.GetComponent<RectTransform>();

            titleContainerGroup = TitleContainer.GetComponent<CanvasGroup>();
            titleText = TitleContainer.transform.Find("Title").GetComponent<TMP_Text>();
            subtitleText = TitleContainer.transform.Find("Subtitle").GetComponent<TMP_Text>();

            camManager = cam.GetComponent<CameraManager>();
        }
        else
        {
            Destroy(gameObject);
        }

        PlayClip(testPrefab);
        //ShowTitle("teste", "subtituloTeste");
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
        GameObject i = Instantiate(clip, transform);
        ICinematicClip c = i.GetComponent<ICinematicClip>();
        c.SetParent(this);
        StartCoroutine(c.Play());
        //Debug.Log("end");
        //Destroy(i);
    }

    public void ShowTitle(string title, string subTitle)
    {
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
    }
}
