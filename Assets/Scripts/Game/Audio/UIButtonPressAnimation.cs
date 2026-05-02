using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 버튼에 붙이면 누를 때 스케일이 줄어들고, 떼면 튕기며 원래 크기로 돌아옵니다.
/// UIButtonPressAutoAttach가 자동으로 붙여줍니다.
/// </summary>
public class UIButtonPressAnimation : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Range(0.5f, 1f)]  public float pressScale    = 0.88f;
    [Range(1f, 1.3f)]  public float bounceScale   = 1.06f;
    [Range(0.02f, 0.2f)] public float pressDuration   = 0.07f;
    [Range(0.05f, 0.4f)] public float releaseDuration = 0.18f;

    Vector3    _originScale;
    Coroutine  _co;
    bool       _isDown;

    void Awake() => _originScale = transform.localScale;

    public void OnPointerDown(PointerEventData _)
    {
        _isDown = true;
        Run(ScaleTo(_originScale * pressScale, pressDuration, ease: false));
    }

    public void OnPointerUp(PointerEventData _)
    {
        if (!_isDown) return;
        _isDown = false;
        Run(BounceBack());
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!_isDown) return;
        _isDown = false;
        Run(ScaleTo(_originScale, pressDuration, ease: true));
    }

    void Run(IEnumerator routine)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(routine);
    }

    IEnumerator ScaleTo(Vector3 target, float duration, bool ease)
    {
        Vector3 from = transform.localScale;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = t / duration;
            if (ease) p = 1f - Mathf.Pow(1f - p, 2f);
            transform.localScale = Vector3.LerpUnclamped(from, target, p);
            yield return null;
        }
        transform.localScale = target;
    }

    IEnumerator BounceBack()
    {
        Vector3 from   = transform.localScale;
        Vector3 peak   = _originScale * bounceScale;
        Vector3 target = _originScale;
        float   half   = releaseDuration * 0.45f;
        float   rest   = releaseDuration - half;

        // 올라가기
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(from, peak, t / half);
            yield return null;
        }
        // 내려오기 (ease-out)
        for (float t = 0f; t < rest; t += Time.unscaledDeltaTime)
        {
            float p = 1f - Mathf.Pow(1f - t / rest, 2f);
            transform.localScale = Vector3.LerpUnclamped(peak, target, p);
            yield return null;
        }
        transform.localScale = target;
        _co = null;
    }

    void OnDisable()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        transform.localScale = _originScale;
        _isDown = false;
    }
}
