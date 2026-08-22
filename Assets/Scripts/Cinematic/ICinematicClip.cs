
using System.Collections;
using UnityEngine;

public abstract class ICinematicClip : MonoBehaviour
{
    protected CinematicManager parent;

    private void Awake()
    {
        BindActors();
    }

    public void SetParent(CinematicManager cinematicManager) {parent = cinematicManager;}

    public virtual void BindActors() { }

    public virtual IEnumerator Play() { yield return null; }
}
