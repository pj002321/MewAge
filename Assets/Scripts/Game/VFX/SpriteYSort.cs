using UnityEngine;

/// <summary>
/// Y좌표 기반 스프라이트 렌더링 순서 정렬.
/// 하단(낮은 Y) → 앞(높은 order), 상단(높은 Y) → 뒤(낮은 order).
///
/// 플레이어·몬스터·보스가 같은 Sorting Layer에 있어야 서로 간 앞뒤가 올바르게 적용됩니다.
/// Sorting Layer는 물리 Layer(충돌 마스크)와 완전히 별개이므로
/// 충돌 설정에 영향 없이 렌더링 레이어만 통일할 수 있습니다.
/// </summary>
public class SpriteYSort : MonoBehaviour
{
    [Tooltip("Y 1유닛 당 sorting order 변화량. 클수록 정밀도 증가.")]
    [SerializeField] private float multiplier = 100f;

    private SpriteRenderer[] _renderers;
    private int[]             _offsets;

    private void Awake() => Refresh();

    /// <summary>동적으로 자식 SR 구성이 바뀌었을 때 호출 (무기 장착 등)</summary>
    public void Refresh()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _offsets   = new int[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _offsets[i] = _renderers[i].sortingOrder;
    }

    private void LateUpdate()
    {
        int yBase = -Mathf.RoundToInt(transform.position.y * multiplier);
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _renderers[i].sortingOrder = yBase + _offsets[i];
    }
}
