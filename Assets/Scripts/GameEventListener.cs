using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    public GameEvent gameEvent;
    public UnityEvent response = new UnityEvent();

    private void Awake()
    {
        if (gameEvent != null)
        {
            gameEvent.OnEventRaised += Respond;
        }
    }

    private void Respond() { Debug.Log("giving response"); response?.Invoke();}
  
}
