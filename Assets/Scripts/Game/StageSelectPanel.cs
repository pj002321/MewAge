using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 씬 전투 패널 — 스테이지 선택 캐러셀.
/// stages 배열에 데이터를 채우면 카드·도트를 자동 생성합니다.
/// </summary>
public class StageSelectPanel : MonoBehaviour
{
    // ── 데이터 ──────────────────────────────────────────────────────
    [System.Serializable]
    public class StageEntry
    {
        public string stageName  = "스테이지";
        public Sprite icon;
        public int    bestWave   = 0;   // 최고 생존 웨이브
    }

    [Header("스테이지 데이터 (Inspector에서 추가/편집)")]
    public StageEntry[] stages = new StageEntry[]
    {
        new StageEntry { stageName = "야생 거리" },
        new StageEntry { stageName = "어둠의 숲" },
        new StageEntry { stageName = "폐허 도시" },
    };

    // ── UI 참조 (Builder가 자동 연결) ────────────────────────────────
    [Header("카드 프리팹 (연결하면 이걸 사용, 비우면 자동 생성)")]
    public StageCardView cardPrefab;

    [Header("스크롤")]
    public ScrollRect    scrollRect;
    public RectTransform content;

    [Header("도트 컨테이너 (DotRow)")]
    public RectTransform dotContainer;

    [Header("버튼")]
    public Button startButton;

    // ── 런타임 생성 ──────────────────────────────────────────────────
    [Header("도트 색상")]
    public Color dotActive   = Color.white;
    public Color dotInactive = new Color(1f, 1f, 1f, 0.3f);

    StageCardView[] cardViews;
    Image[]         dots;
    int             currentIndex;
    bool            isSnapping;

    // ── 카드 스타일 (Inspector에서 조정 가능) ─────────────────────
    [Header("카드 크기 (기준 해상도 기준)")]
    public float cardWidth  = 1080f;
    public float cardHeight = 800f;

    float cachedCardW = -1f;

    void Start()
    {
        BuildCards();
        BuildDots();

        startButton?.onClick.AddListener(OnStartClicked);

        if (scrollRect != null)
        {
            var trigger = scrollRect.gameObject.AddComponent<EventTrigger>();
            var entry   = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            entry.callback.AddListener(_ => SnapToNearest());
            trigger.triggers.Add(entry);
        }

        // 한 프레임 후 레이아웃 확정 → 카드 너비 캐시 후 초기 위치 설정
        StartCoroutine(InitAfterLayout());
    }

    IEnumerator InitAfterLayout()
    {
        yield return null;   // 1프레임 대기 (레이아웃 계산 완료)
        Canvas.ForceUpdateCanvases();

        // viewport 실제 너비로 카드 너비 맞춤
        if (scrollRect != null && cardViews != null)
        {
            float vpW = scrollRect.viewport.rect.width;
            if (vpW > 1f)
            {
                foreach (var cv in cardViews)
                {
                    if (cv == null) continue;
                    var rt = cv.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(vpW, rt.sizeDelta.y);
                    var le = cv.GetComponent<LayoutElement>();
                    if (le != null) { le.preferredWidth = vpW; le.minWidth = vpW; }
                }
                Canvas.ForceUpdateCanvases();
            }
        }

        cachedCardW = -1f;   // 캐시 리셋 후 카드 실제 너비 재측정
        SnapImmediate(0);
    }

    // ── 카드 동적 생성 ───────────────────────────────────────────────

