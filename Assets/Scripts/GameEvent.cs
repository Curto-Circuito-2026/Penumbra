using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEvent : ScriptableObject
{
    public event Action OnEventRaised;

    public void Raise()
    {
        Debug.Log("raised");
        OnEventRaised?.Invoke();
    }
}
