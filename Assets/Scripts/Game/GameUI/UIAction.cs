using UnityEngine;

/// <summary>
/// UI 오브젝트에 간단한 반복 애니메이션을 부여하는 컴포넌트.
/// 하나의 오브젝트에 여러 개를 붙여 효과를 조합할 수 있습니다.
/// </summary>
public class UIAction : MonoBehaviour
{
    public enum ActionType
    {
        Float,   // 위아래 두둥실
        Pulse,   // 크기 맥박
        Rotate,  // 연속 회전
        Fade,    // 알파 페이드 인아웃
    }

    [SerializeField] private ActionType type      = ActionType.Float;
    [SerializeField] private float      speed     = 1f;    // 사이클/초
    [SerializeField] private float      amplitude = 10f;   // 픽셀(Float) / 도/초(Rotate) / 알파 진폭(Fade)
    [SerializeField] private float      phase     = 0f;    // 시작 위상 (0~1)

    [Header("Pulse 전용 (원본 스케일 기준 배율)")]
    [SerializeField] private float pulseMinScale = 0.9f;
    [SerializeField] private float pulseMaxScale = 1.1f;

    private RectTransform  _rt;
    private CanvasGroup    _cg;
    private Vector2        _originPos;
    private Vector3        _originScale;
    private float          _time;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        CacheOrigin();
        _time = phase / Mathf.Max(speed, 0.001f);
    }

    private void OnDisable()
    {
        Restore();
    }

    // ── 업데이트 ──────────────────────────────────────────────────────

    private void Update()
    {
        _time += Time.unscaledDeltaTime;
        float s = Mathf.Sin(_time * speed * Mathf.PI * 2f);

        switch (type)
        {
            case ActionType.Float:
                _rt.anchoredPosition = _originPos + Vector2.up * (s * amplitude);
                break;

            case ActionType.Pulse:
                float t = (s + 1f) * 0.5f; // sin -1~1 → 0~1
                _rt.localScale = _originScale * Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
                break;

            case ActionType.Rotate:
                _rt.Rotate(0f, 0f, amplitude * Time.unscaledDeltaTime * Mathf.Sign(speed));
                break;

            case ActionType.Fade:
                if (_cg != null)
                    _cg.alpha = Mathf.Lerp(0.5f - amplitude * 0.5f, 1f,
                                           (s + 1f) * 0.5f);
                break;
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private void CacheOrigin()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        _originPos   = _rt.anchoredPosition;
        _originScale = _rt.localScale;

        if (type == ActionType.Fade)
            _cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    private void Restore()
    {
        if (_rt == null) return;
        if (type == ActionType.Float)  _rt.anchoredPosition = _originPos;
        if (type == ActionType.Pulse)  _rt.localScale       = _originScale;
    }
}
