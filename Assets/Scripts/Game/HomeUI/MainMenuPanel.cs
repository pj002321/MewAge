using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 씬 전투 패널 — 스테이지 선택 수직 캐러셀.
/// 드래그 중: 손가락 1:1 추적 + 관성 여운 → 가장 가까운 카드에 스냅.
/// 스냅 완료 시에만 SetCenterCard(true) 호출 → 캐릭터/몬스터 랜딩 애니메이션 억제.
/// </summary>
public class MainMenuPanel : MonoBehaviour
{
    // ── 데이터 ──────────────────────────────────────────────────────
    [System.Serializable]
    public class StageEntry
    {
        public string chapterID;
        public string stageName;
        public string chapterLabel;  // "구석기 - 원시의 마을"
        public Sprite icon;
        public int    bestWave;
        public int    totalWaves;
        public bool   isLocked;
        public string requireClearLabel; // "1-1 클리어" 형식
    }

    // ── UI 참조 ──────────────────────────────────────────────────────
    [Header("UI")]
    public Button storageButton;
    public Button specialGiftButton;
    public Button dailymissionButton;
    public Button autohuntButton;
    public Button attendanceButton;
    public Button rankingButton;
    public Button menuButton;
    public Button inventoryButton;
    public Button achievementButton;
    public Button breakthroughRewardButton;

    public PopUpAttendance attendancePopup;
    public PopUpDailyMission dailyMissionPopup;
    public PopUpStorage storagePopup;
    public PopUpSpecialGift specialgiftPopup;
    public PopUpMenu menuPopup;
    public PopUpRanking rankingPopup;
    public PopUpInventory inventoryPopup;
    public PopUpAchievement achievementPopup;
    public PopUpAutoHunt autoHuntPopup;
    public PopUpBreakthroughReward breakthroughRewardPopup;

    [Header("스크롤 (수직)")]
    public ScrollRect    scrollRect;
    public RectTransform content;

    [Header("버튼")]
    public Button startButton;

    [Header("카드")]
    [SerializeField] StageCardView      cardBasePrefab;
    [SerializeField] StageVisualConfig  stageVisuals;
    public float cardWidth     = 800f;
    public float cardHeight    = 260f;
    public float cardSpacing   = 4f;
    [Range(0.5f, 1f)]
    public float sideScale     = 0.82f;
    [Range(0f, 1f)]
    public float nextCardAlpha = 0.85f;

    [Header("스크롤")]
    [Range(0.05f, 0.5f)]
    public float snapDuration   = 0.18f;
    public float flickThreshold = 400f;

    [Header("배경")]
    public Image    background;
    public Sprite[] chapterBackgrounds;  // 인덱스 0 = C1, 1 = C2 …
    public Image    background2;
    public Sprite[] chapterBackgrounds2; // 인덱스 0 = C1, 1 = C2 …
    
    // ── 런타임 ───────────────────────────────────────────────────────
    StageCardView[] cardViews;
    int             currentIndex;
    bool            isSnapping;
    Coroutine       _snapCoroutine;
    float           _dragStartY;
    float           _contentStartY;
    float           cachedCardH = -1f;
    StageEntry[]    stages;
    CanvasGroup     _contentCG;

    // 관성용 속도 추적 (EMA 방식)
    float _dragVelocity;
    float _prevPointerY;
    float _prevPointerTime;
    // BeginDrag에서 캐시 — Drag 중 SnapY() 반복 호출 방지
    float _dragMinY;
    float _dragMaxY;

    // 배경 슬라이드
    Image     _bgOverlay;
    Image     _bgOverlay2;
    Coroutine _bgFadeCoroutine;
    string    _currentBgChapter;
    Vector3   _bgHomePos;
    Vector3   _bg2HomePos;
    const float BG_FADE_DURATION = 0.35f;


