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

    [Header("Audio & BGM")]
    [Tooltip("Música de fundo a ser tocada no Menu Principal.")]
    [SerializeField] public AudioClip menuBgmClip;
    [Tooltip("Tempo de fade out da música ao clicar em Jogar.")]
    [SerializeField] public float bgmFadeOutDuration = 1.2f;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Menu);
        }

        if (menuBgmClip == null)
        {
            menuBgmClip = Resources.Load<AudioClip>("Audio/Menu")
                       ?? Resources.Load<AudioClip>("Menu");
#if UNITY_EDITOR
            if (menuBgmClip == null)
            {
                menuBgmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Menu.mp3");
            }
#endif
        }

        if (menuBgmClip != null && AudioController.Instance != null)
        {
            AudioController.Instance.PlayBGM(menuBgmClip, fadeDuration: 1f, loop: true);
        }
    }

    public void PlayGame()
    {
        Debug.Log("[MenuManager] Botão 'Jogar' clicado!");

        // Inicia o fade out suave da música do menu
        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopBGM(fadeDuration: bgmFadeOutDuration);
        }

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
