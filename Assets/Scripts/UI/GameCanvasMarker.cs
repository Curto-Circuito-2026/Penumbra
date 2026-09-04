using UnityEngine;

/// <summary>
/// Marcador de identificação do GameCanvas principal.
/// Garante que o GameCanvas seja único e persista entre cenas (DontDestroyOnLoad).
/// </summary>
public class GameCanvasMarker : MonoBehaviour
{
    private static GameCanvasMarker instance;

    public static GameCanvasMarker Instance => instance;

    [Header("Persistência")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (dontDestroyOnLoad && Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
}
