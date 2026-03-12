using UnityEngine;

/// <summary>
/// 플레이어를 부드럽게 따라가는 카메라.
/// Cinemachine Confiner2D 대신 사용합니다.
/// useBounds를 켜면 카메라 뷰가 지정한 월드 경계 밖으로 나가지 않습니다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform target;

    [Header("부드러움 (0 = 즉시, 높을수록 느리게)")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("경계 제한")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 boundsMin = new(-10f, -18f);
    [SerializeField] private Vector2 boundsMax = new( 10f,  18f);

    private Camera cam;
    private Vector3 velocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (target == null && PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    private void Start()
    {
        // PlayerController가 Awake보다 늦게 초기화될 수 있으므로 Start에서도 시도
        if (target == null && PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            if (PlayerController.Instance != null)
                target = PlayerController.Instance.transform;
            return;
        }

        Vector3 desiredPos = new(target.position.x, target.position.y, transform.position.z);

        if (useBounds)
            desiredPos = ClampToBounds(desiredPos);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothTime);
    }

    private Vector3 ClampToBounds(Vector3 pos)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        pos.x = Mathf.Clamp(pos.x, boundsMin.x + halfW, boundsMax.x - halfW);
        pos.y = Mathf.Clamp(pos.y, boundsMin.y + halfH, boundsMax.y - halfH);
        return pos;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Vector3 center = new((boundsMin.x + boundsMax.x) * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f, 0f);
        Vector3 size   = new(boundsMax.x - boundsMin.x, boundsMax.y - boundsMin.y, 0f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
