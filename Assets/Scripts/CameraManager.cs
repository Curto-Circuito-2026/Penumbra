using PrimeTween;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] public Transform target;

    Camera cam;

    public bool isFollowing = true;

    private Vector3 velocity = Vector3.zero;
    public float smoothTime = 0.2f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        target = null;
        isFollowing = true;
        velocity = Vector3.zero;
        FindPlayerTarget();
    }

    private void Start()
    {
        FindPlayerTarget();
    }

    private void FindPlayerTarget()
    {
        if (target != null) return;

        // 1. Procura por Tag "Player" (Gameplay)
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null && playerObj.GetComponent<AllyCompanionAI>() == null)
        {
            target = playerObj.transform;
            return;
        }

        // 2. Procura por CharacterController2D
        CharacterController2D playerCC = Object.FindAnyObjectByType<CharacterController2D>();
        if (playerCC != null)
        {
            target = playerCC.transform;
            return;
        }

        // 3. Procura por objeto chamado "Player" ou "Naia"
        GameObject direct = GameObject.Find("Player") ?? GameObject.Find("Naia");
        if (direct != null)
        {
            target = direct.transform;
            return;
        }

        // 4. Procura por Actor protagonista (Cenas Cinemáticas)
        Actor[] actors = Object.FindObjectsByType<Actor>(FindObjectsSortMode.None);
        foreach (var a in actors)
        {
            if (a.actorName == "Naia" || a.name == "Naia" || a.name.Contains("Player"))
            {
                target = a.transform;
                return;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindPlayerTarget();
        }

        if (!isFollowing || target == null) return;

        Vector3 targetPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    public void SetCamSpeed(float speed)
    {
        velocity = new Vector3(speed, speed, 0f);
    }
    public void SetTarget(Transform newTarget, Vector3? customOffset = null)
    {
        target = newTarget;
        if (customOffset.HasValue)
        {
            offset = customOffset.Value;
        }
        else
        {
            offset = new Vector3(0f, 0f, -10f);
        }
        isFollowing = true;
    }

    public Tween Move(Vector2 pos, float duration)
    {
        isFollowing = false;
        Vector3 finalPos = new Vector3(pos.x, pos.y, transform.position.z);
        return Tween.Position(transform, finalPos, duration, Ease.InOutSine);
    }

    public void SetPos(Vector2 pos)
    {
        isFollowing = false;
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    public Tween Zoom(float zoom, float duration) { return Tween.CameraOrthographicSize(cam, zoom, duration, Ease.InOutSine); }

    public Tween Shake(float strength, float duration)
    {
        Vector3 shakeStrength = new Vector3(strength, strength, 0f);
        return Tween.ShakeLocalPosition(transform, shakeStrength, duration);
    }

}
