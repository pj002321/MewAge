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
    [SerializeField] private bool followPlayer = true;

    [Header("부드러움 (0 = 즉시, 높을수록 느리게)")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("경계 제한")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 boundsMin = new(-10f, -18f);
    [SerializeField] private Vector2 boundsMax = new( 10f,  18f);

    [Header("피격 카메라 셰이크")]
    [SerializeField] private float shakeDefaultDuration  = 0.18f;
    [SerializeField] private float shakeDefaultMagnitude = 0.12f;

    public static CameraFollow Shared { get; private set; }

    private Camera  cam;
    private Vector3 velocity;
    private Vector2 shakeOffset;
    private float   shakeTimeLeft;
    private float   shakeMagnitudeCurrent;

    private void Awake()
    {
        Shared = this;
        cam    = GetComponent<Camera>();
        if (target == null && PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    private void Start()
    {
        // PlayerController가 Awake보다 늦게 초기화될 수 있으므로 Start에서도 시도
        if (target == null && PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    /// <summary>환경 전환 시 카메라 동작을 일괄 설정합니다.</summary>
    public void ApplyConfig(bool follow, bool bounds, Vector2 min, Vector2 max)
    {
        followPlayer = follow;
        useBounds    = bounds;
        boundsMin    = min;
        boundsMax    = max;
        velocity     = Vector3.zero; // 이전 스무딩 속도 초기화

        // 플레이어 추적을 끄면 카메라를 원점으로 즉시 이동 (배경 밀림 방지)
        if (!follow)
            transform.position = new Vector3(0f, 0f, transform.position.z);
    }

    /// <summary>카메라를 잠깐 흔듭니다. PlayerController.TakeDamage에서 호출합니다.</summary>
    public void Shake(float duration = -1f, float magnitude = -1f)
    {
        shakeTimeLeft          = duration  > 0f ? duration  : shakeDefaultDuration;
        shakeMagnitudeCurrent  = magnitude > 0f ? magnitude : shakeDefaultMagnitude;
    }

    private void LateUpdate()
    {
        if (followPlayer)
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

        // 카메라 셰이크 오프셋 적용 (이전 오프셋 제거 후 새 오프셋 추가)
        transform.position -= (Vector3)shakeOffset;
        if (shakeTimeLeft > 0f)
        {
            shakeOffset     = Random.insideUnitCircle * shakeMagnitudeCurrent;
            shakeTimeLeft  -= Time.unscaledDeltaTime;
            transform.position += (Vector3)shakeOffset;
        }
        else
        {
            shakeOffset = Vector2.zero;
        }
    }

    private Vector3 ClampToBounds(Vector3 pos)
    {
        float halfH   = cam.orthographicSize;
        float halfW   = halfH * cam.aspect;
        float centerX = (boundsMin.x + boundsMax.x) * 0.5f;
        float centerY = (boundsMin.y + boundsMax.y) * 0.5f;
        float rangeX  = (boundsMax.x - boundsMin.x) * 0.5f - halfW;
        float rangeY  = (boundsMax.y - boundsMin.y) * 0.5f - halfH;

        // 카메라 뷰가 bounds보다 크면 중앙 고정, 아니면 클램프
        pos.x = rangeX > 0f ? Mathf.Clamp(pos.x, centerX - rangeX, centerX + rangeX) : centerX;
        pos.y = rangeY > 0f ? Mathf.Clamp(pos.y, centerY - rangeY, centerY + rangeY) : centerY;
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
