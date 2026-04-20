using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 씬 전투 패널 — 스테이지 선택 수직 캐러셀.
/// 기본: 현재 스테이지(크게) + 다음 스테이지(위에 작게) 표시.
/// 카드 클릭 → 스크롤 모드 ON (scrollModeHideTargets 숨김).
/// 스크롤 모드에서 카드 클릭 → 선택 후 스크롤 모드 OFF.
/// </summary>
public class StageSelectPanel : MonoBehaviour
{
    // ── 데이터 ──────────────────────────────────────────────────────
    [System.Serializable]
    public class StageEntry
    {
        public string chapterID;
        public string stageName;
        public Sprite icon;
        public int    bestWave;
        public bool   isLocked;
    }

    // ── UI 참조 ──────────────────────────────────────────────────────
    [Header("카드 폴백 프리팹 (Resources/Chapter/Card/{chapterID} 없을 때 사용, 비우면 자동 생성)")]
    public StageCardView cardPrefab;

    [Header("스크롤 (수직)")]
    public ScrollRect    scrollRect;
    public RectTransform content;

    [Header("버튼")]
    public Button startButton;

    [Header("스크롤 모드 시 숨길 오브젝트들 (panel_전투 내 기타 요소)")]
    public GameObject[] scrollModeHideTargets;

    [Header("카드 크기")]
    public float cardWidth  = 800f;
    public float cardHeight = 260f;

    [Header("카드 간격")]
    public float cardSpacing = 4f;

    [Header("카드 스케일 (원근 효과)")]
    public float centerScale = 1.0f;
    [Range(0.5f, 1f)]
    public float sideScale   = 0.82f;

    [Header("인접 카드 투명도")]
    [Range(0f, 1f)]
    public float nextCardAlpha = 0.85f;

    [Header("스냅 설정")]
    [Range(0.05f, 0.5f)]
    public float swipeThreshold = 0.2f;
    [Range(0.1f, 1.5f)]
    public float snapDuration   = 0.65f;
    [Range(1f, 5f)]
    public float snapEasePower  = 4f;    // 클수록 끝에서 더 부드럽게 감속

    // ── 런타임 ───────────────────────────────────────────────────────
    StageCardView[] cardViews;
    int             currentIndex;
    bool            isSnapping;
    Coroutine       _snapCoroutine;
    bool            _scrollMode;
    float           _dragStartY;
    float           cachedCardH = -1f;
    StageEntry[]    stages;

    CanvasGroup     _contentCG;   // 초기 플래시 방지용

    // ── DB 로드 ──────────────────────────────────────────────────────
    void LoadStagesFromDB()
    {
        var list = new System.Collections.Generic.List<StageEntry>();
        Mewage.DB.DB_챕터.ForEachEntity(row =>
        {
            string chapterID = row.f_챕터ID;
            string[] parts = chapterID.Split('_');
            if (parts.Length < 2) return;
            if (!int.TryParse(parts[0].Substring(1), out int chapterNum) ||
                !int.TryParse(parts[1].Substring(1), out int stageNum))
            {
                Debug.LogWarning($"[StageSelectPanel] Invalid chapterID: {chapterID}");
                return;
            }

            Sprite icon = Resources.Load<Sprite>($"Chapter/Icon/{chapterID}");
            int bestWave = PlayerPrefs.GetInt($"BestWave_{chapterID}", 0);

            bool isLocked = false; // 모든 스테이지 기본 개방

            list.Add(new StageEntry
            {
                chapterID = chapterID,
                stageName = $"{stageNum}.{row.f_이름}",
                icon      = icon,
                bestWave  = bestWave,
                isLocked  = isLocked
            });
        });

        if (list.Count == 0)
        {
            list.Add(new StageEntry { chapterID = "C1_S1", stageName = "1.야생 거리", isLocked = false });
            list.Add(new StageEntry { chapterID = "C1_S2", stageName = "2.어둠의 숲",  isLocked = false });
            list.Add(new StageEntry { chapterID = "C1_S3", stageName = "3.폐허 도시",  isLocked = false });
            Debug.Log("[StageSelectPanel] DB 없음, 하드코딩 사용");
        }
        stages = list.ToArray();
    }