    // ── DB 로드 ──────────────────────────────────────────────────────
    void LoadStagesFromDB()
    {
        var list = new System.Collections.Generic.List<StageEntry>();
        // (chapterNum, stageNum, row) 임시 저장 후 정렬해 순번 할당
        var raw = new System.Collections.Generic.List<(int c, int s, Mewage.DB.DB_챕터 row)>();
        Mewage.DB.DB_챕터.ForEachEntity(row =>
        {
            string chapterID = row.f_챕터ID;
            string[] parts = chapterID.Split('_');
            if (parts.Length < 2) return;
            if (!int.TryParse(parts[0].Substring(1), out int chapterNum) ||
                !int.TryParse(parts[1].Substring(1), out int stageNum))
            {
                Debug.LogWarning($"[MainMenuPanel] Invalid chapterID: {chapterID}");
                return;
            }
            raw.Add((chapterNum, stageNum, row));
        });
        raw.Sort((a, b) => a.c != b.c ? a.c.CompareTo(b.c) : a.s.CompareTo(b.s));
        for (int i = 0; i < raw.Count; i++)
        {
            var (chapterNum, _, row) = raw[i];
            string chapterID = row.f_챕터ID;
            Sprite icon    = stageVisuals?.Get(i)?.icon;
            int bestWave   = PlayerPrefs.GetInt($"BestWave_{chapterID}", 0);
            int totalWaves = CalcTotalWaves(chapterID);
            list.Add(new StageEntry
            {
                chapterID    = chapterID,
                stageName    = $"{i + 1}.{row.f_이름}",
                chapterLabel = BuildChapterLabel(chapterNum),
                icon         = icon,
                bestWave     = bestWave,
                totalWaves   = totalWaves,
                isLocked     = false
            });
        }

        if (list.Count == 0)
        {
            list.Add(new StageEntry { chapterID = "C1_S1", stageName = "1.야생 거리" });
            list.Add(new StageEntry { chapterID = "C1_S2", stageName = "2.어둠의 숲"  });
            list.Add(new StageEntry { chapterID = "C1_S3", stageName = "3.폐허 도시"  });
        }

        // 첫 스테이지는 항상 해금, C4 이상은 강제 잠금, 이후는 이전 스테이지 클리어 여부로 판단
        for (int i = 0; i < list.Count; i++)
        {
            string[] p = list[i].chapterID.Split('_');
            int chNum = (p.Length >= 1 && p[0].Length >= 2 &&
                         int.TryParse(p[0].Substring(1), out int n)) ? n : 1;

            bool locked = chNum >= 4 ||
                          (i > 0 && PlayerPrefs.GetInt($"ChapterCleared_{list[i - 1].chapterID}", 0) == 0);
            list[i].isLocked = locked;
            if (locked)
                list[i].requireClearLabel = chNum >= 4
                    ? "준비 중"
                    : ChapterIDToDisplayLabel(list[i - 1].chapterID) + " 클리어";
        }

        stages = list.ToArray();
    }

    // "C1_S2" → "1-2"
    static string ChapterIDToDisplayLabel(string chapterID)
    {
        string[] parts = chapterID.Split('_');
        if (parts.Length < 2) return chapterID;
        string c = parts[0].Length > 1 ? parts[0].Substring(1) : parts[0];
        string s = parts[1].Length > 1 ? parts[1].Substring(1) : parts[1];
        return $"{c}-{s}";
    }

    static int CalcTotalWaves(string chapterID)
    {
        string prefix = chapterID + "_W";
        int max = 0;
        Mewage.DB.DB_웨이브.ForEachEntity(row =>
        {
            if (row.f_웨이브ID != null && row.f_웨이브ID.StartsWith(prefix) &&
                int.TryParse(row.f_웨이브ID.Substring(prefix.Length), out int n) && n > max)
                max = n;
        });
        return max > 0 ? max : 30;
    }

    int FindSavedStageIndex()
    {
        if (stages == null || stages.Length == 0) return 0;
        string saved = PlayerPrefs.GetString("SelectedChapterID", "");
        if (string.IsNullOrEmpty(saved)) return 0;
        for (int i = 0; i < stages.Length; i++)
            if (stages[i].chapterID == saved) return i;
        return 0;
    }

