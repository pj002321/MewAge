using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ScrollRect에 붙이면 드래그 끝낼 때 가장 가까운 자식 카드로 스냅합니다.
/// cardWidth를 Inspector에서 직접 지정하거나, 0이면 런타임에 첫 번째 자식 너비를 사용합니다.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class SnapScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Tooltip("카드 1장 너비(px). 0이면 첫 번째 자식 너비를 자동 사용.")]
    [SerializeField] private float cardWidth = 0f;

    [SerializeField] private float snapDuration = 0.2f;

    private ScrollRect    _scroll;
    private RectTransform _content;
    private int           _currentIndex;
    private float         _dragStartX;   // 드래그 시작 시 content.anchoredPosition.x
    private bool          _isDragging;
    private Coroutine     _snapRoutine;

    void Awake()
    {
        _scroll  = GetComponent<ScrollRect>();
        _content = _scroll.content;
    }

    IEnumerator Start()
    {
        // 레이아웃 확정 후 초기 위치 맞춤
        yield return null;
        Canvas.ForceUpdateCanvases();
        SetImmediate(0);
    }

    public void OnBeginDrag(PointerEventData _)
    {
        _isDragging = true;
        if (_snapRoutine != null) { StopCoroutine(_snapRoutine); _snapRoutine = null; }
        _dragStartX = _content.anchoredPosition.x;
    }

    public void OnEndDrag(PointerEventData _)
    {
        _isDragging = false;

        float w     = GetCardWidth();
        float moved = _content.anchoredPosition.x - _dragStartX;

        // 1/4 카드 너비 이상 드래그했으면 방향으로 이동
        if (moved < -w * 0.25f)
            _currentIndex = Mathf.Min(_currentIndex + 1, _content.childCount - 1);
        else if (moved > w * 0.25f)
            _currentIndex = Mathf.Max(_currentIndex - 1, 0);
        // 그 이하면 제자리 스냅

        _snapRoutine = StartCoroutine(SnapTo(_currentIndex));
    }

    // ── 외부 API ──────────────────────────────────────────────────────

    public void GoTo(int index, bool animate = true)
    {
        _currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _content.childCount - 1));
        if (animate) _snapRoutine = StartCoroutine(SnapTo(_currentIndex));
        else         SetImmediate(_currentIndex);
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    float GetCardWidth()
    {
        if (cardWidth > 1f) return cardWidth;

        // 첫 번째 자식 실제 너비
        if (_content != null && _content.childCount > 0)
        {
            var rt = _content.GetChild(0) as RectTransform;
            if (rt != null && rt.rect.width > 1f) return rt.rect.width;
        }

        // Viewport 너비 fallback
        float vw = _scroll.viewport.rect.width;
        return vw > 1f ? vw : 300f;
    }

    void SetImmediate(int index)
    {
        float w = GetCardWidth();
        _content.anchoredPosition = new Vector2(-index * w, _content.anchoredPosition.y);
    }

    IEnumerator SnapTo(int index)
    {
        float w       = GetCardWidth();
        float targetX = -index * w;
        float startX  = _content.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            if (_isDragging) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / snapDuration);
            _content.anchoredPosition = new Vector2(
                Mathf.Lerp(startX, targetX, t),
                _content.anchoredPosition.y);
            yield return null;
        }
        _content.anchoredPosition = new Vector2(targetX, _content.anchoredPosition.y);
        _snapRoutine = null;
    }
}
