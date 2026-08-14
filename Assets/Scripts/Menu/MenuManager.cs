using UnityEngine;

public class MenuManager : MonoBehaviour
{
    int activePanel = 0;

    [SerializeField] SceneController sceneLoader;

    [SerializeField] GameObject[] panels;

    public void PlayGame(){
        sceneLoader.LoadScene(1, TransitionType.CrossFade);
    }

    public void QuitGame(){Application.Quit();}

    public void ChangePanel(int panel) {
        panels[activePanel].SetActive(false);
        panels[panel].SetActive(true);
        activePanel = panel;
    }
}
