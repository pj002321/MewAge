using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영웅·장비·룬·펫 통합 패널.
///
/// 계층 구조:
///   Panel_영웅정보   (이 컴포넌트)
///     영웅배경        CanvasGroup — Tab[0] 전용 배경
///     장비배경        CanvasGroup — Tab[1] 전용 배경
///     Scroll (ScrollRect — Inspector 연결)
///       Contents-영웅  Tab[0] 콘텐츠
///       Contents-장비  Tab[1] 콘텐츠
///       Contents-룬    Tab[2] 콘텐츠
///       Contents-펫    Tab[3] 콘텐츠
///       TabBar
///         Tab_영웅 / Tab_장비 / Tab_룬 / Tab_펫
///
/// 탭 전환 시:
///   - 배경(CanvasGroup.alpha) → ease-out 크로스페이드
///   - 콘텐츠(SetActive)       → 즉시 전환
///   - ScrollRect.content      → 활성 탭 RectTransform으로 교체 + 스크롤 상단 리셋
/// </summary>
public class HomeCombinedPanel : MonoBehaviour
{
    [System.Serializable]
    public class TabEntry
    {
        [Tooltip("Btn-영웅 등 실제 Button 컴포넌트")]
        public Button button;

        [Tooltip("영웅배경 등 전용 배경의 CanvasGroup. 전용 배경 없으면 None.")]
        public CanvasGroup background;

        [Tooltip("Contents-영웅 등 스크롤 콘텐츠 GameObject")]
        public GameObject content;
    }

    // ──────────────────────────────────────────────────────────────────
    [Header("탭 목록 (영웅·장비·룬·펫 순서로 등록)")]
    public TabEntry[] tabs;

    [Header("탭 활성/비활성 이미지")]
    public Sprite tabActiveSprite;
    public Sprite tabInactiveSprite;

    [Header("페이드 시간 (초)")]
    public float fadeDuration = 0.22f;

    [Header("스크롤 (탭 전환 시 Content 자동 교체)")]
    [SerializeField] private ScrollRect scrollRect;

    // ──────────────────────────────────────────────────────────────────
    int       _currentTab = -1;
    Coroutine _fadeRoutine;

    // ──────────────────────────────────────────────────────────────────
    // Awake: 버튼 리스너 등록 + 초기 탭 상태를 OnEnable 전에 확정
    void Awake()
    {
        if (tabs == null) return;
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            tabs[i].button?.onClick.AddListener(() => ShowTab(idx));
        }
        ShowTab(0, instant: true);
    }

    // Start: 캐릭터 드롭인은 다른 싱글톤 초기화 이후이므로 1프레임 뒤 실행
    void Start()
    {
        StartCoroutine(DropInNextFrame());
    }

    void OnEnable()
    {
        if (_currentTab < 0) return;
        // 재활성화 시 현재 탭 배경·콘텐츠를 즉시 복원
        ApplyInstant(_currentTab);
        if (_currentTab == 0) StartCoroutine(DropInNextFrame());
    }

    IEnumerator DropInNextFrame()
    {
        yield return null;
        TriggerMemberDropIn();
    }

    // ──────────────────────────────────────────────────────────────────
    /// <summary>외부에서도 호출 가능.</summary>
    public void ShowTab(int index, bool instant = false)
    {
        if (tabs == null || index < 0 || index >= tabs.Length) return;
        if (index == _currentTab) return;

        int prev   = _currentTab;
        _currentTab = index;

        RefreshSprites(index);

        // 콘텐츠 즉시 전환 + ScrollRect Content 교체
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].content?.SetActive(i == index);

        SyncScrollContent(index);

        // 영웅 탭으로 전환 시 캐릭터 드롭인
        if (index == 0) TriggerMemberDropIn();

        // 배경 페이드
        var fromBg = (prev >= 0) ? tabs[prev].background : null;
        var toBg   = tabs[index].background;

        if (instant || fromBg == toBg)
        {
            SetBgAlpha(fromBg, 0f);
            SetBgAlpha(toBg,   1f);
            return;
        }

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(CrossFade(fromBg, toBg, fadeDuration));
    }

    // ──────────────────────────────────────────────────────────────────
    void ApplyInstant(int index)
    {
        if (tabs == null) return;
        RefreshSprites(index);

        for (int i = 0; i < tabs.Length; i++)
            tabs[i].content?.SetActive(i == index);

        SyncScrollContent(index);

        // 모든 배경 숨긴 뒤 현재 탭 배경만 표시
        var seen = new System.Collections.Generic.HashSet<CanvasGroup>();
        foreach (var t in tabs)
        {
            if (t.background == null || !seen.Add(t.background)) continue;
            SetBgAlpha(t.background, 0f);
        }
        SetBgAlpha(tabs[index].background, 1f);
    }

    void SyncScrollContent(int index)
    {
        if (scrollRect == null || tabs == null || index >= tabs.Length) return;
        var content = tabs[index].content;
        if (content == null) return;
        scrollRect.content = content.GetComponent<RectTransform>();
        scrollRect.verticalNormalizedPosition = 1f;   // 스크롤 상단 리셋
    }

    // ──────────────────────────────────────────────────────────────────
    void RefreshSprites(int active)
    {
        if (tabs == null) return;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].button == null) continue;
            var img = tabs[i].button.GetComponent<Image>();
            if (img == null) continue;
            var sprite = (i == active) ? tabActiveSprite : tabInactiveSprite;
            if (sprite != null) img.sprite = sprite;
        }
    }

    static void SetBgAlpha(CanvasGroup bg, float alpha)
    {
        if (bg == null) return;
        bg.alpha = alpha;
        bg.gameObject.SetActive(alpha > 0f);
    }

    static void TriggerMemberDropIn()
    {
        var charId = PlayerCharacterManager.Shared?.EquippedCharID;
        if (string.IsNullOrEmpty(charId)) return;
        HomeCharacterDisplay.Shared?.ChangeCharacter(charId);
        HomeCharacterDisplay.Shared?.DropIn();
    }

    // ──────────────────────────────────────────────────────────────────
    /// <summary>ease-out quad 크로스페이드 (배경 alpha 전용).</summary>
    IEnumerator CrossFade(CanvasGroup from, CanvasGroup to, float duration)
    {
        // 들어올 배경을 alpha=0 으로 활성화
        if (to != null) { to.gameObject.SetActive(true); to.alpha = 0f; }

        float startFrom = from != null ? from.alpha : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - (1f - t) * (1f - t); // ease-out quad

            if (from != null) from.alpha = Mathf.Lerp(startFrom, 0f, ease);
            if (to   != null) to.alpha   = ease;
            yield return null;
        }

        // 나간 배경 비활성화
        if (from != null) { from.alpha = 0f; from.gameObject.SetActive(false); }
        if (to   != null) to.alpha = 1f;
    }
}
