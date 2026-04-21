using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팝업 패널을 스케일 애니메이션으로 열고 닫는 재사용 가능한 컴포넌트.
/// </summary>
public class ScalablePopup : MonoBehaviour
{
    [Header("닫기 버튼")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundButton;

    [Header("애니메이션 설정")]
    [SerializeField] private float showDuration = 0.25f;
    [SerializeField] private float hideDuration = 0.2f;

    private RectTransform _rectTransform;
    private CanvasGroup   _canvasGroup;
    private Coroutine     _animationCoroutine;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup   = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        closeButton?.onClick.AddListener(Hide);
        backgroundButton?.onClick.AddListener(Hide);

        _rectTransform.localScale    = Vector3.zero;
        _canvasGroup.alpha           = 0f;
        _canvasGroup.interactable    = false;
        _canvasGroup.blocksRaycasts  = false;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(Animate(true));
    }

    public void Hide()
    {
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(Animate(false));
    }

    private IEnumerator Animate(bool show)
    {
        float duration   = show ? showDuration : hideDuration;
        float startScale = show ? 0f : 1f;
        float endScale   = show ? 1f : 0f;

        // Show: 애니메이션 시작 즉시 raycast 차단 (클릭 통과 방지)
        // Hide: 애니메이션 완료 후에 raycast 해제
        _canvasGroup.blocksRaycasts = show;
        _canvasGroup.interactable   = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.SmoothStep(startScale, endScale, t);
            _rectTransform.localScale = Vector3.one * s;
            _canvasGroup.alpha        = s;
            yield return null;
        }

        _rectTransform.localScale = Vector3.one * endScale;
        _canvasGroup.alpha        = endScale;

        if (show)
        {
            _canvasGroup.interactable = true;
        }
        else
        {
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        _animationCoroutine = null;
    }
}