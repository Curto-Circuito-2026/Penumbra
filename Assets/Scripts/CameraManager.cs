using PrimeTween;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] public Transform target;

    Camera cam;

    public bool isFollowing = true;

    private Vector3 velocity = Vector3.zero;
    public float smoothTime = 0.2f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (!isFollowing || target == null) return;

        Vector3 targetPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    public void SetCamSpeed(float speed)
    {
        velocity = new Vector3(speed, speed, 0f);
    }
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
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
