using System.Collections;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    int activePanel = 0;

    [SerializeField] SceneController sceneLoader;

    [SerializeField] GameObject[] panels;

    [SerializeField] GameObject mainCutscene;
    [SerializeField] CinematicManager cinematicManager;

    [SerializeField] GameObject endMenu;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Menu);
        }
    }

    public void PlayGame(){
        StartCoroutine(playGameCoroutine());
    }

    IEnumerator playGameCoroutine()
    {
        yield return StartCoroutine(sceneLoader.PlayTransition(TransitionType.CrossFade));
        cinematicManager.PlayClip(mainCutscene);
        cinematicManager.onEnd = () => {
            endMenu.SetActive(true);
            sceneLoader.LoadScene(1, TransitionType.CrossFade);
            cinematicManager.onEnd = null;
        };
        
    }

    public void QuitGame(){Application.Quit();}

    public void ChangePanel(int panel) {
        panels[activePanel].SetActive(false);
        panels[panel].SetActive(true);
        activePanel = panel;
    }
}
