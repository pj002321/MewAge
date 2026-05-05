using UnityEngine;

/// <summary>
/// Y좌표 기반 스프라이트 렌더링 순서 정렬.
/// 하단(낮은 Y) → 앞, 상단(높은 Y) → 뒤.
/// 적·플레이어·보스 프리팹 루트에 붙이세요.
/// 자식 SpriteRenderer들의 초기 상대 offset은 유지됩니다.
/// </summary>
public class SpriteYSort : MonoBehaviour
{
    [Tooltip("Y 1유닛 당 sorting order 변화량. 클수록 정밀도 증가.")]
    [SerializeField] private float multiplier = 100f;

    private SpriteRenderer[] _renderers;
    private int[]             _offsets;   // Awake 시 캡처한 각 SR의 상대 order

    private void Awake()
    {
        Refresh();
    }

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