    void BuildCards()
    {
        if (content == null) return;

        // 기존 자식 제거
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        cardViews = new StageCardView[stages.Length];

        for (int i = 0; i < stages.Length; i++)
        {
            StageCardView cv;

            if (cardPrefab != null)
            {
                // ── 프리팹 사용 ──────────────────────────────────────
                cv = Instantiate(cardPrefab, content);
                var le        = cv.GetComponent<LayoutElement>();
                if (le == null) le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = cardWidth;
                le.minWidth       = cardWidth;
                cv.Bind(stages[i]);
                cardViews[i] = cv;
                continue;   // 아래 자동 생성 코드 건너뜀
            }

            // ── 자동 생성 ────────────────────────────────────────────
            var card   = new GameObject($"Card_{i}");
            card.transform.SetParent(content, false);

            var cardRT = card.AddComponent<RectTransform>();
            cardRT.sizeDelta = new Vector2(cardWidth, cardHeight);

            var le2            = card.AddComponent<LayoutElement>();
            le2.preferredWidth = cardWidth;
            le2.minWidth       = cardWidth;

            // 아이콘 (카드 정중앙)
            var iconRT      = MakeRT("StageIcon", cardRT);
            iconRT.anchorMin = new Vector2(0.15f, 0.2f);
            iconRT.anchorMax = new Vector2(0.85f, 0.88f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            var iconImg         = iconRT.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.color          = Color.white;
            iconImg.enabled        = false;   // 스프라이트 연결 시 활성화

            // 스테이지 이름 (아이콘 바로 위)
            var nameRT  = MakeTMP("StageName", cardRT, stages[i].stageName, 44f);
            nameRT.anchorMin = new Vector2(0f, 0.88f);
            nameRT.anchorMax = new Vector2(1f, 1.00f);
            nameRT.offsetMin = new Vector2(16f, 0f);
            nameRT.offsetMax = new Vector2(-16f, 0f);
            var nameTmp      = nameRT.GetComponent<TextMeshProUGUI>();
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color     = Color.white;

            // 최고 생존 웨이브 (아이콘 아래)
            string waveStr = stages[i].bestWave > 0 ? $"최고 웨이브: {stages[i].bestWave}" : "최고 웨이브: -";
            var waveRT  = MakeTMP("BestWave", cardRT, waveStr, 32f);
            waveRT.anchorMin = new Vector2(0f, 0.06f);
            waveRT.anchorMax = new Vector2(1f, 0.20f);
            waveRT.offsetMin = new Vector2(16f, 0f);
            waveRT.offsetMax = new Vector2(-16f, 0f);
            waveRT.GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.8f, 0.8f, 1f);

            cv               = card.AddComponent<StageCardView>();
            cv.iconImage     = iconImg;
            cv.stageNameText = nameTmp;
            cv.bestWaveText  = waveRT.GetComponent<TextMeshProUGUI>();
            cv.Bind(stages[i]);

            cardViews[i] = cv;
        }
    }

    void BuildDots()
    {
        if (dotContainer == null) return;

        for (int i = dotContainer.childCount - 1; i >= 0; i--)
            Destroy(dotContainer.GetChild(i).gameObject);

        dots = new Image[stages.Length];
        for (int i = 0; i < stages.Length; i++)
        {
            var go  = new GameObject($"Dot_{i}");
            go.transform.SetParent(dotContainer, false);
            var le  = go.AddComponent<LayoutElement>();
            le.preferredWidth  = 16f;
            le.preferredHeight = 16f;
            dots[i] = go.AddComponent<Image>();
            dots[i].color = i == 0 ? dotActive : dotInactive;
        }
    }

    // ── 네비게이션 ───────────────────────────────────────────────────

    public void GoTo(int index, bool animate = true)
    {
        currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, stages.Length - 1));
        RefreshDots();

        if (animate) StartCoroutine(SnapCoroutine(currentIndex));
        else         SnapImmediate(currentIndex);
    }

    float CardW()
    {
        if (cachedCardW > 0f) return cachedCardW;

        // 카드 0번의 실제 rect 너비를 읽음 (레이아웃 계산 완료 후)
        if (cardViews != null && cardViews.Length > 0)
        {
            var rt = cardViews[0].GetComponent<RectTransform>();
            if (rt != null && rt.rect.width > 1f)
            {
                cachedCardW = rt.rect.width;
                return cachedCardW;
            }
        }
        return cardWidth;
    }

    void SnapToNearest()
    {
        if (scrollRect == null || content == null) return;
        float w     = CardW();
        int   index = Mathf.RoundToInt(-content.anchoredPosition.x / w);
        GoTo(index);
    }

    void SnapImmediate(int index)
    {
        if (scrollRect == null || content == null) return;
        float w = CardW();
        content.anchoredPosition = new Vector2(-index * w, content.anchoredPosition.y);
    }

    IEnumerator SnapCoroutine(int index)
    {
        if (isSnapping || scrollRect == null || content == null) yield break;
        isSnapping = true;

        float w       = CardW();
        float targetX = -index * w;
        float startX  = content.anchoredPosition.x;
        float elapsed = 0f;
        const float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            content.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, t),
                                                   content.anchoredPosition.y);
            yield return null;
        }
        content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
        isSnapping = false;
    }

    void RefreshDots()
    {
        if (dots == null) return;
        for (int i = 0; i < dots.Length; i++)
            if (dots[i] != null)
                dots[i].color = i == currentIndex ? dotActive : dotInactive;
    }

    public void OnStartClicked() => SceneLoadManager.LoadScene("stage@scene");

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    static RectTransform MakeRT(string name, RectTransform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static RectTransform MakeTMP(string name, RectTransform parent, string text, float size)
    {
        var rt  = MakeRT(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = TextAlignmentOptions.Center;
        return rt;
    }
}
