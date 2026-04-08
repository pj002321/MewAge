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
        public string chapterID;  // DB_챕터.f_챕터ID
        public string stageName;  // DB_챕터.f_이름
        public Sprite icon;       // Resources/Chapter/Icon/{chapterID}.png
        public int    bestWave;   // 최고 생존 웨이브 (PlayerPrefs에서 로드)
        public bool   isLocked;   // 잠금 상태
    }

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
    float           _dragStartX;

    [Header("스냅 설정")]
    [Tooltip("이 픽셀 이상 드래그하면 카드 전환 (카드 너비 대비 비율)")]
    [Range(0.05f, 0.5f)]
    public float swipeThreshold = 0.2f;

    // ── 카드 스타일 (Inspector에서 조정 가능) ─────────────────────
    [Header("카드 크기 (기준 해상도 기준)")]
    public float cardWidth  = 1080f;
    public float cardHeight = 800f;

    float cachedCardW = -1f;

    StageEntry[] stages; // DB에서 로드

    void LoadStagesFromDB()
    {
        var list = new System.Collections.Generic.List<StageEntry>();
        Mewage.DB.DB_챕터.ForEachEntity(row =>
        {
            string chapterID = row.f_챕터ID;
            string chapterPart = chapterID.Split('_')[0];
            string stagePart = chapterID.Split('_')[1];
            if (!int.TryParse(chapterPart.Substring(1), out int chapterNum) ||
                !int.TryParse(stagePart.Substring(1), out int stageNum))
            {
                Debug.LogWarning($"[StageSelectPanel] Invalid chapterID: {chapterID}");
                return;
            }
            string iconPath = $"Chapter/Icon/{chapterID}";
            Sprite icon = Resources.Load<Sprite>(iconPath);

            // 최고 웨이브 로드 (PlayerPrefs에서)
            string prefKey = $"BestWave_{chapterID}";
            int bestWave = PlayerPrefs.GetInt(prefKey, 0);

            // 잠금 상태
            bool isLocked = true;
            if (chapterID == "C1_S1")
            {
                isLocked = false; // 1-1은 항상 열림
            }
            else if (chapterPart == "C1")
            {
                // 1챕터 내 이전 스테이지 클리어 확인
                string prevID = $"C1_S{stageNum - 1}";
                int prevBest = PlayerPrefs.GetInt($"BestWave_{prevID}", 0);
                isLocked = prevBest <= 0;
            }
            else
            {
                // 다른 챕터: 1챕터 클리어 확인 (C1_S3 클리어)
                int prevBest = PlayerPrefs.GetInt("BestWave_C1_S3", 0);
                isLocked = prevBest <= 0;
            }

            list.Add(new StageEntry
            {
                chapterID = chapterID,
                stageName = $"{chapterNum}-{stageNum} {row.f_이름}",
                icon = icon,
                bestWave = bestWave,
                isLocked = isLocked
            });
        });
        if (list.Count == 0)
        {
            // DB에 데이터가 없으면 테스트용 하드코딩
            list.Add(new StageEntry { chapterID = "C1", stageName = "1챕터 야생 거리", icon = null, bestWave = 0, isLocked = false });
            list.Add(new StageEntry { chapterID = "C2", stageName = "2챕터 어둠의 숲", icon = null, bestWave = 0, isLocked = true });
            list.Add(new StageEntry { chapterID = "C3", stageName = "3챕터 폐허 도시", icon = null, bestWave = 0, isLocked = true });
            Debug.Log("[StageSelectPanel] No stages in DB, using hardcoded test data");
        }
        stages = list.ToArray();
        Debug.Log($"[StageSelectPanel] Loaded {stages.Length} stages from DB");
    }

    void Start()
    {
        LoadStagesFromDB();
        BuildCards();
        BuildDots();

        startButton?.onClick.AddListener(OnStartClicked);

        if (scrollRect != null)
        {
            scrollRect.inertia      = false;
            scrollRect.movementType = ScrollRect.MovementType.Unrestricted;

            // ScrollRect GameObject에 직접 EventTrigger 등록
            var trigger = scrollRect.gameObject.AddComponent<EventTrigger>();

            var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginEntry.callback.AddListener(e => _dragStartX = ((PointerEventData)e).position.x);
            trigger.triggers.Add(beginEntry);

            var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endEntry.callback.AddListener(e =>
            {
                float delta     = ((PointerEventData)e).position.x - _dragStartX;
                float threshold = CardW() * swipeThreshold;
                int   target    = currentIndex;
                if      (delta < -threshold) target = currentIndex + 1;
                else if (delta >  threshold) target = currentIndex - 1;
                GoTo(Mathf.Clamp(target, 0, stages.Length - 1));
            });
            trigger.triggers.Add(endEntry);
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
        if (content == null)
        {
            Debug.LogError("[StageSelectPanel] content is null - cannot build cards");
            return;
        }
        Debug.Log($"[StageSelectPanel] Building {stages.Length} cards");

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
        if (cv == null)
        {
            Debug.LogError($"[StageSelectPanel] Failed to instantiate cardPrefab for stage {i}");
            continue;
        }
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

    public void OnStartClicked()
    {
        if (currentIndex < 0 || currentIndex >= stages.Length) return;
        if (stages[currentIndex].isLocked) return; // 잠금 시 시작 불가
        string selectedChapterID = stages[currentIndex].chapterID;
        PlayerPrefs.SetString("SelectedChapterID", selectedChapterID);
        PlayerPrefs.Save();
        SceneLoadManager.LoadScene("stage@scene");
    }

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
