using UnityEngine;

/// <summary>
/// 캐릭터 루트에 부착 — 스프라이트를 슬라임처럼 납작했다 펴는 바운스 애니메이션.
/// 스프라이트의 바닥(하단) 위치는 항상 고정됩니다.
/// 피벗이 Center 기준 스프라이트에 맞춰 설계되었습니다.
///
/// ※ 바닥 고정은 SpriteRenderer가 자식 오브젝트에 있을 때만 동작합니다.
///    SpriteRenderer가 루트에 있는 경우(적 등) 스쿼시/스트레치만 적용됩니다.
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

    [Header("대상 스프라이트 (비워두면 자식 SpriteRenderer 자동 탐색)")]
    [SerializeField] private Transform spriteTarget;

    private Vector3 _origLocalPos;
    private Vector3 _origLocalScale;
    private float   _halfHeight;
    private float   _phase;
    private bool    _fixBottom; // spriteTarget이 진짜 자식일 때만 위치 보정

    private void Awake()
    {
        if (spriteTarget == null)
        {
            // 우선 자신의 SpriteRenderer 확인
            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null)
            {
                spriteTarget = transform;
                _fixBottom   = false;  // 루트이면 위치 보정 불가
            }
            else
            {
                // 자신의 스프라이트가 없으면 자식 탐색
                SpriteRenderer found = null;
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.transform != transform) { found = sr; break; }
                }

                if (found != null)
                {
                    spriteTarget = found.transform;
                    _fixBottom   = true;   // 자식이면 위치 보정 가능
                }
                else
                {
                    // 스프라이트가 없음 (오류)
                    spriteTarget = transform;
                    _fixBottom   = false;
                }
            }
        }
        else
        {
            _fixBottom = (spriteTarget != transform);
        }

        _origLocalPos   = spriteTarget.localPosition;
        _origLocalScale = spriteTarget.localScale;
        _phase          = Random.Range(0f, randomPhase);

        var renderer = spriteTarget.GetComponent<SpriteRenderer>();
        _halfHeight = (renderer != null && renderer.sprite != null)
            ? renderer.sprite.bounds.extents.y
            : 0.5f;
    }

    private void Update()
    {
        float t      = (Mathf.Sin((Time.time * frequency + _phase) * Mathf.PI * 2f) + 1f) * 0.5f;
        float scaleY = Mathf.Lerp(minScaleY, maxScaleY, t);
        float scaleX = 1f + (1f - scaleY) * squashStretchX;

        spriteTarget.localScale = new Vector3(
            _origLocalScale.x * scaleX,
            _origLocalScale.y * scaleY,
            _origLocalScale.z);

        // 바닥 고정 — 자식 오브젝트일 때만 (루트면 이동 시스템과 충돌)
        if (_fixBottom)
        {
            float bottomOffset = _halfHeight * _origLocalScale.y * (1f - scaleY);
            spriteTarget.localPosition = new Vector3(
                _origLocalPos.x,
                _origLocalPos.y - bottomOffset,
                _origLocalPos.z);
        }
    }
}
