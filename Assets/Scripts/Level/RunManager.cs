using PrimeTween;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

[Serializable]
public struct Region
{
    public string title;
    public string subtitle;
    public SceneAsset scene;
    public Vector2 spawnPoint;

}


public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }
    [SerializeField] List<Region> regions;
    [SerializeField] CinematicManager cinematicManager;

    [SerializeField] SceneController sceneController;

    [SerializeField] GameObject startRunScreen;
    [SerializeField] GameObject deathScreen;

    [SerializeField] TMP_Text starsText;

    [SerializeField] PlayerStats playerStats;

    int curRegion;
    List<int> runOrder;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        sceneController = FindAnyObjectByType<SceneController>();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        startRunScreen.SetActive(false);
        string title = "";
        string subTitle = "";
        if(scene.name == "Hub")
        {
            playerStats.RestartPlayer();
            title = "A Terra Sem Males"; subTitle = "Yby Marã E'Yma"; 
        }
        else
        {
            title = regions[runOrder[curRegion]].title;
            subTitle = regions[runOrder[curRegion]].subtitle;
        }
        cinematicManager.ShowTitle(title, subTitle);

    }

    public void ShowStartRunScreen(){startRunScreen.SetActive(true);}
    public void CloseStartRunScreen(){startRunScreen.SetActive(false);}


    public void ShowDeathScreen(int starFragments)
    {
        int starAmount = PlayerPrefs.GetInt("PLAYER_STARS_TOTAL");
        starsText.text = $"Seu{(starFragments == 1 ? "" : "s")} {starFragments} fragmento{(starFragments == 1 ? "" : "s")} de estrela\r\n{(starFragments == 1 ? "Foi" : "Foram")} convertido{(starFragments == 1 ? "" : "s")} em\r\n{starAmount} estrela{(starAmount == 1 ? "" : "s") }";
        deathScreen.SetActive(true);
    }
    public void Restart()
    {
        deathScreen.SetActive(false);
        sceneController.LoadScene("Hub", TransitionType.CrossFade);
        playerStats.transform.position = new Vector3(-0.05f, -0.5f, playerStats.transform.position.z);
    }


    public void StartRun()
    {
        curRegion = 0;
        int[] regionVals = { 0, 1, 2 };
        runOrder = regionVals.OrderBy(x => Random.value).ToList();
        runOrder.Append(3);
        Tween.UIAnchoredPositionX(startRunScreen.GetComponent<RectTransform>(), -1920f, 1f, Ease.InOutSine).OnComplete(() =>
        {
            playerStats.transform.position = new Vector3(regions[runOrder[curRegion]].spawnPoint.x, regions[runOrder[curRegion]].spawnPoint.y, playerStats.transform.position.z);
            PassRegion();
        });

    }

    void PassRegion(bool first = false)
    {
        SceneAsset curScene = regions[runOrder[curRegion]].scene;
        sceneController.LoadScene(curScene.name, !first ? TransitionType.CrossFade: TransitionType.None);
        curRegion += 1;
    }

    
}
