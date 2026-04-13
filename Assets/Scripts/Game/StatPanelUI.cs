using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("획득 증강 목록")]
    [SerializeField] private GameObject improvIconPrefab;
    [SerializeField] private Transform  improvContents;

    private StatFieldUI[] _fields;
    private readonly List<GameObject> _improvIcons = new();

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

        RefreshImprovIcons();
    }

    private void RefreshImprovIcons()
    {
        if (improvIconPrefab == null || improvContents == null) return;

        var mgr      = ImprovManager.Shared;
        var acquired = mgr?.Acquired;

        // 아이콘 재사용 또는 생성
        int idx = 0;
        if (acquired != null)
        {
            foreach (var kv in acquired)
            {
                var (data, count) = kv.Value;

                // 아이콘 오브젝트 확보
                GameObject icon;
                if (idx < _improvIcons.Count)
                {
                    icon = _improvIcons[idx];
                    icon.SetActive(true);
                }
                else
                {
                    icon = Instantiate(improvIconPrefab, improvContents);
                    _improvIcons.Add(icon);
                }

                // Image 설정
                var img = icon.GetComponentInChildren<Image>(true);
                if (img != null)
                {
                    string augId = data.f_증강ID.ToString();
                    Sprite sprite = ImprovVisualConfig.Shared?.GetSprite(augId)
                                 ?? Resources.Load<Sprite>("AUG/" + augId.ToLower());
                    img.sprite  = sprite;
                    img.enabled = sprite != null;
                }

                // CountText 설정
                var tmp = icon.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.text = count > 1 ? $"×{count}" : "";

                idx++;
            }
        }

        // 남은 아이콘 비활성화
        for (int i = idx; i < _improvIcons.Count; i++)
            _improvIcons[i].SetActive(false);
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
