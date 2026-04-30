using UnityEngine;

/// <summary>
/// VisualRoot에 부착 — 자기 자신(VisualRoot)을 스케일+위치 조작해 슬라임 바운스를 표현합니다.
/// fixBottom=true 이면 스프라이트 하단이 월드 공간에서 고정됩니다.
/// 자식 SpriteRenderer의 위치·스케일·피벗에 무관하게 정확하게 동작합니다.
/// PlayerController의 X scale 부호(flip)와 충돌하지 않습니다.
/// </summary>
public class SlimeBobAnimation : MonoBehaviour
{
    [Header("스케일 범위")]
    [SerializeField] private float minScaleY = 0.78f;
    [SerializeField] private float maxScaleY = 1.12f;
    [SerializeField] [Range(0f, 1f)]
    private float squashStretchX = 0.5f;

    [Header("속도")]
    [SerializeField] private float frequency   = 2f;
    [SerializeField] private float randomPhase = 1f;

    [Header("바닥 고정")]
    [SerializeField] private bool  fixBottom         = false;
    [SerializeField] private float halfHeightOverride = 0f;   // 0이면 자식 SpriteRenderer 자동 감지

    private float   _origLocalScaleAbsX;
    private float   _origLocalScaleY;
    private float   _origLocalScaleZ;
    private Vector3 _origLocalPos;
    private float   _phase;

    // VisualRoot 자신의 로컬 공간에서 스프라이트 바닥의 Y 오프셋
    // (자식 localPosition + 피벗 오프셋까지 모두 반영)
    private float _spriteBottomLocal;

    private SpriteRenderer _renderer;

    private void Awake()
    {
        _origLocalPos       = transform.localPosition;
        _origLocalScaleAbsX = Mathf.Abs(transform.localScale.x);
        _origLocalScaleY    = transform.localScale.y;
        _origLocalScaleZ    = transform.localScale.z;
        _phase              = Random.Range(0f, randomPhase);

        TryInitRenderer();
    }

    private void Start()
    {
        // 모델이 Awake 이후에 교체될 수 있으므로(SwapPlayerModel 등) Start에서 재시도
        if (_renderer == null)
            TryInitRenderer();
    }

    /// <summary>모델 교체 후 PlayerCharacterManager.SwapPlayerModel에서 호출</summary>
    public void Reinitialize()
    {
        TryInitRenderer();
    }

    private void TryInitRenderer()
    {
        _renderer = GetComponentInChildren<SpriteRenderer>();

        if (halfHeightOverride > 0f)
        {
            // 수동 지정: 스프라이트 중심이 VisualRoot 원점에 있다고 가정
            _spriteBottomLocal = -halfHeightOverride;
        }
        else if (_renderer != null && _renderer.sprite != null)
        {
            // 스프라이트 바닥(pivot 기준 bounds.min.y)을 월드로 변환 후
            // VisualRoot 로컬 공간으로 역변환 → 중간 계층·피벗·localPos 모두 정확 반영
            Vector3 worldBot = _renderer.transform.TransformPoint(
                new Vector3(0f, _renderer.sprite.bounds.min.y, 0f));
            _spriteBottomLocal = transform.InverseTransformPoint(worldBot).y;
        }
        else
        {
            _spriteBottomLocal = -0.5f;
        }
    }

    private void Update()
    {
        if (_renderer != null && !_renderer.isVisible) return;

        float t      = (Mathf.Sin((Time.time * frequency + _phase) * Mathf.PI * 2f) + 1f) * 0.5f;
        float scaleY = Mathf.Lerp(minScaleY, maxScaleY, t);
        float scaleX = 1f + (1f - scaleY) * squashStretchX;

        // flip 부호 유지 (PlayerController가 X scale 부호로 flip 제어)
        float flipSign = transform.localScale.x >= 0f ? 1f : -1f;

        transform.localScale = new Vector3(
            _origLocalScaleAbsX * scaleX * flipSign,
            _origLocalScaleY * scaleY,
            _origLocalScaleZ);

        if (fixBottom)
        {
            // parent 공간에서 스프라이트 바닥 Y를 고정:
            //   원래 바닥 = _origLocalPos.y + _spriteBottomLocal * _origLocalScaleY
            //   현재 바닥 = newPos.y       + _spriteBottomLocal * _origLocalScaleY * scaleY
            //   → newPos.y = _origLocalPos.y + _spriteBottomLocal * _origLocalScaleY * (1 - scaleY)
            float newY = _origLocalPos.y
                       + _spriteBottomLocal * _origLocalScaleY * (1f - scaleY);
            transform.localPosition = new Vector3(_origLocalPos.x, newY, _origLocalPos.z);
        }
    }
}
