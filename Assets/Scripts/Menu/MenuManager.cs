using System;
using System.Collections;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    int activePanel = 0;

    [SerializeField] GameObject[] panels;

    public void PlayGame(){
        Debug.Log("startLoad");
        StartCoroutine(LoadSceneAsyncCoroutine());
    }

    private IEnumerator LoadSceneAsyncCoroutine()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        UnityEngine.AsyncOperation operation = SceneManager.LoadSceneAsync(nextSceneIndex);

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            Debug.Log($"{progress * 100}");

            yield return null;
        }

        Debug.Log("endLoad");
    }

    public void QuitGame(){Application.Quit();}

    public void ChangePanel(int panel) {
        panels[activePanel].SetActive(false);
        panels[panel].SetActive(true);
        activePanel = panel;
    }
}
