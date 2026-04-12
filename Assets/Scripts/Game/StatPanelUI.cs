using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 능력치 팝업 패널.
/// 자식 오브젝트에서 StatFieldUI 컴포넌트를 자동 수집하여 Refresh 호출합니다.
///
/// Inspector 연결:
///   closeButton  — 닫기 버튼
///   activeButton — 패널 열기/닫기 토글 버튼
/// </summary>
public class StatPanelUI : MonoBehaviourSingleton<StatPanelUI>
{
    protected override bool DontDestroy => false;

    [SerializeField] private Button closeButton;
    [SerializeField] private Button activeButton;

    [Header("스케일 애니메이션")]
    [SerializeField] private float openDuration  = 0.18f;
    [SerializeField] private float closeDuration = 0.12f;

    private StatFieldUI[] _fields;

    private bool     _hasPreview;
    private StatType _previewStat;
    private float    _previewPct;

    private Coroutine _scaleCoroutine;
    private Vector3   _originalScale;

    protected override void Awake()
    {
        base.Awake();
        _originalScale = transform.localScale;

        closeButton ?.onClick.AddListener(Hide);
        activeButton?.onClick.AddListener(Show);

        foreach (var anim in GetComponentsInChildren<Animator>(includeInactive: true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        _fields = GetComponentsInChildren<StatFieldUI>(includeInactive: true);

        gameObject.SetActive(false);
    }

    public void Show()
    {
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);
        RefreshInternal();
        _scaleCoroutine = StartCoroutine(ScaleRoutine(Vector3.zero, _originalScale, openDuration, easeOut: true));
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleRoutine(transform.localScale, Vector3.zero, closeDuration, easeOut: false, deactivateOnEnd: true));
    }

    public void Refresh()
    {
        if (!gameObject.activeSelf) return;
        RefreshInternal();
    }

    /// <summary>증강 미리보기를 설정합니다. 패널이 열려 있으면 즉시 갱신됩니다.</summary>
    public void SetPreview(StatType stat, float pct)
    {
        _previewStat = stat;
        _previewPct  = pct;
        _hasPreview  = true;
        if (gameObject.activeSelf) RefreshInternal();
    }

    /// <summary>미리보기를 해제하고 일반 수치로 복귀합니다.</summary>
    public void ClearPreview()
    {
        _hasPreview = false;
        if (gameObject.activeSelf) RefreshInternal();
    }

    private void RefreshInternal()
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return;

        foreach (var field in _fields)
        {
            if (_hasPreview)
                field.RefreshWithPreview(sm, _previewStat, _previewPct);
            else
                field.Refresh(sm);
        }
    }

    private IEnumerator ScaleRoutine(Vector3 from, Vector3 to, float duration,
                                     bool easeOut, bool deactivateOnEnd = false)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = easeOut ? 1f - (1f - t) * (1f - t)  // ease-out
                                 : t * t;                      // ease-in
            transform.localScale = Vector3.LerpUnclamped(from, to, ease);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        transform.localScale = to;
        if (deactivateOnEnd) gameObject.SetActive(false);
        _scaleCoroutine = null;
    }
}