    // ── 초기화 ───────────────────────────────────────────────────────
    void Start()
    {
        LoadStagesFromDB();
        EnsureVerticalLayout();
        BuildCards();

        // 레이아웃 완료 전 마지막 카드가 잠깐 보이는 플래시 방지
        _contentCG = content != null
            ? (content.GetComponent<CanvasGroup>() ?? content.gameObject.AddComponent<CanvasGroup>())
            : null;
        if (_contentCG != null) _contentCG.alpha = 0f;

        startButton?.onClick.AddListener(OnStartClicked);
        SetupScrollEvents();
        SetScrollMode(true);

        StartCoroutine(InitAfterLayout());
    }

    void EnsureVerticalLayout()
    {
        if (content == null) return;

        // 수직 스크롤용 anchor/pivot으로 재설정 (빌더가 수평 스크롤용으로 생성하기 때문)
        content.anchorMin        = new Vector2(0f, 1f);
        content.anchorMax        = new Vector2(1f, 1f);
        content.pivot            = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta        = Vector2.zero;

        var hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) DestroyImmediate(hlg);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment        = TextAnchor.UpperCenter;
        vlg.spacing               = cardSpacing;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = false; // 높이는 sizeDelta로 직접 제어
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    void SetupScrollEvents()
    {
        if (scrollRect == null) return;
        scrollRect.vertical      = false; // 드래그는 EventTrigger가 처리 → elastic 방지
        scrollRect.horizontal    = false;
        scrollRect.inertia       = false;
        scrollRect.movementType  = ScrollRect.MovementType.Unrestricted;

        var trigger = scrollRect.gameObject.GetComponent<EventTrigger>()
                   ?? scrollRect.gameObject.AddComponent<EventTrigger>();

        var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginEntry.callback.AddListener(e =>
        {
            if (_scrollMode)
            {
                _dragStartY = ((PointerEventData)e).position.y;
            }
        });
        trigger.triggers.Add(beginEntry);

        var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endEntry.callback.AddListener(e =>
        {
            if (!_scrollMode) return;
            float delta     = ((PointerEventData)e).position.y - _dragStartY;
            float threshold = SlotH() * swipeThreshold;
            int   target    = currentIndex;
            if      (delta < -threshold) target = currentIndex + 1;
            else if (delta >  threshold) target = currentIndex - 1;
            GoTo(Mathf.Clamp(target, 0, stages.Length - 1));
        });
        trigger.triggers.Add(endEntry);
    }

    IEnumerator InitAfterLayout()
    {
        // 뷰포트 크기가 유효해질 때까지 대기 (최대 60프레임)
        float vpW = 0f, vpH = 0f;
        for (int i = 0; i < 60; i++)
        {
            yield return null;
            if (scrollRect == null || scrollRect.viewport == null) continue;
            Canvas.ForceUpdateCanvases();
            vpW = scrollRect.viewport.rect.width;
            vpH = scrollRect.viewport.rect.height;
            if (vpW > 1f && vpH > 1f) break;
        }

        if (cardViews != null && vpW > 1f && vpH > 1f)
        {
            float targetH = vpH * 0.28f;
            foreach (var cv in cardViews)
            {
                if (cv == null) continue;
                var rt = cv.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(vpW, targetH);
                var le = cv.GetComponent<LayoutElement>();
                if (le == null) le = cv.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth  = vpW;  le.minWidth  = vpW;
                le.preferredHeight = targetH; le.minHeight = targetH;
            }
            cachedCardH = targetH;
            Canvas.ForceUpdateCanvases();
        }

        SnapImmediate(currentIndex);

        // 올바른 위치에 snap된 뒤 content 표시
        if (_contentCG != null) _contentCG.alpha = 1f;
    }

