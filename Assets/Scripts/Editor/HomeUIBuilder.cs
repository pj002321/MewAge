using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HomeUIBuilder
{
    // ────────────────────────────────────────────────────────────────
    // 상수
    // ────────────────────────────────────────────────────────────────

    const string SCENE_PATH  = "Assets/_Recovery/home@scene.unity";
    const float  TOP_BAR_H   = 190f;   // 기준 해상도(1080×1920) 기준 상단 바 높이
    const float  TAB_BAR_H   = 120f;   // 하단 탭 바 높이

    static readonly Color ColDarkBg     = new Color(0.08f, 0.10f, 0.18f, 1f);
    static readonly Color ColTopBarBg   = new Color(0.06f, 0.08f, 0.14f, 0.97f);
    static readonly Color ColTabActive  = new Color(1f,   0.80f, 0.20f, 1f);
    static readonly Color ColTabInactive= new Color(0.35f,0.35f, 0.35f, 1f);
    static readonly Color ColGold       = new Color(1f,   0.80f, 0.15f, 1f);
    static readonly Color ColGem        = new Color(0.20f,0.85f, 0.90f, 1f);
    static readonly Color ColAvatar     = new Color(0.30f,0.35f, 0.55f, 1f);

    // ────────────────────────────────────────────────────────────────
    // Home UI 전체 빌드
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Home UI")]
    public static void BuildHomeUI()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // 기존 Canvas 제거
        foreach (var c in Object.FindObjectsOfType<Canvas>())
            if (c.gameObject.name == "HomeUICanvas")
                Object.DestroyImmediate(c.gameObject);

        // ── 1. Canvas ─────────────────────────────────────────────────
        var canvasGO = new GameObject("HomeUICanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        // CanvasScaler: 높이 기준 스케일 (기준 해상도 1080×1920)
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 1f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Canvas 전체 배경 = 검정 (iPad 좌우 필러박스 영역)
        var canvasBg   = canvasGO.AddComponent<Image>();
        canvasBg.color = Color.black;
        canvasBg.raycastTarget = false;

        // EventSystem
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── 2. AspectContainer: 9:16 비율 유지, 나머지는 검은 여백 ────
        var aspectGO   = new GameObject("AspectContainer");
        aspectGO.transform.SetParent(canvasGO.transform, false);
        var aspectRT   = aspectGO.AddComponent<RectTransform>();
        aspectRT.anchorMin        = new Vector2(0.5f, 0.5f);
        aspectRT.anchorMax        = new Vector2(0.5f, 0.5f);
        aspectRT.pivot            = new Vector2(0.5f, 0.5f);
        aspectRT.anchoredPosition = Vector2.zero;
        aspectRT.sizeDelta        = new Vector2(1080, 1920);

        var arf            = aspectGO.AddComponent<AspectRatioFitter>();
        arf.aspectMode     = AspectRatioFitter.AspectMode.FitInParent;
        arf.aspectRatio    = 9f / 16f;   // iPhone 기준 세로 비율

        // ── 3. SafeAreaAnchor (노치/홈인디케이터 대응) ────────────────
        var safeAnchor = CreateStretch("SafeAreaAnchor", aspectRT);
        safeAnchor.gameObject.AddComponent<SafeAreaFitter>();

        // ── 3. TopBar ─────────────────────────────────────────────────
        var topBar = BuildTopBar(safeAnchor);

        // ── 4. PanelContainer (TopBar 아래 ~ TabBar 위) ───────────────
        var container = CreateRect("PanelContainer", safeAnchor);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = new Vector2(0,  TAB_BAR_H);
        container.offsetMax = new Vector2(0, -TOP_BAR_H);

        // ── 5. 패널 5개 ───────────────────────────────────────────────
        string[] panelNames  = { "Panel_상점", "Panel_대원", "Panel_전투", "Panel_장비", "Panel_기물" };
        Color[]  panelColors =
        {
            new Color(0.20f, 0.40f, 0.80f, 1f),
            new Color(0.20f, 0.70f, 0.40f, 1f),
            new Color(0.60f, 0.20f, 0.80f, 1f),
            new Color(0.80f, 0.50f, 0.10f, 1f),
            new Color(0.10f, 0.60f, 0.70f, 1f),
        };

        var panels = new GameObject[5];
        for (int i = 0; i < panelNames.Length; i++)
        {
            var p = new GameObject(panelNames[i]);
            p.transform.SetParent(container, false);

            var pImg = p.AddComponent<Image>();
            pImg.color = panelColors[i];

            var pRect = p.GetComponent<RectTransform>();
            pRect.anchorMin = Vector2.zero;
            pRect.anchorMax = Vector2.one;
            pRect.offsetMin = Vector2.zero;
            pRect.offsetMax = Vector2.zero;

            p.SetActive(i == 2); // 기본: 전투 패널만 활성

            // 패널 레이블
            AddLabel(p.transform, panelNames[i].Replace("Panel_", ""));

            panels[i] = p;
        }

        // 전투 패널: 게임 시작 버튼
        var startBtn = AddStartButton(panels[2].transform);

        // ── 6. TabBar ────────────────────────────────────────────────
        var (tabBar, tabButtons) = BuildTabBar(safeAnchor);

        // ── 7. HomeUIManager 연결 ────────────────────────────────────
        var manager        = canvasGO.AddComponent<HomeUIManager>();
        manager.panels     = panels;
        manager.tabButtons = tabButtons;

        // TopBar의 HomeUserInfoBar 연결
        var infoBar = topBar.GetComponent<HomeUserInfoBar>();
        if (infoBar != null) manager.userInfoBar = infoBar;

        // StartButton → StartGame 이벤트 연결
        UnityEventTools.AddPersistentListener(startBtn.onClick, manager.StartGame);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Home UI 빌드 완료.");
    }

    // ────────────────────────────────────────────────────────────────
    // TopBar 빌드
    // ────────────────────────────────────────────────────────────────

    static GameObject BuildTopBar(RectTransform parent)
    {
        // ─ 컨테이너 ──────────────────────────────────────────────────
        var rt       = CreateRect("TopBar", parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -TOP_BAR_H);
        rt.offsetMax = Vector2.zero;
        rt.gameObject.AddComponent<Image>().color = ColTopBarBg;

        // LayoutGroup 없이 직접 앵커+sizeDelta 배치 → Inspector에서 자유롭게 편집 가능
        const float PAD       = 14f;
        const float AVATAR_S  = 150f;
        const float INFO_X    = PAD + AVATAR_S + 12f;  // 176
        const float INFO_W    = 340f;
        const float CUR_W     = 170f;   // 재화 묶음 너비
        const float CUR_H     = 46f;    // 재화 묶음 높이
        const float CUR_GAP   = 6f;

        // ─ 아바타 ─────────────────────────────────────────────────────
        var avatarRT          = CreateRect("AvatarFrame", rt);
        avatarRT.anchorMin    = new Vector2(0f, 0.5f);
        avatarRT.anchorMax    = new Vector2(0f, 0.5f);
        avatarRT.pivot        = new Vector2(0f, 0.5f);
        avatarRT.anchoredPosition = new Vector2(PAD, 0f);
        avatarRT.sizeDelta    = new Vector2(AVATAR_S, AVATAR_S);
        avatarRT.gameObject.AddComponent<Image>().color = ColAvatar;

        // 아바타 아이콘 (내부 — 스프라이트 연결 후 enabled = true)
        var avatarIconRT      = CreateRect("AvatarIcon", avatarRT);
        avatarIconRT.anchorMin = new Vector2(0.08f, 0.08f);
        avatarIconRT.anchorMax = new Vector2(0.92f, 0.92f);
        avatarIconRT.offsetMin = Vector2.zero;
        avatarIconRT.offsetMax = Vector2.zero;
        var avatarIcon        = avatarIconRT.gameObject.AddComponent<Image>();
        avatarIcon.color      = new Color(0.5f, 0.55f, 0.75f, 0.6f);
        avatarIcon.enabled    = false;

        // ─ 레벨 텍스트 ────────────────────────────────────────────────
        var levelText         = CreateTMPText("LevelText", rt, "Lv.1", 24f, TextAlignmentOptions.Left);
        levelText.color       = ColGold;
        levelText.fontStyle   = FontStyles.Bold;
        var levelRT           = levelText.GetComponent<RectTransform>();
        levelRT.anchorMin     = new Vector2(0f, 1f);
        levelRT.anchorMax     = new Vector2(0f, 1f);
        levelRT.pivot         = new Vector2(0f, 1f);
        levelRT.anchoredPosition = new Vector2(INFO_X, -10f);
        levelRT.sizeDelta     = new Vector2(INFO_W, 32f);

        // ─ 이름 텍스트 ────────────────────────────────────────────────
        var nameText          = CreateTMPText("NameText", rt, "용사", 32f, TextAlignmentOptions.Left);
        nameText.color        = Color.white;
        var nameRT            = nameText.GetComponent<RectTransform>();
        nameRT.anchorMin      = new Vector2(0f, 1f);
        nameRT.anchorMax      = new Vector2(0f, 1f);
        nameRT.pivot          = new Vector2(0f, 1f);
        nameRT.anchoredPosition = new Vector2(INFO_X, -48f);
        nameRT.sizeDelta      = new Vector2(INFO_W, 42f);

        // ─ 경험치 바 ──────────────────────────────────────────────────
        // sizeDelta.x / sizeDelta.y 로 너비·두께 자유롭게 조절
        var expSlider         = CreateExpSlider("ExpBar", rt, new Color(0.20f, 0.55f, 1.00f, 1f));
        var expRT             = expSlider.GetComponent<RectTransform>();
        expRT.anchorMin       = new Vector2(0f, 0f);
        expRT.anchorMax       = new Vector2(0f, 0f);
        expRT.pivot           = new Vector2(0f, 0f);
        expRT.anchoredPosition = new Vector2(INFO_X, 18f);
        expRT.sizeDelta       = new Vector2(INFO_W, 22f);

        // ─ 재화 3개 (우측, 세로 배치) ─────────────────────────────────
        // 각 그룹의 anchoredPosition / sizeDelta 를 Inspector에서 자유롭게 조절
        Color[]  currencyColors = { ColGold, ColGem, new Color(1f, 0.5f, 0.1f, 1f) };
        string[] currencyNames  = { "Currency_Gold", "Currency_Gem", "Currency_Stamina" };
        var currencyIcons       = new Image[3];
        var currencyTexts       = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            var (grp, icon, txt) = CreateCurrencyItem(currencyNames[i], rt, currencyColors[i]);
            grp.anchorMin        = new Vector2(1f, 1f);
            grp.anchorMax        = new Vector2(1f, 1f);
            grp.pivot            = new Vector2(1f, 1f);
            grp.anchoredPosition = new Vector2(-PAD, -10f - i * (CUR_H + CUR_GAP));
            grp.sizeDelta        = new Vector2(CUR_W, CUR_H);
            currencyIcons[i]     = icon;
            currencyTexts[i]     = txt;
        }

        // ─ HomeUserInfoBar 연결 ───────────────────────────────────────
        var infoBar           = rt.gameObject.AddComponent<HomeUserInfoBar>();
        infoBar.avatarImage   = avatarIcon;
        infoBar.levelText     = levelText;
        infoBar.nameText      = nameText;
        infoBar.expSlider     = expSlider;
        infoBar.currencyIcons = currencyIcons;
        infoBar.currencyTexts = currencyTexts;

        return rt.gameObject;
    }

    /// <summary>Unity Slider 계층 구조를 생성합니다 (핸들 없는 진행바 형태).</summary>
    static Slider CreateExpSlider(string name, Transform parent, Color fillColor)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        // 배경
        var bg      = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRT    = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.25f, 1f);

        // Fill Area
        var fillArea   = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRT       = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.1f);
        faRT.anchorMax = new Vector2(1f, 0.9f);
        faRT.offsetMin = new Vector2(2f, 0f);
        faRT.offsetMax = new Vector2(-2f, 0f);

        // Fill
        var fill   = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);  // Slider가 동적으로 조정
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = fillColor;

        // Slider 컴포넌트
        var slider          = go.AddComponent<Slider>();
        slider.fillRect     = fillRT;
        slider.direction    = Slider.Direction.LeftToRight;
        slider.minValue     = 0f;
        slider.maxValue     = 1f;
        slider.value        = 0f;
        slider.interactable = false;   // 표시 전용

        return slider;
    }

    // ────────────────────────────────────────────────────────────────
    // TabBar 빌드
    // ────────────────────────────────────────────────────────────────

    static (RectTransform bar, Button[] buttons) BuildTabBar(RectTransform parent)
    {
        var rt       = CreateRect("TabBar", parent);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = new Vector2(0f, TAB_BAR_H);

        var bg   = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.22f, 1f);

        var hlg                   = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight= true;
        hlg.spacing               = 2f;
        hlg.padding               = new RectOffset(4, 4, 4, 4);

        string[] labels  = { "상점", "대원", "전투", "장비", "기물" };
        var buttons      = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            buttons[i] = CreateTabButton($"Tab_{labels[i]}", rt, labels[i], i == 2);

        return (rt, buttons);
    }

    // ────────────────────────────────────────────────────────────────
    // 전투 패널 빌더 (Panel_전투만 재빌드 — 다른 패널은 건드리지 않음)
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Battle Panel")]
    public static void BuildBattlePanel()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // PanelContainer 찾기
        GameObject container = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "PanelContainer" && go.scene == scene)
            { container = go; break; }
        }
        if (container == null)
        {
            Debug.LogError("[HomeUIBuilder] PanelContainer 를 찾을 수 없습니다. 먼저 Tools/Build Home UI 를 실행하세요.");
            return;
        }

        // 기존 Panel_전투_신규 제거 후 재생성
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Panel_전투_신규" && go.scene == scene)
            { Object.DestroyImmediate(go); break; }
        }

        // Panel_전투_신규 생성
        var panelJeontu = new GameObject("Panel_전투_신규");
        panelJeontu.transform.SetParent(container.transform, false);
        panelJeontu.AddComponent<Image>().color = new Color(0.60f, 0.20f, 0.80f, 1f);
        var panelRT = panelJeontu.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelJeontu.SetActive(true);

        // ── 1. 캐러셀 (ScrollRect 뼈대만 — 카드는 런타임 동적 생성) ──
        var carouselRoot = CreateRect("CarouselRoot", panelJeontu.transform);
        SetAnchors(carouselRoot, new Vector2(0f, 0.30f), Vector2.one, Vector2.zero, Vector2.zero);

        var scrollGO        = new GameObject("StageScroll");
        scrollGO.transform.SetParent(carouselRoot, false);
        scrollGO.AddComponent<RectTransform>();
        var scrollRT        = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin  = Vector2.zero;
        scrollRT.anchorMax  = Vector2.one;
        scrollRT.offsetMin  = Vector2.zero;
        scrollRT.offsetMax  = Vector2.zero;

        var sr              = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal       = true;
        sr.vertical         = false;
        sr.inertia          = false;   // 관성 OFF → 손 뗀 즉시 스냅

        var viewport        = CreateRect("Viewport", scrollGO.transform);
        SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        var content         = CreateRect("Content", viewport);
        content.anchorMin   = new Vector2(0f, 0f);
        content.anchorMax   = new Vector2(0f, 1f);
        content.pivot       = new Vector2(0f, 0.5f);
        content.offsetMin   = Vector2.zero;
        content.offsetMax   = Vector2.zero;

        var hlg                    = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing                = 0f;

        var csf               = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit     = ContentSizeFitter.FitMode.PreferredSize;
        sr.content            = content;

        // ── 2. 도트 컨테이너 (카드 수에 맞게 런타임 생성) ────────────
        var dotRow      = CreateRect("DotRow", panelJeontu.transform);
        SetAnchors(dotRow, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.30f),
                   Vector2.zero, Vector2.zero);
        var dotHLG      = dotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        dotHLG.childAlignment         = TextAnchor.MiddleCenter;
        dotHLG.spacing                = 16f;
        dotHLG.childControlWidth      = true;
        dotHLG.childControlHeight     = true;
        dotHLG.childForceExpandWidth  = false;
        dotHLG.childForceExpandHeight = false;

        // ── 3. 시작 버튼 ──────────────────────────────────────────────
        var startBtnGO = new GameObject("StartButton");
        startBtnGO.transform.SetParent(panelJeontu.transform, false);
        var startBtn   = startBtnGO.AddComponent<Button>();
        startBtnGO.AddComponent<Image>().color = new Color(1f, 0.78f, 0.08f, 1f);
        var startBtnRT = startBtnGO.GetComponent<RectTransform>();
        SetAnchors(startBtnRT, new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.20f),
                   Vector2.zero, Vector2.zero);

        var startLbl = CreateTMPText("Label", startBtnGO.transform, "게임 시작", 52f, TextAlignmentOptions.Center);
        startLbl.color     = Color.white;
        startLbl.fontStyle = FontStyles.Bold;
        var startLblRT     = startLbl.GetComponent<RectTransform>();
        startLblRT.anchorMin = Vector2.zero;
        startLblRT.anchorMax = Vector2.one;
        startLblRT.offsetMin = Vector2.zero;
        startLblRT.offsetMax = Vector2.zero;

        // ── 4. StageSelectPanel 연결 ──────────────────────────────────
        var stagePanel          = panelJeontu.AddComponent<StageSelectPanel>();
        stagePanel.scrollRect   = sr;
        stagePanel.content      = content;
        stagePanel.dotContainer = dotRow;
        stagePanel.startButton  = startBtn;

        UnityEventTools.AddPersistentListener(startBtn.onClick, stagePanel.OnStartClicked);

        panelJeontu.SetActive(false); // 다시 비활성

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Panel_전투_신규 빌드 완료. HomeUIManager의 panels 배열에 수동으로 연결하세요.");
    }

    static Button CreateArrowButton(string name, RectTransform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        var go       = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot     = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img      = go.AddComponent<Image>();
        img.color    = new Color(0f, 0f, 0f, 0.4f);
        var btn      = go.AddComponent<Button>();

        var txt      = CreateTMPText("Label", go.transform, label, 36f, TextAlignmentOptions.Center);
        txt.color    = Color.white;
        var txtRT    = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        return btn;
    }

    // ────────────────────────────────────────────────────────────────
    // 장비 패널 빌더
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Equipment Panel")]
    public static void BuildEquipmentPanel()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        GameObject panelJangbi = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "Panel_장비" && go.scene == scene) { panelJangbi = go; break; }

        if (panelJangbi == null)
        {
            Debug.LogError("[HomeUIBuilder] Panel_장비 를 찾을 수 없습니다. 먼저 Tools/Build Home UI 를 실행하세요.");
            return;
        }
        panelJangbi.SetActive(true);
        while (panelJangbi.transform.childCount > 0)
            Object.DestroyImmediate(panelJangbi.transform.GetChild(0).gameObject);

        // ── 1. 캐릭터 뷰 (상단 55%) ──────────────────────────────────
        // 전체 배경
        var charView = CreateRect("CharacterView", panelJangbi.transform);
        SetAnchors(charView, new Vector2(0f, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);
        charView.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.55f, 0.10f, 1f); // 주황

        // 스탯 바 (상단 중앙) — ATK | HP
        var statsBar = CreateRect("StatsBar", charView);
        SetAnchors(statsBar, new Vector2(0.15f, 0.85f), new Vector2(0.85f, 1f), Vector2.zero, Vector2.zero);
        var statsHLG = statsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        statsHLG.childControlWidth = statsHLG.childControlHeight = true;
        statsHLG.childForceExpandWidth = statsHLG.childForceExpandHeight = true;
        statsHLG.spacing = 12; statsHLG.padding = new RectOffset(8, 8, 4, 4);

        // ATK 그룹
        var atkGrp = CreateRect("ATK", statsBar);
        var atkHLG = atkGrp.gameObject.AddComponent<HorizontalLayoutGroup>();
        atkHLG.childControlWidth = atkHLG.childControlHeight = true;
        atkHLG.childForceExpandWidth = atkHLG.childForceExpandHeight = true;
        atkHLG.spacing = 6; atkHLG.padding = new RectOffset(4,4,2,2);
        var atkBg = atkGrp.gameObject.AddComponent<Image>();
        atkBg.color = new Color(0.15f, 0.15f, 0.20f, 0.85f);
        var atkIconRT = CreateRect("Icon", atkGrp);
        atkIconRT.gameObject.AddComponent<Image>().color = new Color(0.6f, 0.8f, 1f, 1f);
        var atkLE = atkIconRT.gameObject.AddComponent<LayoutElement>(); atkLE.preferredWidth = 36; atkLE.preferredHeight = 36;
        var attackText = CreateTMPText("Value", atkGrp, "—", 30f, TextAlignmentOptions.Center);
        attackText.color = Color.white; attackText.fontStyle = FontStyles.Bold;

        // HP 그룹
        var hpGrp = CreateRect("HP", statsBar);
        var hpHLG = hpGrp.gameObject.AddComponent<HorizontalLayoutGroup>();
        hpHLG.childControlWidth = hpHLG.childControlHeight = true;
        hpHLG.childForceExpandWidth = hpHLG.childForceExpandHeight = true;
        hpHLG.spacing = 6; hpHLG.padding = new RectOffset(4,4,2,2);
        var hpBg = hpGrp.gameObject.AddComponent<Image>();
        hpBg.color = new Color(0.15f, 0.15f, 0.20f, 0.85f);
        var hpIconRT = CreateRect("Icon", hpGrp);
        hpIconRT.gameObject.AddComponent<Image>().color = new Color(1f, 0.35f, 0.35f, 1f);
        var hpLE = hpIconRT.gameObject.AddComponent<LayoutElement>(); hpLE.preferredWidth = 36; hpLE.preferredHeight = 36;
        var hpText = CreateTMPText("Value", hpGrp, "—", 30f, TextAlignmentOptions.Center);
        hpText.color = Color.white; hpText.fontStyle = FontStyles.Bold;

        // 장착 슬롯 6개 (좌3 우3) — VerticalLayoutGroup으로 균등 배치
        var leftSlots = CreateRect("SlotsLeft", charView);
        SetAnchors(leftSlots, new Vector2(0f, 0.05f), new Vector2(0.22f, 0.85f), Vector2.zero, Vector2.zero);
        AddVLG(leftSlots.gameObject, 8, new RectOffset(8, 8, 6, 6));

        var rightSlots = CreateRect("SlotsRight", charView);
        SetAnchors(rightSlots, new Vector2(0.78f, 0.05f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero);
        AddVLG(rightSlots.gameObject, 8, new RectOffset(8, 8, 6, 6));

        var equipFrames = new Image[6];
        var equipIcons  = new Image[6];
        for (int i = 0; i < 6; i++)
        {
            var parent = i < 3 ? leftSlots : rightSlots;
            var slot   = new GameObject($"EquipSlot_{i}");
            slot.transform.SetParent(parent, false);

            equipFrames[i]       = slot.AddComponent<Image>();
            equipFrames[i].color = new Color(0.25f, 0.25f, 0.30f, 0.9f);

            // LE: 정사각형 유지
            var le = slot.AddComponent<LayoutElement>();
            le.minWidth = le.minHeight = 10;

            var iconGO   = new GameObject("Icon");
            iconGO.transform.SetParent(slot.transform, false);
            var iconRT   = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.08f, 0.08f);
            iconRT.anchorMax = new Vector2(0.92f, 0.92f);
            iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
            equipIcons[i]         = iconGO.AddComponent<Image>();
            equipIcons[i].enabled = false;
        }

        // 캐릭터 포트레이트 (가운데)
        var charHolder = CreateRect("CharacterHolder", charView);
        SetAnchors(charHolder, new Vector2(0.20f, 0.05f), new Vector2(0.80f, 0.88f), Vector2.zero, Vector2.zero);
        charHolder.gameObject.AddComponent<Image>().color = Color.clear;

        var charIcon = CreateRect("CharacterIcon", charHolder);
        SetAnchors(charIcon, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        charIcon.gameObject.AddComponent<Image>().color = new Color(1f,1f,1f,0f); // 스프라이트 연결용

        var pedestal = CreateRect("Pedestal", charHolder);
        SetAnchors(pedestal, new Vector2(0f, 0f), new Vector2(1f, 0.15f), Vector2.zero, Vector2.zero);
        pedestal.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.38f, 0.15f, 0.8f);

        // ── 2. 탭 바 (등급별 · 나의 장비 · 합성) ─────────────────────
        var tabBar = CreateRect("TabBar", panelJangbi.transform);
        SetAnchors(tabBar, new Vector2(0f, 0.39f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
        tabBar.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 1f);

        var tabHLG = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabHLG.childControlWidth = tabHLG.childControlHeight = true;
        tabHLG.childForceExpandWidth = tabHLG.childForceExpandHeight = true;
        tabHLG.spacing = 2; tabHLG.padding = new RectOffset(4, 4, 3, 3);

        var tabGradeBtn   = CreateTabButton("Tab_등급별",  tabBar, "등급별",  false);
        var tabMyEquipBtn = CreateTabButton("Tab_나의장비", tabBar, "나의 장비", true);
        var tabSynthBtn   = CreateTabButton("Tab_합성",    tabBar, "합성",    false);

        // ── 3. 인벤토리 스크롤 (하단 39%) ────────────────────────────
        var scrollGO  = new GameObject("InventoryScroll");
        scrollGO.transform.SetParent(panelJangbi.transform, false);
        var scrollRT  = scrollGO.AddComponent<RectTransform>();
        SetAnchors(scrollRT, Vector2.zero, new Vector2(1f, 0.39f), Vector2.zero, Vector2.zero);
        scrollGO.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 1f);

        var sr        = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        var viewport  = CreateRect("Viewport", scrollGO.transform);
        SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        var myEquipContent = CreateGridContent("MyEquipContent", viewport);
        var gradeContent   = CreateGridContent("GradeContent",   viewport);
        var synthContent   = CreateGridContent("SynthContent",   viewport);
        gradeContent.gameObject.SetActive(false);
        synthContent.gameObject.SetActive(false);
        sr.content = myEquipContent;

        for (int i = 0; i < 12; i++)
            CreateDummyItemSlot($"Slot_{i}", myEquipContent);

        // ── 4. HomeEquipPanel 연결 ────────────────────────────────────
        var equipPanel            = panelJangbi.AddComponent<HomeEquipPanel>();
        equipPanel.equipFrames    = equipFrames;
        equipPanel.equipIcons     = equipIcons;
        equipPanel.attackText     = attackText;
        equipPanel.hpText         = hpText;
        equipPanel.tabGrade       = tabGradeBtn;
        equipPanel.tabMyEquip     = tabMyEquipBtn;
        equipPanel.tabSynth       = tabSynthBtn;
        equipPanel.myEquipContent = myEquipContent;
        equipPanel.gradeContent   = gradeContent;
        equipPanel.synthContent   = synthContent;

        panelJangbi.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Equipment Panel 빌드 완료.");
    }

    // ────────────────────────────────────────────────────────────────
    // 유틸리티
    // ────────────────────────────────────────────────────────────────

    /// <summary>전체 앵커 스트레치 RectTransform 생성.</summary>
    static RectTransform CreateStretch(string name, Transform parent)
    {
        var rt       = CreateRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    static void AddVLG(GameObject go, float spacing, RectOffset padding)
    {
        var vlg                    = go.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = true;
        vlg.spacing                = spacing;
        vlg.padding                = padding;
    }

    static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        return tmp;
    }

    /// <summary>아이콘 + 텍스트 재화 그룹 생성. LayoutGroup 없이 앵커로 배치 → 자유 편집 가능.</summary>
    static (RectTransform group, Image icon, TextMeshProUGUI amountText) CreateCurrencyItem(string name, Transform parent, Color iconColor)
    {
        var rt = CreateRect(name, parent);
        // 그룹 자체는 LayoutGroup 없음 — 부모가 직접 sizeDelta 설정

        // 아이콘 (왼쪽 고정, 정사각형)
        // sizeDelta.x/y 로 크기 조절
        var iconRT          = CreateRect("Icon", rt);
        iconRT.anchorMin    = new Vector2(0f, 0.5f);
        iconRT.anchorMax    = new Vector2(0f, 0.5f);
        iconRT.pivot        = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = Vector2.zero;
        iconRT.sizeDelta    = new Vector2(40f, 40f);
        var iconImg         = iconRT.gameObject.AddComponent<Image>();
        iconImg.color       = iconColor;

        // 텍스트 (아이콘 오른쪽, 나머지 너비 채움)
        // anchoredPosition.x 로 아이콘과의 간격 조절
        var txt             = CreateTMPText("Amount", rt, "0", 28f, TextAlignmentOptions.Left);
        txt.color           = Color.white;
        var txtRT           = txt.GetComponent<RectTransform>();
        txtRT.anchorMin     = new Vector2(0f, 0f);
        txtRT.anchorMax     = new Vector2(1f, 1f);
        txtRT.offsetMin     = new Vector2(48f, 0f);   // 아이콘 너비(40) + 간격(8)
        txtRT.offsetMax     = Vector2.zero;

        return (rt, iconImg, txt);
    }

    static Button CreateTabButton(string name, RectTransform parent, string label, bool active)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img   = go.AddComponent<Image>();
        img.color = active ? ColTabActive : ColTabInactive;
        var btn   = go.AddComponent<Button>();

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var rt       = textGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp        = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text       = label;
        tmp.fontSize   = 32f;
        tmp.fontStyle  = FontStyles.Bold;
        tmp.alignment  = TextAlignmentOptions.Center;
        tmp.color      = Color.white;

        return btn;
    }

    static void AddLabel(Transform parent, string text)
    {
        var labelGO = new GameObject("PanelLabel");
        labelGO.transform.SetParent(parent, false);
        var rt       = labelGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.7f);
        rt.anchorMax = new Vector2(1f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp       = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 80f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }

    static Button AddStartButton(Transform battlePanel)
    {
        var btnGO   = new GameObject("StartButton");
        btnGO.transform.SetParent(battlePanel, false);
        var btn     = btnGO.AddComponent<Button>();
        var btnImg  = btnGO.AddComponent<Image>();
        btnImg.color = new Color(1f, 0.3f, 0.3f, 1f);

        var btnRT        = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin  = new Vector2(0.25f, 0.4f);
        btnRT.anchorMax  = new Vector2(0.75f, 0.6f);
        btnRT.offsetMin  = Vector2.zero;
        btnRT.offsetMax  = Vector2.zero;

        var txt      = CreateTMPText("Text", btnGO.transform, "게임 시작", 48f, TextAlignmentOptions.Center);
        var txtRT    = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        txt.color = Color.white;
        txt.fontStyle = FontStyles.Bold;

        return btn;
    }

    // ────────────────────────────────────────────────────────────────
    // 대원 패널 빌더
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Member Panel")]
    public static void BuildMemberPanel()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        GameObject panelDaewon = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "Panel_대원" && go.scene == scene) { panelDaewon = go; break; }

        if (panelDaewon == null)
        {
            Debug.LogError("[HomeUIBuilder] Panel_대원 을 찾을 수 없습니다. 먼저 Tools/Build Home UI 를 실행하세요.");
            return;
        }
        panelDaewon.SetActive(true);
        while (panelDaewon.transform.childCount > 0)
            Object.DestroyImmediate(panelDaewon.transform.GetChild(0).gameObject);

        // ── 1. 상단 캐릭터 뷰 (상단 52%) ─────────────────────────────
        var charView = CreateRect("CharacterView", panelDaewon.transform);
        SetAnchors(charView, new Vector2(0f, 0.48f), Vector2.one, Vector2.zero, Vector2.zero);
        charView.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.45f, 0.70f, 1f); // 파란 배경

        // ── 1-a. 좌측 사이드바 (의상 슬롯) — 너비 10% ────────────────
        var sidebar = CreateRect("Sidebar", charView);
        SetAnchors(sidebar, Vector2.zero, new Vector2(0.12f, 1f), Vector2.zero, Vector2.zero);
        sidebar.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.25f, 0.45f, 0.8f);

        var cosSlotGO = new GameObject("CostumeSlot");
        cosSlotGO.transform.SetParent(sidebar, false);
        var cosRT      = cosSlotGO.AddComponent<RectTransform>();
        cosRT.anchorMin = new Vector2(0.1f, 0.70f);
        cosRT.anchorMax = new Vector2(0.9f, 0.90f);
        cosRT.offsetMin = cosRT.offsetMax = Vector2.zero;
        cosSlotGO.AddComponent<Image>().color = new Color(0.35f, 0.40f, 0.60f, 1f);
        cosSlotGO.AddComponent<Button>();

        var cosLbl = CreateTMPText("Label", cosSlotGO.transform, "의상", 22f, TextAlignmentOptions.Center);
        cosLbl.color = Color.white;
        var cosLblRT = cosLbl.GetComponent<RectTransform>();
        cosLblRT.anchorMin = new Vector2(0f, -0.5f);
        cosLblRT.anchorMax = new Vector2(1f,  0f);
        cosLblRT.offsetMin = cosLblRT.offsetMax = Vector2.zero;

        // ── 1-b. 캐릭터 포트레이트 (좌측 12~48%) ─────────────────────
        var portraitArea = CreateRect("PortraitArea", charView);
        SetAnchors(portraitArea, new Vector2(0.12f, 0f), new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);

        var charPortrait = CreateRect("CharacterPortrait", portraitArea);
        SetAnchors(charPortrait, new Vector2(0f, 0.15f), Vector2.one, Vector2.zero, Vector2.zero);
        var portraitImg = charPortrait.gameObject.AddComponent<Image>();
        portraitImg.color = new Color(1f, 1f, 1f, 0f); // 스프라이트 연결용

        var charNameGO = CreateRect("CharacterName", portraitArea);
        SetAnchors(charNameGO, Vector2.zero, new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
        charNameGO.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var charNameTmp = CreateTMPText("Text", charNameGO, "캐릭터", 38f, TextAlignmentOptions.Center);
        charNameTmp.color     = Color.white;
        charNameTmp.fontStyle = FontStyles.Bold;
        var charNameTmpRT     = charNameTmp.GetComponent<RectTransform>();
        charNameTmpRT.anchorMin = Vector2.zero;
        charNameTmpRT.anchorMax = Vector2.one;
        charNameTmpRT.offsetMin = charNameTmpRT.offsetMax = Vector2.zero;

        // ── 1-c. 스킬 정보 패널 (우측 48~100%) ───────────────────────
        var skillPanel = CreateRect("SkillInfoPanel", charView);
        SetAnchors(skillPanel, new Vector2(0.48f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        skillPanel.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.15f, 0.28f, 0.95f);

        // 스킬 헤더 (아이콘 + 이름 + 정보 버튼)
        var skillHeader = CreateRect("SkillHeader", skillPanel);
        SetAnchors(skillHeader, new Vector2(0f, 0.82f), Vector2.one, Vector2.zero, Vector2.zero);
        var shHLG = skillHeader.gameObject.AddComponent<HorizontalLayoutGroup>();
        shHLG.childControlWidth = shHLG.childControlHeight = true;
        shHLG.childForceExpandWidth = false;
        shHLG.childForceExpandHeight = true;
        shHLG.spacing = 10; shHLG.padding = new RectOffset(12, 12, 8, 8);

        var skillIconRT = CreateRect("SkillIcon", skillHeader);
        var skillIconImg = skillIconRT.gameObject.AddComponent<Image>();
        skillIconImg.color = new Color(0.8f, 0.3f, 0.2f, 1f);
        var siLE = skillIconRT.gameObject.AddComponent<LayoutElement>();
        siLE.preferredWidth = 64; siLE.minWidth = 64;

        var skillNameTmp = CreateTMPText("SkillName", skillHeader, "스킬 이름", 32f, TextAlignmentOptions.Left);
        skillNameTmp.color = Color.white;
        skillNameTmp.fontStyle = FontStyles.Bold;
        skillNameTmp.GetComponent<LayoutElement>()?.ToString(); // ensure LE exists
        var snLE = skillNameTmp.gameObject.AddComponent<LayoutElement>();
        snLE.flexibleWidth = 1;

        var infoBtnGO = new GameObject("InfoButton");
        infoBtnGO.transform.SetParent(skillHeader, false);
        var infoImg = infoBtnGO.AddComponent<Image>();
        infoImg.color = new Color(0.5f, 0.55f, 0.7f, 1f);
        infoBtnGO.AddComponent<Button>();
        var infoLE = infoBtnGO.AddComponent<LayoutElement>();
        infoLE.preferredWidth = 44; infoLE.minWidth = 44;
        var infoTmp = CreateTMPText("i", infoBtnGO.transform, "i", 28f, TextAlignmentOptions.Center);
        infoTmp.color     = Color.white;
        infoTmp.fontStyle = FontStyles.Bold;
        var infoRT        = infoTmp.GetComponent<RectTransform>();
        infoRT.anchorMin  = Vector2.zero;
        infoRT.anchorMax  = Vector2.one;
        infoRT.offsetMin  = infoRT.offsetMax = Vector2.zero;

        // 스킬 레벨 컨테이너
        var skillLevelContainer = CreateRect("SkillLevelContainer", skillPanel);
        SetAnchors(skillLevelContainer, Vector2.zero, new Vector2(1f, 0.82f), Vector2.zero, Vector2.zero);
        var slcVLG = skillLevelContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        slcVLG.childControlWidth = slcVLG.childControlHeight = true;
        slcVLG.childForceExpandWidth = true;
        slcVLG.childForceExpandHeight = false;
        slcVLG.spacing = 6; slcVLG.padding = new RectOffset(10, 10, 8, 8);

        // Lv.1 ~ Lv.4 더미 행
        string[] lvTexts = {
            "HP가 낮을수록 공격력이 높아지며 최대 20% 증가 가능",
            "대미지를 입은 후 5초 내 공격 속도 20% 증가",
            "대미지를 입은 후 10초 내 공격력 20% 증가",
            "HP 50% 미만일 경우 공격 속도 20% 증가"
        };
        Color[] lvColors = {
            new Color(0.25f, 0.45f, 0.80f, 1f),
            new Color(0.20f, 0.35f, 0.20f, 0.9f),
            new Color(0.20f, 0.35f, 0.20f, 0.9f),
            new Color(0.20f, 0.35f, 0.20f, 0.9f),
        };
        for (int lv = 0; lv < 4; lv++)
        {
            var rowGO = new GameObject($"LvRow_{lv + 1}");
            rowGO.transform.SetParent(skillLevelContainer, false);
            rowGO.AddComponent<Image>().color = lvColors[lv];
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 58;

            var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHLG.childControlWidth = rowHLG.childControlHeight = true;
            rowHLG.childForceExpandWidth = false;
            rowHLG.childForceExpandHeight = true;
            rowHLG.spacing = 8; rowHLG.padding = new RectOffset(8, 8, 4, 4);

            var lvBadgeGO = new GameObject("LvBadge");
            lvBadgeGO.transform.SetParent(rowGO.transform, false);
            lvBadgeGO.AddComponent<Image>().color = new Color(0.15f, 0.20f, 0.45f, 1f);
            var lvBadgeLE = lvBadgeGO.AddComponent<LayoutElement>(); lvBadgeLE.minWidth = 52;
            var lvTmp = CreateTMPText("Lv", lvBadgeGO.transform, $"Lv.{lv + 1}", 22f, TextAlignmentOptions.Center);
            lvTmp.color = Color.white; lvTmp.fontStyle = FontStyles.Bold;
            var lvTmpRT = lvTmp.GetComponent<RectTransform>();
            lvTmpRT.anchorMin = Vector2.zero; lvTmpRT.anchorMax = Vector2.one;
            lvTmpRT.offsetMin = lvTmpRT.offsetMax = Vector2.zero;

            var descTmp = CreateTMPText("Desc", rowGO.transform, lvTexts[lv], 20f, TextAlignmentOptions.Left);
            descTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            descTmp.enableWordWrapping = true;
            var descLE = descTmp.gameObject.AddComponent<LayoutElement>(); descLE.flexibleWidth = 1;
        }

        // ── 2. 탭 바 ──────────────────────────────────────────────────
        var tabBar = CreateRect("TabBar", panelDaewon.transform);
        SetAnchors(tabBar, new Vector2(0f, 0.42f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);
        tabBar.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.22f, 1f);

        var tabHLG = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabHLG.childControlWidth = tabHLG.childControlHeight = true;
        tabHLG.childForceExpandWidth = tabHLG.childForceExpandHeight = true;
        tabHLG.spacing = 0;

        var tabCharBtn  = CreateMemberTab("Tab_캐릭터", tabBar, "캐릭터", true);
        var tabEquipBtn = CreateMemberTab("Tab_장비",   tabBar, "장비",   false);
        var tabRuneBtn  = CreateMemberTab("Tab_룬",     tabBar, "룬",    false);

        // ── 3. 캐릭터 스크롤 (하단 42%) ──────────────────────────────
        var scrollGO = new GameObject("CharacterScroll");
        scrollGO.transform.SetParent(panelDaewon.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        SetAnchors(scrollRT, Vector2.zero, new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
        scrollGO.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.18f, 1f);

        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        var viewport = CreateRect("Viewport", scrollGO.transform);
        SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        var charContent = CreateMemberGridContent("CharacterContent", viewport);
        sr.content = charContent;

        for (int i = 0; i < 12; i++)
            CreateCharacterSlot($"CharSlot_{i}", charContent, i == 2);

        // ── 4. HomeMemberPanel 연결 ───────────────────────────────────
        var memberPanel                 = panelDaewon.AddComponent<HomeMemberPanel>();
        memberPanel.characterPortrait   = portraitImg;
        memberPanel.characterNameText   = charNameTmp;
        memberPanel.skillIcon           = skillIconImg;
        memberPanel.skillNameText       = skillNameTmp;
        memberPanel.skillLevelContainer = skillLevelContainer;
        memberPanel.tabCharacter        = tabCharBtn;
        memberPanel.tabEquip            = tabEquipBtn;
        memberPanel.tabRune             = tabRuneBtn;
        memberPanel.characterContent    = charContent;

        panelDaewon.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Member Panel 빌드 완료.");
    }

    static Button CreateMemberTab(string name, RectTransform parent, string label, bool active)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img   = go.AddComponent<Image>();
        img.color = active ? new Color(1f, 0.80f, 0.20f, 1f) : new Color(0.10f, 0.14f, 0.22f, 1f);
        var btn   = go.AddComponent<Button>();

        var lbl = CreateTMPText("Text", go.transform, label, 32f, TextAlignmentOptions.Center);
        lbl.fontStyle = FontStyles.Bold;
        lbl.color     = active ? Color.black : Color.white;
        var lRT        = lbl.GetComponent<RectTransform>();
        lRT.anchorMin  = Vector2.zero;
        lRT.anchorMax  = Vector2.one;
        lRT.offsetMin  = lRT.offsetMax = Vector2.zero;

        return btn;
    }

    static RectTransform CreateMemberGridContent(string name, RectTransform parent)
    {
        var rt       = CreateRect(name, parent);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var grid              = rt.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize         = new Vector2(230, 280);
        grid.spacing          = new Vector2(10, 10);
        grid.padding          = new RectOffset(12, 12, 12, 12);
        grid.childAlignment   = TextAnchor.UpperCenter;
        grid.constraint       = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount  = 4;

        var fitter         = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    static void CreateCharacterSlot(string name, RectTransform parent, bool isSelected)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var bg = go.AddComponent<Image>();
        bg.color = isSelected
            ? new Color(0.20f, 0.55f, 0.90f, 1f)
            : new Color(0.15f, 0.18f, 0.30f, 1f);

        // 테두리 효과 (selected: 밝은 테두리)
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(go.transform, false);
        var borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero; borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(2, 2); borderRT.offsetMax = new Vector2(-2, -2);
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = isSelected ? new Color(0.40f, 0.80f, 1f, 0.4f) : Color.clear;

        // 캐릭터 이미지 영역
        var iconGO = new GameObject("Portrait");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.25f);
        iconRT.anchorMax = new Vector2(1f, 1f);
        iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
        iconGO.AddComponent<Image>().color = new Color(0.25f, 0.30f, 0.50f, 1f);

        // 자물쇠 아이콘 (비선택 캐릭터에만)
        if (!isSelected)
        {
            var lockGO = new GameObject("Lock");
            lockGO.transform.SetParent(go.transform, false);
            var lockRT = lockGO.AddComponent<RectTransform>();
            lockRT.anchorMin = new Vector2(0.72f, 0.70f);
            lockRT.anchorMax = new Vector2(0.97f, 0.97f);
            lockRT.offsetMin = lockRT.offsetMax = Vector2.zero;
            lockGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        }
        else
        {
            // "사용 중" 뱃지
            var badgeGO = new GameObject("Badge_InUse");
            badgeGO.transform.SetParent(go.transform, false);
            var badgeRT = badgeGO.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.15f, 0.70f);
            badgeRT.anchorMax = new Vector2(0.85f, 0.90f);
            badgeRT.offsetMin = badgeRT.offsetMax = Vector2.zero;
            badgeGO.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.22f, 0.85f);
            var badgeTmp = CreateTMPText("Text", badgeGO.transform, "사용 중", 22f, TextAlignmentOptions.Center);
            badgeTmp.color = Color.white;
            var btRT = badgeTmp.GetComponent<RectTransform>();
            btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
            btRT.offsetMin = btRT.offsetMax = Vector2.zero;
        }

        // 하단 조각 카운터 (0/50)
        var countGO = new GameObject("FragmentCount");
        countGO.transform.SetParent(go.transform, false);
        var countRT = countGO.AddComponent<RectTransform>();
        countRT.anchorMin = Vector2.zero;
        countRT.anchorMax = new Vector2(1f, 0.25f);
        countRT.offsetMin = countRT.offsetMax = Vector2.zero;
        countGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        var countTmp = CreateTMPText("Text", countGO.transform, "0/50", 22f, TextAlignmentOptions.Center);
        countTmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var ctRT = countTmp.GetComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one;
        ctRT.offsetMin = ctRT.offsetMax = Vector2.zero;
    }

    static RectTransform CreateGridContent(string name, RectTransform parent)
    {
        var rt        = CreateRect(name, parent);
        rt.anchorMin  = new Vector2(0, 1);
        rt.anchorMax  = new Vector2(1, 1);
        rt.pivot      = new Vector2(0.5f, 1f);
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;

        var grid             = rt.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(170, 170);
        grid.spacing         = new Vector2(12, 12);
        grid.padding         = new RectOffset(16, 16, 16, 16);
        grid.childAlignment  = TextAnchor.UpperCenter;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        var fitter            = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit    = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    static void CreateDummyItemSlot(string name, RectTransform parent)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.4f, 1f);

        var iconGO  = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var rt       = iconGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.1f);
        rt.anchorMax = new Vector2(0.9f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        iconGO.AddComponent<Image>().color = new Color(0.3f, 0.35f, 0.55f, 1f);
    }
}
