using UnityEngine;

/// <summary>
/// Camera that follows the player with bounds clamping.
/// orthographicSize NEVER changes here — it is completely disconnected from level logic,
/// ensuring that all units maintain exactly the same size on screen in any map.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform target;

    [Header("카메라 줌")]
    [Tooltip("0이면 씬에 설정된 값 그대로 사용. 값을 넣으면 모든 기기에서 동일한 크기로 고정 (유닛 비율 절대 보장)")]
    [SerializeField] private float fixedOrthographicSize = 6.25f;

    [Header("부드러움 (0 = 즉시, 높을수록 느리게)")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("피격 카메라 셰이크")]
    [SerializeField] private float shakeDefaultDuration  = 0.18f;
    [SerializeField] private float shakeDefaultMagnitude = 0.12f;

    public static CameraFollow Shared { get; private set; }

    private Camera  cam;
    private Vector3 velocity;
    private Vector2 shakeOffset;
    private float   shakeTimeLeft;
    private float   shakeMagnitudeCurrent;

    // Runtime state
    private bool    _followPlayer;
    private bool    _useBounds;
    private Vector2 _boundsMin;
    private Vector2 _boundsMax;

    private void Awake()
    {
        Shared = this;
        cam    = GetComponent<Camera>();

        if (target == null && PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    private void Start()
    {
        if (target == null && PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    /// <summary>
    /// GameManager에서 전체 씬이 안정화된 후 (특히 모바일의 Safe Area, 해상도 초기화 이후) 명시적으로 호출합니다.
    /// </summary>
    public void Init()
    {
        // PixelPerfectCamera는 해상도별 정수 픽셀 배율(2x, 3x...)로 projection matrix를 직접 덮어써서
        // 같은 9:16 비율이어도 해상도가 다르면 오브젝트 크기가 달라지는 문제가 있습니다.
        // 먼저 비활성화하고 projection matrix를 초기화한 뒤 orthographicSize를 설정해야 합니다.
        var ppc = GetComponent("PixelPerfectCamera") as Behaviour;
        if (ppc != null)
        {
            ppc.enabled = false;
            cam.ResetProjectionMatrix();
        }

        // 세로형(Portrait) 게임 기준 (1080x1920 = 9:16 비율)
        // 화면 가로 넓이를 일정하게 보장하여 캐릭터 비율이 거대해지지 않도록 방지
        if (fixedOrthographicSize > 0f)
        {
            float targetAspect = 9f / 16f;
            if (cam.aspect < targetAspect)
                cam.orthographicSize = fixedOrthographicSize * (targetAspect / cam.aspect); // 모바일 등 길쭉한 화면
            else
                cam.orthographicSize = fixedOrthographicSize; // 태블릿, PC 등 넓은 화면
        }
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>
    /// Fixed view mode: Camera follows the center of the MovementBounds (Light blue box).
    /// Safe against FitSpriteToScreen scaling order issues.
    /// </summary>
    public void ApplyFixedView(Vector2 camMin, Vector2 camMax)
    {
        _followPlayer = false;
        _useBounds    = true; // We use bounds to determine the lock center
        _boundsMin    = camMin;
        _boundsMax    = camMax;

        velocity      = Vector3.zero;
        shakeOffset   = Vector2.zero;
        shakeTimeLeft = 0f;
    }

    /// <summary>
    /// Dynamic follow mode: Camera tracks player, strictly bounded by camMin/camMax.
    /// </summary>
    public void ApplyDynamicFollow(Vector2 camMin, Vector2 camMax)
    {
        _followPlayer = true;
        _useBounds    = true;
        _boundsMin    = camMin;
        _boundsMax    = camMax;

        velocity      = Vector3.zero;
        shakeOffset   = Vector2.zero;
        shakeTimeLeft = 0f;
    }

    /// <summary>Legacy API — kept for backward compat.</summary>
    public void ApplyConfig(bool follow, bool bounds, Vector2 min, Vector2 max)
    {
        _followPlayer = follow;
        _useBounds    = bounds;
        _boundsMin    = min;
        _boundsMax    = max;
        velocity      = Vector3.zero;
        shakeOffset   = Vector2.zero;
        shakeTimeLeft = 0f;

        if (!follow && !bounds)
            transform.position = new Vector3(0f, 0f, transform.position.z);
    }

    /// <summary>Shake camera briefly. Called from PlayerController.TakeDamage.</summary>
    public void Shake(float duration = -1f, float magnitude = -1f)
    {
        shakeTimeLeft         = duration  > 0f ? duration  : shakeDefaultDuration;
        shakeMagnitudeCurrent = magnitude > 0f ? magnitude : shakeDefaultMagnitude;
    }

    private float lastAspect = -1f;

    private void Update()
    {
        // 모바일 환경에서 씬 전환 직후 Screen 해상도는 동일하지만 Camera.aspect가 Unity 내부에 뒤늦게 세팅되는 현상 방지
        if (Mathf.Abs(cam.aspect - lastAspect) > 0.001f)
        {
            lastAspect = cam.aspect;
            Init();
        }
    }

    // ── LateUpdate ────────────────────────────────────────────────

    private void LateUpdate()
    {
        Vector3 desiredPos = transform.position;

        if (_followPlayer)
        {
            if (target == null)
            {
                if (PlayerController.Instance != null)
                    target = PlayerController.Instance.transform;
            }
            else
            {
                desiredPos = new Vector3(target.position.x, target.position.y, transform.position.z);
            }
        }
        else if (_useBounds)
        {
            // FixedView 모드: 하늘색 영역(MovementBounds)의 정중앙을 타겟으로 잡음
            Vector2 min = _boundsMin;
            Vector2 max = _boundsMax;
            if (MovementBounds.Shared != null)
                MovementBounds.Shared.GetCameraBoundsMinMax(out min, out max);

            desiredPos = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, transform.position.z);
        }

        if (_useBounds && _followPlayer)
            desiredPos = ClampToBounds(desiredPos);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothTime);

        // SmoothDamp can overshoot, clamp again
        if (_useBounds && _followPlayer)
            transform.position = ClampToBounds(transform.position);

        // Camera shake
        transform.position -= (Vector3)shakeOffset;
        if (_useBounds && _followPlayer)
            transform.position = ClampToBounds(transform.position);

        if (shakeTimeLeft > 0f)
        {
            shakeOffset    = Random.insideUnitCircle * shakeMagnitudeCurrent;
            shakeTimeLeft -= Time.unscaledDeltaTime;
            transform.position += (Vector3)shakeOffset;
        }
        else
        {
            shakeOffset = Vector2.zero;
        }
    }

    /// <summary>
    /// Strict camera clamp. Ensures the camera's view frustum does not cross the boundaries.
    /// Uses MovementBounds in real-time to respect any dynamic Sprite scaling via FitSpriteToScreen.
    /// </summary>
    private Vector3 ClampToBounds(Vector3 pos)
    {
        Vector2 min = _boundsMin;
        Vector2 max = _boundsMax;
        
        if (MovementBounds.Shared != null)
            MovementBounds.Shared.GetCameraBoundsMinMax(out min, out max);

        float halfH   = cam.orthographicSize;
        float halfW   = halfH * cam.aspect;
        float centerX = (min.x + max.x) * 0.5f;
        float centerY = (min.y + max.y) * 0.5f;
        float rangeX  = (max.x - min.x) * 0.5f - halfW;
        float rangeY  = (max.y - min.y) * 0.5f - halfH;

        // "영역 내에 가둔다" - 하늘색 영역을 넘어가지 못하게 방지
        // 만약 지정한 하늘색 영역이 현재 카메라 해상도보다 작다면, 정중앙에 고정.
        pos.x = rangeX > 0f ? Mathf.Clamp(pos.x, centerX - rangeX, centerX + rangeX) : centerX;
        pos.y = rangeY > 0f ? Mathf.Clamp(pos.y, centerY - rangeY, centerY + rangeY) : centerY;
        return pos;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!_useBounds) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Vector3 center = new((_boundsMin.x + _boundsMax.x) * 0.5f, (_boundsMin.y + _boundsMax.y) * 0.5f, 0f);
        Vector3 size   = new(_boundsMax.x - _boundsMin.x, _boundsMax.y - _boundsMin.y, 0f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
