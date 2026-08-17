using System;
using System.Collections.Generic;
using System.Linq;
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
}

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }
    [SerializeField] List<Region> regions;
    [SerializeField] CinematicManager cinematicManager;

    [SerializeField] SceneController sceneController;

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string title = "";
        string subTitle = "";
        if(scene.name == "Hub"){ title = "A Terra Sem Mal"; subTitle = "Yby Marã E'Yma"; }
        else
        {
            title = regions[runOrder[curRegion]].title;
            subTitle = regions[runOrder[curRegion]].subtitle;
        }
        cinematicManager.ShowTitle(title, subTitle);

    }

    public void StartRun()
    {
        curRegion = 0;
        int[] regions = { 0, 1, 2 };
        runOrder = regions.OrderBy(x => Random.value).ToList();
        runOrder.Append(3);
        PassRegion();

    }

    void PassRegion()
    {
        SceneAsset curScene = regions[runOrder[curRegion]].scene;
        sceneController.LoadScene(curScene.name);
        curRegion += 1;
    }

    
}
