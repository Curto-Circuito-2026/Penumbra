using System;
using System.Collections;
using System.Collections.Generic;
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
        }

        Debug.Log("Awake");
        if (activeAnimation != TransitionType.None) { transitions[activeAnimation].gameObject.SetActive(false);}

    }

    public void LoadScene(int sceneIndex, TransitionType animationType = TransitionType.None)
    {
        if (animationType != TransitionType.None) { 
            transitions[animationType].gameObject.SetActive(true);
            activeAnimation = animationType;
        }

        StartCoroutine(LoadSceneAsync(sceneIndex));
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
}