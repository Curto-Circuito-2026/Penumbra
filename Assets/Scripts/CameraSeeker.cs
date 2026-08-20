using UnityEngine;

public class CameraSeeker : MonoBehaviour
{
    void Start()
    {
        Canvas canvas = GetComponent<Canvas>();

        canvas.worldCamera = Camera.main;
    }
}