    // ── 초기화 ───────────────────────────────────────────────────────
    void Awake()
    {
        // Start보다 먼저 content를 숨겨 첫 프레임 플래시 방지
        if (content != null)
        {
            _contentCG = content.GetComponent<CanvasGroup>();
            if (_contentCG == null)
                _contentCG = content.gameObject.AddComponent<CanvasGroup>();
            _contentCG.alpha = 0f;
        }
        SetupBackgroundOverlay();
    }

    void SetupBackgroundOverlay()
    {
        _bgOverlay  = CreateOverlayFor(background,  "BackgroundOverlay");
        _bgOverlay2 = CreateOverlayFor(background2, "BackgroundOverlay2");
    }

    Image CreateOverlayFor(Image src, string goName)
    {
        if (src == null) return null;
        var go = new GameObject(goName);
        go.transform.SetParent(src.transform.parent, false);
        var srcRT     = src.GetComponent<RectTransform>();
        var overlayRT = go.AddComponent<RectTransform>();
        overlayRT.anchorMin = srcRT.anchorMin;
        overlayRT.anchorMax = srcRT.anchorMax;
        overlayRT.pivot     = srcRT.pivot;
        overlayRT.offsetMin = srcRT.offsetMin;
        overlayRT.offsetMax = srcRT.offsetMax;
        var img   = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        go.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
        return img;
    }

    void OnEnable()
    {
        if (cardViews == null) return; // Start() 이전이면 건너뜀
        RefreshStages();
        StartCoroutine(ReSnapOnEnable());
    }