    // ── 카드 생성 ────────────────────────────────────────────────────
    // 역순 빌드: stage[n-1] → stage[0]
    // 결과: content 상단 = 높은 인덱스(다음 스테이지), 하단 = 낮은 인덱스(현재)
    void BuildCards()
    {
        if (content == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        cardViews = new StageCardView[stages.Length];

        for (int i = stages.Length - 1; i >= 0; i--)
        {
            StageCardView cv;

            // 1순위: Resources/Chapter/Card/{chapterID} 프리팹
            // 2순위: Inspector의 cardPrefab 폴백
            // 3순위: 코드 자동 생성
            string prefabPath = $"Chapter/Card/{stages[i].chapterID}";
            StageCardView loadedPrefab = Resources.Load<StageCardView>(prefabPath);
            StageCardView sourcePrefab = loadedPrefab ?? cardPrefab;

            if (sourcePrefab != null)
            {
                cv = Instantiate(sourcePrefab, content);
                if (cv == null) continue;
                var le = cv.GetComponent<LayoutElement>()
                      ?? cv.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = cardHeight;
                le.minHeight       = cardHeight;
                le.preferredWidth  = cardWidth;
                le.minWidth        = cardWidth;
                cv.Bind(stages[i]);
            }
            else
            {
                cv = BuildAutoCard(i);
            }

            // 초기 상태: 현재 스테이지만 중앙 카드로 설정
            cv.SetCenterCard(i == currentIndex);

            // CanvasGroup 미리 추가 (ApplyCardScales에서 alpha 제어용)
            if (cv.GetComponent<CanvasGroup>() == null)
                cv.gameObject.AddComponent<CanvasGroup>();

            // 클릭 핸들러
            int stageIdx = i;
            var btn = cv.GetComponent<Button>()
                   ?? cv.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnCardClicked(stageIdx));

            // Button 레이캐스트를 위한 배경 Image 보장
            if (cv.GetComponent<Image>() == null)
            {
                var bg = cv.gameObject.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0f); // 투명
            }

            cardViews[i] = cv;
        }
    }

    StageCardView BuildAutoCard(int i)
    {
        var card   = new GameObject($"Card_{i}");
        card.transform.SetParent(content, false);

        var cardRT       = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(cardWidth, cardHeight);

        var le2            = card.AddComponent<LayoutElement>();
        le2.preferredHeight = cardHeight;
        le2.minHeight       = cardHeight;
        le2.preferredWidth  = cardWidth;
        le2.minWidth        = cardWidth;

        // 배경
        var bg = card.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        // 아이콘
        var iconRT       = MakeRT("StageIcon", cardRT);
        iconRT.anchorMin = new Vector2(0.1f, 0.15f);
        iconRT.anchorMax = new Vector2(0.5f, 0.85f);
        iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
        var iconImg            = iconRT.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.enabled        = false;

        // 스테이지 이름
        var nameRT       = MakeTMP("StageName", cardRT, stages[i].stageName, 36f);
        nameRT.anchorMin = new Vector2(0.5f, 0.55f);
        nameRT.anchorMax = new Vector2(1.0f, 1.0f);
        nameRT.offsetMin = new Vector2(8f, 0f);
        nameRT.offsetMax = new Vector2(-8f, 0f);
        var nameTmp          = nameRT.GetComponent<TextMeshProUGUI>();
        nameTmp.fontStyle    = FontStyles.Bold;
        nameTmp.color        = Color.white;
        nameTmp.alignment    = TextAlignmentOptions.Left;

        // 최고 웨이브
        string waveStr  = stages[i].bestWave > 0 ? $"최고 웨이브: {stages[i].bestWave}" : "최고 웨이브: -";
        var waveRT       = MakeTMP("BestWave", cardRT, waveStr, 28f);
        waveRT.anchorMin = new Vector2(0.5f, 0.05f);
        waveRT.anchorMax = new Vector2(1.0f, 0.50f);
        waveRT.offsetMin = new Vector2(8f, 0f);
        waveRT.offsetMax = new Vector2(-8f, 0f);
        waveRT.GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.8f, 0.8f);

        var cv           = card.AddComponent<StageCardView>();
        cv.iconImage     = iconImg;
        cv.stageNameText = nameTmp;
        cv.bestWaveText  = waveRT.GetComponent<TextMeshProUGUI>();
        cv.Bind(stages[i]);
        return cv;
    }

    // ── 카드 클릭 / 스크롤 모드 ─────────────────────────────────────
    void OnCardClicked(int index)
    {
        GoTo(index);
    }

    void SetScrollMode(bool on)
    {
        _scrollMode = on;
        // 배경/이미지 raycastTarget만 해제 — Button 등 Selectable은 그대로 유지
        foreach (var go in scrollModeHideTargets)
        {
            if (go == null) continue;
            foreach (var g in go.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (g.GetComponent<UnityEngine.UI.Selectable>() != null) continue;
                g.raycastTarget = !on;
            }
        }
    }

    void RefreshCardVisibility()
    {
        if (cardViews == null) return;
        foreach (var cv in cardViews)
            if (cv != null) cv.gameObject.SetActive(true);
    }

    // ── 네비게이션 ───────────────────────────────────────────────────
    public void GoTo(int index, bool animate = true)
    {
        currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, stages.Length - 1));
        if (animate)
        {
            if (_snapCoroutine != null) { StopCoroutine(_snapCoroutine); isSnapping = false; }
            _snapCoroutine = StartCoroutine(SnapCoroutine(currentIndex));
        }
        else SnapImmediate(currentIndex);
    }

    float CardH()
    {
        if (cachedCardH > 0f) return cachedCardH;
        if (cardViews != null && cardViews.Length > 0 && cardViews[0] != null)
        {
            var rt = cardViews[0].GetComponent<RectTransform>();
            if (rt != null && rt.rect.height > 1f)
            {
                cachedCardH = rt.rect.height;
                return cachedCardH;
            }
        }
        return cardHeight;
    }

    float SlotH() => CardH() + cardSpacing;

    // stage[index]를 뷰포트 중앙에 놓는 content.anchoredPosition.y
    // top-anchored content, 역순 빌드: stage[i]는 content 상단에서 (n-1-i)번째 슬롯
    // cardCenterY_in_content = -(n-1-i)*slotH - h/2  (음수, 위에서 아래)
    // anchoredPosition.y = -vpH/2 - cardCenterY = -vpH/2 + (n-1-i)*slotH + h/2
    float SnapY(int index)
    {
        if (stages == null || scrollRect == null) return 0f;
        float h     = CardH();
        float slotH = h + cardSpacing;
        int   n     = stages.Length;
        float vpH   = scrollRect.viewport.rect.height;
        return -vpH * 0.5f + (n - 1 - index) * slotH + h * 0.5f;
    }

    float SnapYToVirtualCenter(float anchoredY)
    {
        float h     = CardH();
        float slotH = h + cardSpacing;
        int   n     = stages.Length;
        float vpH   = scrollRect != null ? scrollRect.viewport.rect.height : h;
        // inverse: (n-1-index)*slotH = anchoredY + vpH/2 - h/2
        float slotRank = (anchoredY + vpH * 0.5f - h * 0.5f) / Mathf.Max(1f, slotH);
        return n - 1 - slotRank;
    }

    void ApplyCardScales(float virtualCenter)
    {
        if (cardViews == null) return;
        for (int i = 0; i < cardViews.Length; i++)
        {
            if (cardViews[i] == null) continue;
            float dist = Mathf.Abs(i - virtualCenter);
            float s    = Mathf.Lerp(centerScale, sideScale, Mathf.Clamp01(dist));
            cardViews[i].transform.localScale = Vector3.one * s;
            cardViews[i].SetCenterCard(dist < 0.5f);

            var cg = cardViews[i].GetComponent<CanvasGroup>();
            if (cg != null)
            {
                // 이전 카드는 애니메이션 중에도 항상 숨김
                if (i < currentIndex)
                {
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                }
                else if (dist < 0.5f)
                {
                    cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                }
                else
                {
                    cg.alpha = Mathf.Lerp(nextCardAlpha, 0f, Mathf.Clamp01(dist - 0.5f));
                    cg.blocksRaycasts = true;
                }
            }
        }
    }

    void SnapImmediate(int index)
    {
        if (scrollRect == null || content == null) return;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, SnapY(index));
        ApplyCardScales(index);
        RefreshCardVisibility();
    }

    IEnumerator SnapCoroutine(int index)
    {
        if (scrollRect == null || content == null) yield break;
        isSnapping = true;

        float targetY = SnapY(index);
        float startY  = content.anchoredPosition.y;
        float elapsed = 0f;

        // 현재 위치에서 타겟까지 ease-out으로 스크롤 (카드가 자연스럽게 넘어가는 느낌)
        while (elapsed < snapDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / snapDuration);
            float tEased = 1f - Mathf.Pow(1f - t, snapEasePower);
            float newY   = Mathf.Lerp(startY, targetY, tEased);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);
            ApplyCardScales(SnapYToVirtualCenter(newY));
            yield return null;
        }
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
        ApplyCardScales(index);
        isSnapping = false;
        _snapCoroutine = null;

        // 중앙 도착 시 펀치
        cardViews?[currentIndex]?.PlayPunch();
    }


    // ── 시작 ────────────────────────────────────────────────────────
    public void OnStartClicked()
    {
        if (currentIndex < 0 || currentIndex >= stages.Length) return;
        if (stages[currentIndex].isLocked) return;
        PlayerPrefs.SetString("SelectedChapterID", stages[currentIndex].chapterID);
        PlayerPrefs.Save();
        SceneLoadManager.LoadScene("stage@scene");
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────
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
