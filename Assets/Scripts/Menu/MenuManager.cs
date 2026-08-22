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

    public void PlayGame()
    {
        Debug.Log("[MenuManager] Botão 'Jogar' clicado!");
        StartCoroutine(playGameCoroutine());
    }

    IEnumerator playGameCoroutine()
    {
        if (sceneLoader == null) sceneLoader = SceneController.Instance ?? FindAnyObjectByType<SceneController>();
        if (cinematicManager == null) cinematicManager = CinematicManager.Instance ?? FindAnyObjectByType<CinematicManager>();

        System.Action setupCutscene = () =>
        {
            // Desativa o painel inicial do menu enquanto a tela está preta
            if (panels != null && activePanel >= 0 && activePanel < panels.Length && panels[activePanel] != null)
            {
                panels[activePanel].SetActive(false);
            }

            if (cinematicManager != null && mainCutscene != null)
            {
                cinematicManager.PlayClip(mainCutscene);
                cinematicManager.onEnd = () => {
                    if (endMenu != null) endMenu.SetActive(true);
                    if (sceneLoader != null)
                    {
                        sceneLoader.LoadScene(1, TransitionType.CrossFade);
                    }
                    else
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
                    }
                    cinematicManager.onEnd = null;
                };
            }
            else
            {
                Debug.LogWarning("[MenuManager] Cutscene ou CinematicManager não encontrado! Carregando fase diretamente...");
                if (sceneLoader != null)
                {
                    sceneLoader.LoadScene(1, TransitionType.CrossFade);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(1);
                }
            }
        };

        if (sceneLoader != null)
        {
            yield return StartCoroutine(sceneLoader.PlayTransition(TransitionType.CrossFade, setupCutscene));
        }
        else
        {
            setupCutscene();
        }
    }

    public void QuitGame(){Application.Quit();}

    public void ChangePanel(int panel) {
        panels[activePanel].SetActive(false);
        panels[panel].SetActive(true);
        activePanel = panel;
    }
}
