using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;


public enum TransitionType
{
    None,
    CrossFade
}
[Serializable]
public struct TransitionProperties
{
    public GameObject gameObject;
    public Animator animator;
    public float time;
}

[Serializable]
public struct TransitionEntry
{
    public TransitionType key;
    public TransitionProperties value;
}

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    TransitionType activeAnimation = TransitionType.None;

    [SerializeField] private List<TransitionEntry> serializedTransitions = new List<TransitionEntry>();

    Dictionary<TransitionType, TransitionProperties> transitions = new Dictionary<TransitionType, TransitionProperties>();

    public void OnBeforeSerialize()
    {
        serializedTransitions.Clear();
        foreach (var kvp in transitions)
        {
            serializedTransitions.Add(new TransitionEntry { key = kvp.Key, value = kvp.Value });
        }
    }

    public void OnAfterDeserialize()
    {
        transitions.Clear();
        for (int i = 0; i < serializedTransitions.Count; i++)
        {
            if (!transitions.ContainsKey(serializedTransitions[i].key))
            {
                transitions.Add(serializedTransitions[i].key, serializedTransitions[i].value);
            }
        }
    }

    private void Awake()
    {
       
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            transitions.Clear();
            foreach (var kvp in serializedTransitions){ transitions.Add(kvp.key, kvp.value);}
            serializedTransitions.Clear();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Debug.Log("Awake");
        if (activeAnimation != TransitionType.None) { transitions[activeAnimation].gameObject.SetActive(false);}

    }

    public IEnumerator PlayTransition(TransitionType transition, Action onBlackScreen = null)
    {
        if (transition == TransitionType.None || !transitions.ContainsKey(transition) || transitions[transition].gameObject == null)
        {
            onBlackScreen?.Invoke();
            yield break;
        }

        float elapsedTime = 0f;
        float targetTime = 0f;
        transitions[transition].gameObject.SetActive(true);
        activeAnimation = transition;
        if (transitions[activeAnimation].animator != null)
        {
            transitions[activeAnimation].animator.ResetTrigger("End");
            transitions[activeAnimation].animator.SetTrigger("Start");
        }
        targetTime = transitions[activeAnimation].time;
        while (elapsedTime < targetTime)
        {
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        // Executa a troca (ex: ocultar menu e instanciar cutscene) com a tela 100% preta
        onBlackScreen?.Invoke();

        if (transitions[activeAnimation].animator != null)
        {
            transitions[activeAnimation].animator.ResetTrigger("Start");
            transitions[activeAnimation].animator.SetTrigger("End");
        }

        yield return new WaitForSecondsRealtime(0.35f);
        if (transitions.ContainsKey(activeAnimation) && transitions[activeAnimation].gameObject != null)
        {
            transitions[activeAnimation].gameObject.SetActive(false);
        }
    }

    public void LoadScene(int sceneIndex, TransitionType animationType = TransitionType.None)
    {
        if (animationType != TransitionType.None) { 
            transitions[animationType].gameObject.SetActive(true);
            activeAnimation = animationType;
        }

        StartCoroutine(LoadSceneAsync(sceneIndex));
    }

    public void LoadScene(string sceneName, TransitionType animationType = TransitionType.None)
    {
        if (animationType != TransitionType.None)
        {
            try
            {
                transitions[animationType].gameObject.SetActive(true);
                activeAnimation = animationType;
            }
            catch (Exception e)
            {
                Debug.Log("Animação não carregada");
            }
        }

        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(int sceneIndex)
    {
        float elapsedTime = 0f;
        float targetTime = 0f;
        if (activeAnimation != TransitionType.None)
        {
            transitions[activeAnimation].animator.ResetTrigger("End");
            transitions[activeAnimation].animator.SetTrigger("Start");
            targetTime = transitions[activeAnimation].time;
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        asyncLoad.allowSceneActivation = false;

        while (elapsedTime < targetTime || asyncLoad.progress < 0.9f)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        asyncLoad.allowSceneActivation = true;
        if (activeAnimation != TransitionType.None)
        {
            transitions[activeAnimation].animator.ResetTrigger("Start");
            transitions[activeAnimation].animator.SetTrigger("End");

            yield return null;

            while (transitions[activeAnimation].animator.IsInTransition(0) || transitions[activeAnimation].animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
            {
                yield return null;
            }
            transitions[activeAnimation].animator.gameObject.SetActive(false);
        }
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        float elapsedTime = 0f;
        float targetTime = 0f;
        if (activeAnimation != TransitionType.None)
        {
            transitions[activeAnimation].animator.ResetTrigger("End");
            transitions[activeAnimation].animator.SetTrigger("Start");
            targetTime = transitions[activeAnimation].time;
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (elapsedTime < targetTime || asyncLoad.progress < 0.9f)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        asyncLoad.allowSceneActivation = true;
        if (activeAnimation != TransitionType.None)
        {
            transitions[activeAnimation].animator.ResetTrigger("Start");
            transitions[activeAnimation].animator.SetTrigger("End");

            yield return null;

            while (transitions[activeAnimation].animator.IsInTransition(0) || transitions[activeAnimation].animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
            {
                yield return null;
            }
            transitions[activeAnimation].animator.gameObject.SetActive(false);
        }
    }
}