    IEnumerator ReSnapOnEnable()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        SnapImmediate(currentIndex);
    }

    void Start()
    {
        LoadStagesFromDB();
        currentIndex = FindSavedStageIndex();
        EnsureVerticalLayout();
        BuildCards();

        storageButton?.onClick.AddListener(() => storagePopup?.Show());
        dailymissionButton?.onClick.AddListener(() => dailyMissionPopup?.Show());
        attendanceButton?.onClick.AddListener(() => attendancePopup?.Show());
        specialGiftButton?.onClick.AddListener(() => specialgiftPopup?.Show());
        menuButton?.onClick.AddListener(() => menuPopup?.Show());
        rankingButton?.onClick.AddListener(() => rankingPopup?.Show());
        inventoryButton?.onClick.AddListener(() => inventoryPopup.Show());
        achievementButton?.onClick.AddListener(() => achievementPopup.Show());
        autohuntButton?.onClick.AddListener(() => autoHuntPopup.Show());
        breakthroughRewardButton?.onClick.AddListener(() => breakthroughRewardPopup.Show());

        startButton?.onClick.AddListener(OnStartClicked);
        SetupScrollEvents();

        ApplyMenuUnlockState();
        StartCoroutine(InitAfterLayout());
    }

    // ── 메뉴 잠금/해금 ───────────────────────────────────────────────
    public bool IsMenuUnlocked { get; private set; }

    public void ApplyMenuUnlockState()
    {
        IsMenuUnlocked = PlayerPrefs.GetInt("BattleCompleted", 0) == 1;
        SetMenuButtonsActive(IsMenuUnlocked);
    }

    public void DebugToggleMenu()
    {
        IsMenuUnlocked = !IsMenuUnlocked;
        SetMenuButtonsActive(IsMenuUnlocked);
    }

    void SetMenuButtonsActive(bool active)
    {
        foreach (var btn in new[]
        {
            storageButton, specialGiftButton, dailymissionButton,
            achievementButton, attendanceButton, rankingButton, inventoryButton,
        })
            if (btn) btn.gameObject.SetActive(active);
    }

    void EnsureVerticalLayout()
    {
        if (content == null) return;

        content.anchorMin        = new Vector2(0f, 1f);
        content.anchorMax        = new Vector2(1f, 1f);
        content.pivot            = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta        = Vector2.zero;

        var hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) DestroyImmediate(hlg);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.UpperCenter;
        vlg.spacing                = cardSpacing;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    void SetupScrollEvents()
    {
        if (scrollRect == null) return;
        scrollRect.vertical     = false;
        scrollRect.horizontal   = false;
        scrollRect.inertia      = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var trigger = scrollRect.gameObject.GetComponent<EventTrigger>()
                   ?? scrollRect.gameObject.AddComponent<EventTrigger>();

        // ── BeginDrag ──────────────────────────────────────────────
        var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginEntry.callback.AddListener(e =>
        {
            if (_snapCoroutine != null) { StopCoroutine(_snapCoroutine); isSnapping = false; }
            var ptr         = (PointerEventData)e;
            _dragStartY     = ptr.position.y;
            _contentStartY  = content.anchoredPosition.y;
            _dragVelocity   = 0f;
            _prevPointerY   = ptr.position.y;
            _prevPointerTime = Time.unscaledTime;
            float rubber = SlotH() * 0.3f;
            _dragMinY = SnapY(stages.Length - 1) - rubber;
            _dragMaxY = SnapY(0)                 + rubber;
        });
        trigger.triggers.Add(beginEntry);

        // ── Drag ───────────────────────────────────────────────────
        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener(e =>
        {
            var ptr  = (PointerEventData)e;
            float y  = ptr.position.y;
            float dt = Time.unscaledTime - _prevPointerTime;

            if (dt > 0.001f)
            {
                float frameVel = (y - _prevPointerY) / dt;
                _dragVelocity  = Mathf.Lerp(_dragVelocity, frameVel, 0.35f);
            }
            _prevPointerY    = y;
            _prevPointerTime = Time.unscaledTime;

            float rawY = _contentStartY + (y - _dragStartY);
            float newY = Mathf.Clamp(rawY, _dragMinY, _dragMaxY);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);

            ApplyCardScales(SnapYToVirtualCenter(newY), suppressAnimation: true);
        });
        trigger.triggers.Add(dragEntry);

        // ── EndDrag ────────────────────────────────────────────────
        var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endEntry.callback.AddListener(e =>
        {
            // 현재 콘텐츠 위치 기준 가장 가까운 카드 계산
            int nearest = Mathf.Clamp(
                Mathf.RoundToInt(SnapYToVirtualCenter(content.anchoredPosition.y)),
                0, stages.Length - 1);

            // 플릭이 충분히 빠르면 nearest 에서 한 칸 더 이동
            int target;
            if      (_dragVelocity < -flickThreshold) target = Mathf.Min(nearest + 1, stages.Length - 1);
            else if (_dragVelocity >  flickThreshold) target = Mathf.Max(nearest - 1, 0);
            else                                      target = nearest;

            GoTo(target);
        });
        trigger.triggers.Add(endEntry);
    }

    IEnumerator InitAfterLayout()
    {
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
            // 리사이즈 후 레이아웃이 확정되도록 1프레임 대기
            yield return null;
        }

        Canvas.ForceUpdateCanvases();
        SnapImmediate(currentIndex);

        if (stages != null && stages.Length > 0)
        {
            SoundManager.Shared?.PlayChapterHomeBGM(stages[currentIndex].chapterID);
            string initChapter = ChapterPrefix(stages[currentIndex].chapterID);
            _currentBgChapter  = initChapter;
            if (background != null)
            {
                background.sprite = ChapterSprite(initChapter);
                background.SetNativeSize();
            }
            if (background2 != null)
            {
                background2.sprite = ChapterSprite2(initChapter);
                background2.SetNativeSize();
            }
        }

        if (_contentCG != null) _contentCG.alpha = 1f;

        if (background  != null) _bgHomePos  = background.rectTransform.localPosition;
        if (background2 != null) _bg2HomePos = background2.rectTransform.localPosition;
    }

    // ── 카드 생성 ────────────────────────────────────────────────────
    void BuildCards()
    {
        if (content == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        cardViews = new StageCardView[stages.Length];

        for (int i = stages.Length - 1; i >= 0; i--)
        {
            StageCardView cv;

            if (cardBasePrefab != null)
            {
                cv = Instantiate(cardBasePrefab, content);
                if (cv == null) continue;
                var le            = cv.GetComponent<LayoutElement>() ?? cv.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = cardHeight; le.minHeight = cardHeight;
                le.preferredWidth  = cardWidth;  le.minWidth  = cardWidth;
                cv.Bind(stages[i]);
                cv.BindSymbol(stageVisuals?.Get(i)?.symbolPrefab);
            }
            else
            {
                cv = BuildAutoCard(i);
            }

            cv.SetCenterCard(i == currentIndex);

            if (cv.GetComponent<CanvasGroup>() == null)
                cv.gameObject.AddComponent<CanvasGroup>();

            int stageIdx = i;
            var btn = cv.GetComponent<Button>() ?? cv.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnCardClicked(stageIdx));

            if (cv.GetComponent<Image>() == null)
            {
                var bg = cv.gameObject.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0f);
            }

            cardViews[i] = cv;
        }
    }

    StageCardView BuildAutoCard(int i)
    {
        var card         = new GameObject($"Card_{i}");
        card.transform.SetParent(content, false);
        var cardRT       = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(cardWidth, cardHeight);

        var le2             = card.AddComponent<LayoutElement>();
        le2.preferredHeight = cardHeight; le2.minHeight = cardHeight;
        le2.preferredWidth  = cardWidth;  le2.minWidth  = cardWidth;

        var bg   = card.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        var iconRT             = MakeRT("StageIcon", cardRT);
        iconRT.anchorMin       = new Vector2(0.1f, 0.15f);
        iconRT.anchorMax       = new Vector2(0.5f, 0.85f);
        iconRT.offsetMin       = iconRT.offsetMax = Vector2.zero;
        var iconImg            = iconRT.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.enabled        = false;

        var nameRT       = MakeTMP("StageName", cardRT, stages[i].stageName, 36f);
        nameRT.anchorMin = new Vector2(0.5f, 0.55f);
        nameRT.anchorMax = new Vector2(1.0f, 1.0f);
        nameRT.offsetMin = new Vector2(8f, 0f);
        nameRT.offsetMax = new Vector2(-8f, 0f);
        var nameTmp      = nameRT.GetComponent<TextMeshProUGUI>();
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color     = Color.white;
        nameTmp.alignment = TextAlignmentOptions.Left;

        string waveStr   = stages[i].bestWave > 0 ? $"최고생존 : {stages[i].bestWave}/{stages[i].totalWaves}" : "";
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

    // ── 카드 클릭 ────────────────────────────────────────────────────
    void OnCardClicked(int index) => GoTo(index);

    // ── 네비게이션 ───────────────────────────────────────────────────
    public void GoTo(int index, bool animate = true)
    {
        currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, stages.Length - 1));
        SoundManager.Shared?.PlayChapterHomeBGM(stages[currentIndex].chapterID);

        string newChapter = ChapterPrefix(stages[currentIndex].chapterID);
        if (newChapter != _currentBgChapter)
        {
            _currentBgChapter = newChapter;
            if (_bgFadeCoroutine != null) StopCoroutine(_bgFadeCoroutine);
            _bgFadeCoroutine = StartCoroutine(CrossfadeBackground(
                ChapterSprite(newChapter), ChapterSprite2(newChapter)));
        }

        if (_snapCoroutine != null) { StopCoroutine(_snapCoroutine); isSnapping = false; }

        if (animate)
            _snapCoroutine = StartCoroutine(SnapCoroutine(currentIndex));
        else
            SnapImmediate(currentIndex);
    }

    IEnumerator CrossfadeBackground(Sprite next, Sprite next2)
    {
        // 인터럽트 복구: base를 홈 위치로 스냅
        if (background  != null) background.rectTransform.localPosition  = _bgHomePos;
        if (background2 != null) background2.rectTransform.localPosition = _bg2HomePos;

        float slideH = (background != null && background.rectTransform.rect.height > 10f)
                       ? background.rectTransform.rect.height : 800f;

        // 오버레이: 새 스프라이트, 완전 불투명, 화면 아래에 배치
        if (_bgOverlay != null)
        {
            _bgOverlay.sprite = next;
            _bgOverlay.color  = Color.white;
            _bgOverlay.rectTransform.localPosition = _bgHomePos + Vector3.down * slideH;
        }
        if (_bgOverlay2 != null)
        {
            _bgOverlay2.sprite = next2;
            _bgOverlay2.color  = Color.white;
            _bgOverlay2.rectTransform.localPosition = _bg2HomePos + Vector3.down * slideH;
        }

        float elapsed = 0f;
        while (elapsed < BG_FADE_DURATION)
        {
            elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(elapsed / BG_FADE_DURATION);
            float ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

            if (background  != null)
                background.rectTransform.localPosition  = _bgHomePos  + Vector3.down * (slideH * ease);
            if (background2 != null)
                background2.rectTransform.localPosition = _bg2HomePos + Vector3.down * (slideH * ease);
            if (_bgOverlay  != null)
                _bgOverlay.rectTransform.localPosition  = _bgHomePos  + Vector3.down * (slideH * (1f - ease));
            if (_bgOverlay2 != null)
                _bgOverlay2.rectTransform.localPosition = _bg2HomePos + Vector3.down * (slideH * (1f - ease));

            yield return null;
        }

        // 마무리: base 교체 + 위치 복원, overlay 숨김
        if (background  != null) { background.sprite  = next;  background.rectTransform.localPosition  = _bgHomePos;  background.SetNativeSize();  }
        if (background2 != null) { background2.sprite = next2; background2.rectTransform.localPosition = _bg2HomePos; background2.SetNativeSize(); }
        if (_bgOverlay  != null) { _bgOverlay.color   = new Color(1f,1f,1f,0f); _bgOverlay.rectTransform.localPosition  = _bgHomePos;  }
        if (_bgOverlay2 != null) { _bgOverlay2.color  = new Color(1f,1f,1f,0f); _bgOverlay2.rectTransform.localPosition = _bg2HomePos; }
        _bgFadeCoroutine = null;
    }

    static string ChapterPrefix(string chapterID)
    {
        if (string.IsNullOrEmpty(chapterID)) return chapterID;
        int under = chapterID.IndexOf('_');
        return under >= 0 ? chapterID.Substring(0, under) : chapterID;
    }

    Sprite ChapterSprite(string chapterPrefix)
        => ChapterSpriteFrom(chapterPrefix, chapterBackgrounds);

    Sprite ChapterSprite2(string chapterPrefix)
        => ChapterSpriteFrom(chapterPrefix, chapterBackgrounds2);

    static Sprite ChapterSpriteFrom(string chapterPrefix, Sprite[] arr)
    {
        if (arr == null || arr.Length == 0) return null;
        if (chapterPrefix.Length >= 2 && chapterPrefix[0] == 'C' &&
            int.TryParse(chapterPrefix.Substring(1), out int num))
        {
            int idx = num - 1;
            if (idx >= 0 && idx < arr.Length) return arr[idx];
        }
        return arr[0];
    }

    // ── 스케일/알파 ──────────────────────────────────────────────────
    /// <param name="suppressAnimation">true 이면 SetCenterCard 호출 생략 (드래그 중)</param>
    void ApplyCardScales(float virtualCenter, bool suppressAnimation = false)
    {
        if (cardViews == null) return;
        for (int i = 0; i < cardViews.Length; i++)
        {
            if (cardViews[i] == null) continue;
            float dist = Mathf.Abs(i - virtualCenter);
            float s    = Mathf.Lerp(1f, sideScale, Mathf.Clamp01(dist));
            cardViews[i].transform.localScale = Vector3.one * s;

            if (!suppressAnimation)
                cardViews[i].SetCenterCard(dist < 0.5f);

            var cg = cardViews[i].GetComponent<CanvasGroup>();
            if (cg == null) continue;

            if (!suppressAnimation && i < currentIndex)
            {
                cg.alpha = 0f; cg.blocksRaycasts = false;
            }
            else if (dist < 0.5f)
            {
                cg.alpha = 1f; cg.blocksRaycasts = true;
            }
            else
            {
                cg.alpha = Mathf.Lerp(nextCardAlpha, 0f, Mathf.Clamp01(dist - 0.5f));
                cg.blocksRaycasts = true;
            }
        }
    }

    void SnapImmediate(int index)
    {
        if (scrollRect == null || content == null) return;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, SnapY(index));
        ApplyCardScales(index, suppressAnimation: false);
        foreach (var cv in cardViews)
            if (cv != null) cv.gameObject.SetActive(true);
    }

    // ── 스냅 코루틴 (ease-out) ───────────────────────────────────────
    IEnumerator SnapCoroutine(int index)
    {
        isSnapping = true;
        float targetY = SnapY(index);
        float startY  = content.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / snapDuration);
            float tEased = 1f - Mathf.Pow(1f - t, 2f);
            float newY   = Mathf.Lerp(startY, targetY, tEased);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);
            ApplyCardScales(SnapYToVirtualCenter(newY), suppressAnimation: true);
            yield return null;
        }

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
        ApplyCardScales(index, suppressAnimation: false);

        isSnapping     = false;
        _snapCoroutine = null;
    }

    // ── 수학 헬퍼 ───────────────────────────────────────────────────
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
        float slotRank = (anchoredY + vpH * 0.5f - h * 0.5f) / Mathf.Max(1f, slotH);
        return n - 1 - slotRank;
    }

    // ── 시작 ────────────────────────────────────────────────────────
    public void OnStartClicked()
    {
        if (currentIndex < 0 || currentIndex >= stages.Length) return;
        if (stages[currentIndex].isLocked)
        {
            string[] p = stages[currentIndex].chapterID.Split('_');
            bool comingSoon = p.Length >= 1 && p[0].Length >= 2 &&
                              int.TryParse(p[0].Substring(1), out int cn) && cn >= 4;
            PopupBaseNoti.Shared?.Show(comingSoon ? "업데이트 예정입니다." : "이전 스테이지 클리어 후 해금");
            return;
        }
        if (SceneLoadManager.IsTransitioning) return;
        PlayerPrefs.SetString("SelectedChapterID", stages[currentIndex].chapterID);
        PlayerPrefs.Save();
        SceneLoadManager.LoadScene("stage@scene");
    }

    // ── 스테이지 갱신 (외부 호출용) ──────────────────────────────────
    public void RefreshStages()
    {
        LoadStagesFromDB();
        currentIndex = FindSavedStageIndex();
        if (stages == null || cardViews == null) return;
        for (int i = 0; i < Mathf.Min(stages.Length, cardViews.Length); i++)
            cardViews[i]?.Bind(stages[i]);
        ApplyCardScales(currentIndex, suppressAnimation: false);
        StartCoroutine(ReSnapOnEnable());
    }

    // ── 챕터 레이블 ─────────────────────────────────────────────────
    static string BuildChapterLabel(int chapterNum) => chapterNum switch
    {
        1  => "원시시대",
        2  => "신석기시대",
        3  => "청동시대",
        4  => "고대문명",
        5  => "중세시대",
        6  => "르네상스",
        7  => "산업시대",
        8  => "현대시대",
        9  => "도시시대",
        10 => "궤도시대",
        11 => "우주시대",
        _  => $"{chapterNum}챕터"
    };

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
