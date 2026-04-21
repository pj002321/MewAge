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

    const string SCENE_PATH  = "Assets/씬/home@scene.unity";
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
        var stagePanel          = panelJeontu.AddComponent<MainMenuPanel>();
        stagePanel.scrollRect  = sr;
        stagePanel.content     = content;
        stagePanel.startButton = startBtn;

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
        // 기존 컴포넌트 및 자식 제거
        var oldPanel = panelJangbi.GetComponent<HomeEquipPanel>();
        if (oldPanel != null) Object.DestroyImmediate(oldPanel);
        while (panelJangbi.transform.childCount > 0)
            Object.DestroyImmediate(panelJangbi.transform.GetChild(0).gameObject);

        // ── 1. 캐릭터 뷰 (상단 55%) ──────────────────────────────────
        var charView = CreateRect("CharacterView", panelJangbi.transform);
        SetAnchors(charView, new Vector2(0f, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);
        charView.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.55f, 0.10f, 1f);

        // 장착 슬롯 6개 (좌3 우3)
        var leftSlots = CreateRect("SlotsLeft", charView);
        SetAnchors(leftSlots, new Vector2(0f, 0.05f), new Vector2(0.22f, 0.95f), Vector2.zero, Vector2.zero);
        AddVLG(leftSlots.gameObject, 8, new RectOffset(8, 8, 6, 6));

        var rightSlots = CreateRect("SlotsRight", charView);
        SetAnchors(rightSlots, new Vector2(0.78f, 0.05f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);
        AddVLG(rightSlots.gameObject, 8, new RectOffset(8, 8, 6, 6));

        var equipSlots = new EquipmentSlotView[6];

        for (int i = 0; i < 6; i++)
        {
            var parent  = i < 3 ? leftSlots : rightSlots;
            var slotGO  = new GameObject($"EquipSlot_{i}");
            slotGO.transform.SetParent(parent, false);

            var le = slotGO.AddComponent<LayoutElement>();
            le.minWidth = le.minHeight = 10;

            // 프레임 (배경 Image)
            var frameImg   = slotGO.AddComponent<Image>();
            frameImg.color = new Color(0.25f, 0.25f, 0.30f, 0.9f);
            slotGO.AddComponent<Button>();

            // 카테고리 아이콘
            var catIconGO = new GameObject("CategoryIcon");
            catIconGO.transform.SetParent(slotGO.transform, false);
            var catIconRT      = catIconGO.AddComponent<RectTransform>();
            catIconRT.anchorMin = new Vector2(0.05f, 0.55f);
            catIconRT.anchorMax = new Vector2(0.45f, 0.95f);
            catIconRT.offsetMin = catIconRT.offsetMax = Vector2.zero;
            var catIconImg = catIconGO.AddComponent<Image>();
            catIconImg.enabled = false;

            // 아이콘
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slotGO.transform, false);
            var iconRT      = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.08f, 0.08f);
            iconRT.anchorMax = new Vector2(0.92f, 0.92f);
            iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.enabled = false;

            // 등급 텍스트
            var gradeTxtGO = new GameObject("GradeText");
            gradeTxtGO.transform.SetParent(slotGO.transform, false);
            var gradeTxtRT      = gradeTxtGO.AddComponent<RectTransform>();
            gradeTxtRT.anchorMin = new Vector2(0f, 0f);
            gradeTxtRT.anchorMax = new Vector2(1f, 0.25f);
            gradeTxtRT.offsetMin = gradeTxtRT.offsetMax = Vector2.zero;
            var gradeTmp = gradeTxtGO.AddComponent<TextMeshProUGUI>();
            gradeTmp.fontSize  = 18f;
            gradeTmp.alignment = TextAlignmentOptions.Center;
            gradeTmp.color     = Color.white;

            // 문명 텍스트
            var civTxtGO = new GameObject("CivText");
            civTxtGO.transform.SetParent(slotGO.transform, false);
            var civTxtRT      = civTxtGO.AddComponent<RectTransform>();
            civTxtRT.anchorMin = new Vector2(0f, 0.75f);
            civTxtRT.anchorMax = new Vector2(1f, 1f);
            civTxtRT.offsetMin = civTxtRT.offsetMax = Vector2.zero;
            var civTmp = civTxtGO.AddComponent<TextMeshProUGUI>();
            civTmp.fontSize  = 16f;
            civTmp.alignment = TextAlignmentOptions.Center;
            civTmp.color     = Color.white;

            var sv           = slotGO.AddComponent<EquipmentSlotView>();
            sv.frameImage    = frameImg;
            sv.iconImage     = iconImg;
            sv.categoryIcon  = catIconImg;
            sv.gradeText     = gradeTmp;
            sv.civText       = civTmp;

            equipSlots[i] = sv;
        }

        // 캐릭터 포트레이트 (가운데)
        var charHolder = CreateRect("CharacterHolder", charView);
        SetAnchors(charHolder, new Vector2(0.20f, 0.05f), new Vector2(0.80f, 0.95f), Vector2.zero, Vector2.zero);
        charHolder.gameObject.AddComponent<Image>().color = Color.clear;

        var charIcon = CreateRect("CharacterIcon", charHolder);
        SetAnchors(charIcon, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        charIcon.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

        var pedestal = CreateRect("Pedestal", charHolder);
        SetAnchors(pedestal, new Vector2(0f, 0f), new Vector2(1f, 0.12f), Vector2.zero, Vector2.zero);
        pedestal.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.38f, 0.15f, 0.8f);

        // ── 2. 탭 바 (전체 · 등급순 · 문명순) ───────────────────────
        var tabBar = CreateRect("TabBar", panelJangbi.transform);
        SetAnchors(tabBar, new Vector2(0f, 0.39f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
        tabBar.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 1f);

        var tabHLG = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabHLG.childControlWidth      = tabHLG.childControlHeight     = true;
        tabHLG.childForceExpandWidth  = tabHLG.childForceExpandHeight = true;
        tabHLG.spacing = 2; tabHLG.padding = new RectOffset(4, 4, 3, 3);

        var tabAllBtn     = CreateTabButton("Tab_전체",  tabBar, "전체",  true);
        var tabGradeBtn   = CreateTabButton("Tab_등급순", tabBar, "등급순", false);
        var tabCivBtn     = CreateTabButton("Tab_문명순", tabBar, "문명순", false);

        // ── 3. 인벤토리 스크롤 (하단 39%) ────────────────────────────
        var scrollGO = new GameObject("InventoryScroll");
        scrollGO.transform.SetParent(panelJangbi.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        SetAnchors(scrollRT, Vector2.zero, new Vector2(1f, 0.39f), Vector2.zero, Vector2.zero);
        scrollGO.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 1f);

        var sr        = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        var viewport = CreateRect("Viewport", scrollGO.transform);
        SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        var inventoryContent = CreateGridContent("InventoryContent", viewport);
        sr.content = inventoryContent;

        // ── 4. EquipmentDetailPopup 오버레이 ─────────────────────────
        var popupGO = new GameObject("EquipmentDetailPopup");
        popupGO.transform.SetParent(panelJangbi.transform, false);
        var popupRT = popupGO.AddComponent<RectTransform>();
        popupRT.anchorMin = Vector2.zero;
        popupRT.anchorMax = Vector2.one;
        popupRT.offsetMin = popupRT.offsetMax = Vector2.zero;
        var detailPopup = popupGO.AddComponent<EquipmentDetailPopup>();

        // 배경 딤 (클릭하면 닫힘)
        var backdrop       = popupGO.AddComponent<Image>();
        backdrop.color     = new Color(0f, 0f, 0f, 0.65f);
        var backdropBtn    = popupGO.AddComponent<Button>();
        detailPopup.backdrop = backdropBtn;

        // 중앙 카드
        var cardGO  = new GameObject("Card");
        cardGO.transform.SetParent(popupGO.transform, false);
        var cardRT  = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin        = new Vector2(0.1f, 0.25f);
        cardRT.anchorMax        = new Vector2(0.9f, 0.78f);
        cardRT.offsetMin        = cardRT.offsetMax = Vector2.zero;
        cardGO.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.22f, 1f);

        // 아이콘
        var iconPopupRT = CreateRect("ItemIcon", cardGO.transform);
        SetAnchors(iconPopupRT, new Vector2(0.05f, 0.60f), new Vector2(0.35f, 0.95f), Vector2.zero, Vector2.zero);
        detailPopup.itemIcon = iconPopupRT.gameObject.AddComponent<Image>();
        detailPopup.itemIcon.color = new Color(1f, 1f, 1f, 0f);

        // 이름
        var nameTxt  = CreateTMPText("NameText",  cardGO.transform, "장비 이름", 38f, TextAlignmentOptions.Left);
        var nameRT   = nameTxt.GetComponent<RectTransform>();
        SetAnchors(nameRT, new Vector2(0.38f, 0.78f), new Vector2(0.98f, 0.95f), Vector2.zero, Vector2.zero);
        nameTxt.color = Color.white; nameTxt.fontStyle = FontStyles.Bold;
        detailPopup.nameText = nameTxt;

        // 등급
        var gradeTxt = CreateTMPText("GradeText", cardGO.transform, "레어", 30f, TextAlignmentOptions.Left);
        var gradeRT  = gradeTxt.GetComponent<RectTransform>();
        SetAnchors(gradeRT, new Vector2(0.38f, 0.64f), new Vector2(0.98f, 0.78f), Vector2.zero, Vector2.zero);
        gradeTxt.color = new Color(0.15f, 0.45f, 0.90f, 1f);
        detailPopup.gradeText = gradeTxt;

        // 문명·카테고리
        var civTxt   = CreateTMPText("CivText",   cardGO.transform, "구석기  ·  반지", 26f, TextAlignmentOptions.Left);
        var civRT    = civTxt.GetComponent<RectTransform>();
        SetAnchors(civRT, new Vector2(0.38f, 0.52f), new Vector2(0.98f, 0.64f), Vector2.zero, Vector2.zero);
        civTxt.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        detailPopup.civText = civTxt;

        // 구분선
        var divider     = CreateRect("Divider", cardGO.transform);
        SetAnchors(divider, new Vector2(0.03f, 0.48f), new Vector2(0.97f, 0.49f), Vector2.zero, Vector2.zero);
        divider.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        // 스탯
        var statTxt  = CreateTMPText("StatText",  cardGO.transform, "공격력 +40", 30f, TextAlignmentOptions.Left);
        var statRT   = statTxt.GetComponent<RectTransform>();
        SetAnchors(statRT, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.46f), Vector2.zero, Vector2.zero);
        statTxt.color = new Color(1f, 0.90f, 0.50f, 1f);
        detailPopup.statText = statTxt;

        // 설명
        var descTxt  = CreateTMPText("DescText",  cardGO.transform, "장비 설명", 24f, TextAlignmentOptions.Left);
        var descRT   = descTxt.GetComponent<RectTransform>();
        SetAnchors(descRT, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.30f), Vector2.zero, Vector2.zero);
        descTxt.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        detailPopup.descText = descTxt;

        // 장착/해제 버튼
        var actionGO  = new GameObject("ActionButton");
        actionGO.transform.SetParent(cardGO.transform, false);
        var actionRT  = actionGO.AddComponent<RectTransform>();
        SetAnchors(actionRT, new Vector2(0.15f, 0.01f), new Vector2(0.85f, 0.10f), Vector2.zero, Vector2.zero);
        actionGO.AddComponent<Image>().color = new Color(0.20f, 0.60f, 0.25f, 1f);
        detailPopup.actionButton = actionGO.AddComponent<Button>();
        var actionLbl = CreateTMPText("Label", actionGO.transform, "장착", 32f, TextAlignmentOptions.Center);
        actionLbl.color = Color.white; actionLbl.fontStyle = FontStyles.Bold;
        var lRT = actionLbl.GetComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        detailPopup.actionLabel = actionLbl;

        // 닫기 버튼 (카드 우상단 ×)
        var closeGO  = new GameObject("CloseButton");
        closeGO.transform.SetParent(cardGO.transform, false);
        var closeRT  = closeGO.AddComponent<RectTransform>();
        SetAnchors(closeRT, new Vector2(0.88f, 0.92f), new Vector2(1f, 1.02f), Vector2.zero, Vector2.zero);
        closeGO.AddComponent<Image>().color = new Color(0.7f, 0.15f, 0.15f, 1f);
        detailPopup.closeButton = closeGO.AddComponent<Button>();
        var closeLbl = CreateTMPText("X", closeGO.transform, "✕", 28f, TextAlignmentOptions.Center);
        closeLbl.color = Color.white;
        var cRT = closeLbl.GetComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = cRT.offsetMax = Vector2.zero;

        popupGO.SetActive(false);

        // ── 5. HomeEquipPanel 연결 ─────────────────────────────────────
        var equipPanel              = panelJangbi.AddComponent<HomeEquipPanel>();
        equipPanel.equipSlots       = equipSlots;
        equipPanel.tabAll           = tabAllBtn;
        equipPanel.tabByGrade       = tabGradeBtn;
        equipPanel.tabByCiv         = tabCivBtn;
        equipPanel.inventoryContent = inventoryContent;
        equipPanel.detailPopup      = detailPopup;
        // slotPrefab 은 프리팹 참조이므로 Inspector에서 직접 연결 필요

        panelJangbi.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Equipment Panel 빌드 완료. ※ slotPrefab 은 Inspector에서 직접 연결하세요.");
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
        var portraitImg = charPortrait.gameObject.AddComponent<RawImage>();
        // RawImage는 texture로 연결할 예정

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

        var tabCharBtn = CreateMemberTab("Tab_캐릭터", tabBar, "캐릭터", true);
        var tabRuneBtn = CreateMemberTab("Tab_룬",     tabBar, "룬",    false);

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
        memberPanel.characterPortrait = portraitImg;
        memberPanel.characterNameText = charNameTmp;
        memberPanel.tabCharacter      = tabCharBtn;
        memberPanel.tabRune           = tabRuneBtn;
        memberPanel.characterContent  = charContent;

        panelDaewon.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Member Panel 빌드 완료.");
    }

    // ────────────────────────────────────────────────────────────────
    // 대원 패널 패치 — 기존 구조 유지, 컴포넌트만 추가
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Patch Member Panel")]
    public static void PatchMemberPanel()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // Panel_대원 찾기
        GameObject panel = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "Panel_대원" && go.scene == scene) { panel = go; break; }

        if (panel == null) { Debug.LogError("[Patch] Panel_대원 없음"); return; }

        bool wasActive = panel.activeSelf;
        panel.SetActive(true);

        // ── 1. HomeMemberPanel 컴포넌트 최신화 (중복 방지) ───────────
        var existing = panel.GetComponent<HomeMemberPanel>();
        if (existing != null) Object.DestroyImmediate(existing);
        var mp = panel.AddComponent<HomeMemberPanel>();

        // 탭 버튼 연결 (이름으로 찾기)
        var tabBar = panel.transform.Find("TabBar");
        if (tabBar != null)
        {
            // Tab_장비 제거
            var tabEquip = tabBar.Find("Tab_장비");
            if (tabEquip != null) Object.DestroyImmediate(tabEquip.gameObject);

            mp.tabCharacter = tabBar.Find("Tab_캐릭터")?.GetComponent<Button>();
            mp.tabRune      = tabBar.Find("Tab_룬")?.GetComponent<Button>();
        }

        // ── 2. 캐릭터 Content 스크롤 찾기 ───────────────────────────
        var scrollGO     = panel.transform.Find("CharacterScroll");
        var viewport     = scrollGO?.Find("Viewport");
        var content      = viewport?.Find("CharacterContent");
        mp.characterContent = content;

        // ── 3. 캐릭터 뷰 (포트레이트 RawImage + 이름 텍스트) ───────────
        var charView = panel.transform.Find("CharacterView");
        if (charView != null)
        {
            var portrait = charView.Find("PortraitArea/CharacterPortrait");
            if (portrait != null)
            {
                // 기존 Image 제거하고 RawImage 추가
                var img = portrait.GetComponent<Image>();
                if (img != null) Object.DestroyImmediate(img);
                var rawImg = portrait.gameObject.AddComponent<RawImage>();
                mp.characterPortrait = rawImg;
            }

            var nameGO = charView.Find("PortraitArea/CharacterName/Text");
            if (nameGO != null)
                mp.characterNameText = nameGO.GetComponent<TextMeshProUGUI>();
        }

        // ── 4. 기존 슬롯에 CharacterSlotView + Highlight + ActionBtn ─
        if (content != null)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                var slot = content.GetChild(i);
                PatchCharacterSlot(slot, i == 2); // 3번 슬롯을 isOwned 예시
            }
        }

        panel.SetActive(wasActive);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Patch] Member Panel 패치 완료.");
    }

    static void PatchCharacterSlot(Transform slot, bool isOwned)
    {
        // Button 추가 (없으면)
        if (slot.GetComponent<Button>() == null)
            slot.gameObject.AddComponent<Button>();

        // Highlight (없으면 생성)
        var hlT = slot.Find("Highlight");
        GameObject hlGO;
        if (hlT == null)
        {
            hlGO = new GameObject("Highlight");
            hlGO.transform.SetParent(slot, false);
            var hlRT = hlGO.AddComponent<RectTransform>();
            hlRT.anchorMin = Vector2.zero; hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = new Vector2(2, 2); hlRT.offsetMax = new Vector2(-2, -2);
            hlGO.AddComponent<Image>().color = new Color(0.40f, 0.85f, 1f, 0.35f);
        }
        else hlGO = hlT.gameObject;
        hlGO.SetActive(false);

        // ActionButton (없으면 생성)
        var actT = slot.Find("ActionButton");
        GameObject actGO;
        if (actT == null)
        {
            actGO = new GameObject("ActionButton");
            actGO.transform.SetParent(slot, false);
            var actRT = actGO.AddComponent<RectTransform>();
            actRT.anchorMin = new Vector2(0.05f, 0.28f);
            actRT.anchorMax = new Vector2(0.95f, 0.50f);
            actRT.offsetMin = actRT.offsetMax = Vector2.zero;
            actGO.AddComponent<Image>().color = new Color(0.20f, 0.70f, 0.25f, 1f);
            actGO.AddComponent<Button>();
            var actTmp = actGO.AddComponent<TextMeshProUGUI>();
            // TMP는 별도 GO에 붙여야 하므로 텍스트 GO 생성
            Object.DestroyImmediate(actTmp);
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(actGO.transform, false);
            var textRT    = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
            textRT.offsetMin = textRT.offsetMax = Vector2.zero;
            var tmp        = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text       = isOwned ? "장착" : "구매";
            tmp.fontSize   = 22f;
            tmp.fontStyle  = FontStyles.Bold;
            tmp.alignment  = TextAlignmentOptions.Center;
            tmp.color      = Color.white;
        }
        else actGO = actT.gameObject;
        actGO.SetActive(false);

        // CharacterSlotView (없으면 추가, 있으면 갱신)
        var sv = slot.GetComponent<CharacterSlotView>();
        if (sv == null) sv = slot.gameObject.AddComponent<CharacterSlotView>();
        sv.highlight  = hlGO;
        sv.equipButton = actGO.GetComponent<Button>();
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

    static void CreateCharacterSlot(string name, RectTransform parent, bool isOwned)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.30f, 1f);
        go.AddComponent<Button>();

        // 포트레이트 영역
        var iconGO = new GameObject("Portrait");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.28f);
        iconRT.anchorMax = new Vector2(1f, 1f);
        iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
        iconGO.AddComponent<Image>().color = new Color(0.25f, 0.30f, 0.50f, 1f);

        if (!isOwned)
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
            var badgeGO = new GameObject("Badge_InUse");
            badgeGO.transform.SetParent(go.transform, false);
            var badgeRT = badgeGO.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.1f, 0.72f);
            badgeRT.anchorMax = new Vector2(0.9f, 0.90f);
            badgeRT.offsetMin = badgeRT.offsetMax = Vector2.zero;
            badgeGO.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.22f, 0.85f);
            var btTmp = CreateTMPText("Text", badgeGO.transform, "사용 중", 20f, TextAlignmentOptions.Center);
            btTmp.color = Color.white;
            var btRT    = btTmp.GetComponent<RectTransform>();
            btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
            btRT.offsetMin = btRT.offsetMax = Vector2.zero;
        }

        // 하단 조각 카운터
        var countGO = new GameObject("FragmentCount");
        countGO.transform.SetParent(go.transform, false);
        var countRT = countGO.AddComponent<RectTransform>();
        countRT.anchorMin = Vector2.zero;
        countRT.anchorMax = new Vector2(1f, 0.28f);
        countRT.offsetMin = countRT.offsetMax = Vector2.zero;
        countGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        var countTmp = CreateTMPText("Text", countGO.transform, "0/50", 20f, TextAlignmentOptions.Center);
        countTmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var ctRT = countTmp.GetComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one;
        ctRT.offsetMin = ctRT.offsetMax = Vector2.zero;

        // 하이라이트 오버레이 (기본 비표시)
        var hlGO = new GameObject("Highlight");
        hlGO.transform.SetParent(go.transform, false);
        var hlRT = hlGO.AddComponent<RectTransform>();
        hlRT.anchorMin = Vector2.zero; hlRT.anchorMax = Vector2.one;
        hlRT.offsetMin = new Vector2(2, 2); hlRT.offsetMax = new Vector2(-2, -2);
        hlGO.AddComponent<Image>().color = new Color(0.40f, 0.85f, 1f, 0.35f);
        hlGO.SetActive(false);

        // 액션 버튼 (장착 / 구매, 기본 비표시)
        var actGO = new GameObject("ActionButton");
        actGO.transform.SetParent(go.transform, false);
        var actRT = actGO.AddComponent<RectTransform>();
        actRT.anchorMin = new Vector2(0.05f, 0.28f);
        actRT.anchorMax = new Vector2(0.95f, 0.50f);
        actRT.offsetMin = actRT.offsetMax = Vector2.zero;
        actGO.AddComponent<Image>().color = new Color(0.20f, 0.70f, 0.25f, 1f);
        var actBtn = actGO.AddComponent<Button>();
        var actTmp = CreateTMPText("Text", actGO.transform, isOwned ? "장착" : "구매", 22f, TextAlignmentOptions.Center);
        actTmp.color = Color.white; actTmp.fontStyle = FontStyles.Bold;
        var atRT    = actTmp.GetComponent<RectTransform>();
        atRT.anchorMin = Vector2.zero; atRT.anchorMax = Vector2.one;
        atRT.offsetMin = atRT.offsetMax = Vector2.zero;
        actGO.SetActive(false);

        // CharacterSlotView 연결
        var slotView         = go.AddComponent<CharacterSlotView>();
        slotView.highlight   = hlGO;
        slotView.equipButton = actBtn;
    }

    // ────────────────────────────────────────────────────────────────
    // 상점 패널 빌더
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Shop Panel")]
    public static void BuildShopPanel()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        GameObject panelShop = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "Panel_상점" && go.scene == scene) { panelShop = go; break; }

        if (panelShop == null)
        {
            Debug.LogError("[HomeUIBuilder] Panel_상점 을 찾을 수 없습니다. 먼저 Tools/Build Home UI 를 실행하세요.");
            return;
        }
        panelShop.SetActive(true);
        while (panelShop.transform.childCount > 0)
            Object.DestroyImmediate(panelShop.transform.GetChild(0).gameObject);

        var panelRT = panelShop.GetComponent<RectTransform>();

        // ── 배경 ────────────────────────────────────────────────────
        var bg = CreateRect("BG", panelShop.transform);
        SetAnchors(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.25f, 1f);

        // ── 상단 웨이브 장식 (상단 11%) ─────────────────────────────
        var wave = CreateRect("WaveDecoration", panelShop.transform);
        SetAnchors(wave, new Vector2(0f, 0.89f), Vector2.one, Vector2.zero, Vector2.zero);
        wave.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.60f, 0.90f, 1f);
        // 파도 무늬 힌트 레이블
        var waveTitle = CreateTMPText("ShopTitle", wave, "상점", 52f, TextAlignmentOptions.Center);
        waveTitle.color     = Color.white;
        waveTitle.fontStyle = FontStyles.Bold;
        var wtRT             = waveTitle.GetComponent<RectTransform>();
        wtRT.anchorMin       = Vector2.zero;
        wtRT.anchorMax       = Vector2.one;
        wtRT.offsetMin       = wtRT.offsetMax = Vector2.zero;

        // ── 단일 세로 스크롤 (웨이브 아래 전체) ──────────────────────
        var mainScroll = new GameObject("MainScroll");
        mainScroll.transform.SetParent(panelShop.transform, false);
        var msRT = mainScroll.AddComponent<RectTransform>();
        SetAnchors(msRT, Vector2.zero, new Vector2(1f, 0.89f), Vector2.zero, Vector2.zero);

        var msr       = mainScroll.AddComponent<ScrollRect>();
        msr.horizontal = false; msr.vertical = true;

        var msViewport = CreateRect("Viewport", mainScroll.transform);
        SetAnchors(msViewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        msViewport.gameObject.AddComponent<RectMask2D>();
        msr.viewport = msViewport;

        var msContent       = CreateRect("Content", msViewport);
        msContent.anchorMin = new Vector2(0f, 1f);
        msContent.anchorMax = new Vector2(1f, 1f);
        msContent.pivot     = new Vector2(0.5f, 1f);
        msContent.offsetMin = msContent.offsetMax = Vector2.zero;

        var msVLG = msContent.gameObject.AddComponent<VerticalLayoutGroup>();
        msVLG.childControlWidth = msVLG.childControlHeight = true;
        msVLG.childForceExpandWidth = true;
        msVLG.childForceExpandHeight = false;
        msVLG.spacing = 20;
        msVLG.padding = new RectOffset(16, 16, 16, 40);

        var msCSF         = msContent.gameObject.AddComponent<ContentSizeFitter>();
        msCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        msr.content       = msContent;

        // ─ 섹션 순서: 패키지 → 일일상점 → 장비뽑기 → 기물뽑기 → 골드 → 보석 ──
        BuildGachaSectionHeader(msContent, "챕터 패키지");
        BuildChapterPackageCard(msContent);

        BuildDailyShopSection(msContent);

        BuildGachaSectionHeader(msContent, "장비 뽑기");
        BuildGachaCardRow(msContent);

        BuildGachaSectionHeader(msContent, "기물 캡슐");
        BuildWareCapsuleRow(msContent);

        BuildIAPSection(msContent, "골드", new Color(0.15f, 0.40f, 0.70f, 1f), new[]
        {
            ("1,350",    "", "골드 한 줌",    "₩1,100"),
            ("13,500",   "", "골드 더미",     "₩6,600"),
            ("135,000",  "", "골드 무더기",   "₩14,000"),
            ("270,000",  "", "골드 한 주머니","₩29,000"),
            ("700,000",  "", "골드 한 통",    "₩66,000"),
            ("1,500,000","", "골드 한 상자",  "₩149,000"),
        });

        BuildIAPSection(msContent, "보석", new Color(0.50f, 0.20f, 0.80f, 1f), new[]
        {
            ("80",    "추가 증정 80",    "보석 한 줌",    "₩1,100"),
            ("500",   "추가 증정 500",   "보석 더미",     "₩6,600"),
            ("1,200", "추가 증정 1200",  "보석 무더기",   "₩14,000"),
            ("2,500", "추가 증정 2500",  "보석 한 주머니","₩29,000"),
            ("6,500", "추가 증정 6500",  "보석 한 통",    "₩66,000"),
            ("14,000","추가 증정 14000", "보석 한 상자",  "₩149,000"),
        });

        // ── 가챠 결과 팝업 (최상단 오버레이) ────────────────────────
        var popupGO = new GameObject("GachaResultPopup");
        popupGO.transform.SetParent(panelShop.transform, false);
        var popupRT = popupGO.AddComponent<RectTransform>();
        SetAnchors(popupRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 파란 배경
        var popupBG = popupGO.AddComponent<Image>();
        popupBG.color = new Color(0.18f, 0.28f, 0.62f, 0.97f);

        // 닫기 히트 에어리어 (전체, 뒤에 배치)
        var closeHitGO = CreateRect("CloseHitArea", popupGO.transform);
        SetAnchors(closeHitGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        closeHitGO.gameObject.AddComponent<Image>().color = Color.clear;
        var closeBtn = closeHitGO.gameObject.AddComponent<Button>();
        var closeCB  = new UnityEngine.Events.UnityAction(() => {});
        // (닫기는 GachaResultPopup.Awake 에서 바인딩)

        // 등급 필 (상단 중앙)
        var gradePillGO = CreateRect("GradePill", popupGO.transform);
        SetAnchors(gradePillGO, new Vector2(0.25f, 0.72f), new Vector2(0.75f, 0.80f), Vector2.zero, Vector2.zero);
        gradePillGO.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.30f, 0.95f);
        var gradeText = CreateTMPText("GradeText", gradePillGO, "일반", 38f, TextAlignmentOptions.Center);
        gradeText.color     = Color.white;
        gradeText.fontStyle = FontStyles.Bold;
        var gradeRT          = gradeText.GetComponent<RectTransform>();
        gradeRT.anchorMin    = Vector2.zero; gradeRT.anchorMax = Vector2.one;
        gradeRT.offsetMin    = gradeRT.offsetMax = Vector2.zero;

        // 아이템 이름 텍스트
        var itemNameGO = CreateRect("ItemNameText", popupGO.transform);
        SetAnchors(itemNameGO, new Vector2(0.1f, 0.64f), new Vector2(0.9f, 0.72f), Vector2.zero, Vector2.zero);
        var itemNameTmp = itemNameGO.gameObject.AddComponent<TextMeshProUGUI>();
        itemNameTmp.text      = "아이템 이름";
        itemNameTmp.fontSize  = 40f;
        itemNameTmp.fontStyle = FontStyles.Bold;
        itemNameTmp.alignment = TextAlignmentOptions.Center;
        itemNameTmp.color     = Color.white;

        // 아이템 아이콘 카드
        var iconCardGO = CreateRect("ItemIconCard", popupGO.transform);
        SetAnchors(iconCardGO, new Vector2(0.25f, 0.46f), new Vector2(0.75f, 0.64f), Vector2.zero, Vector2.zero);
        iconCardGO.gameObject.AddComponent<Image>().color = new Color(0.45f, 0.48f, 0.55f, 1f);
        var itemIconGO = CreateRect("ItemIcon", iconCardGO);
        SetAnchors(itemIconGO, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero);
        var itemIconImg = itemIconGO.gameObject.AddComponent<Image>();
        itemIconImg.color = Color.white;

        // 보물상자 이미지 영역
        var chestGO = CreateRect("ChestImage", popupGO.transform);
        SetAnchors(chestGO, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.45f), Vector2.zero, Vector2.zero);
        chestGO.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.40f, 0.60f, 0.6f);
        var chestLbl = CreateTMPText("Hint", chestGO, "보물상자", 36f, TextAlignmentOptions.Center);
        chestLbl.color = new Color(1f, 1f, 1f, 0.5f);
        var chestLblRT = chestLbl.GetComponent<RectTransform>();
        chestLblRT.anchorMin = Vector2.zero; chestLblRT.anchorMax = Vector2.one;
        chestLblRT.offsetMin = chestLblRT.offsetMax = Vector2.zero;

        // 뽑기 버튼 (앞에 배치 — CloseHitArea 위에 렌더링되므로 클릭 차단)
        var drawBtnGO = CreateRect("DrawButton", popupGO.transform);
        SetAnchors(drawBtnGO, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.20f), Vector2.zero, Vector2.zero);
        drawBtnGO.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.75f, 0.20f, 1f);
        var drawBtn = drawBtnGO.gameObject.AddComponent<Button>();
        var drawBtnLbl = CreateTMPText("Label", drawBtnGO.transform, "1회 뽑기", 34f, TextAlignmentOptions.Center);
        drawBtnLbl.color     = Color.white;
        drawBtnLbl.fontStyle = FontStyles.Bold;
        var drawLblRT         = drawBtnLbl.GetComponent<RectTransform>();
        drawLblRT.anchorMin   = Vector2.zero; drawLblRT.anchorMax = Vector2.one;
        drawLblRT.offsetMin   = new Vector2(0f, 0f); drawLblRT.offsetMax = new Vector2(0f, 28f);
        var drawCostTmp = CreateTMPText("CostText", drawBtnGO.transform, "x80", 28f, TextAlignmentOptions.Center);
        drawCostTmp.color = new Color(0.80f, 1f, 0.80f, 1f);
        var costRT         = drawCostTmp.GetComponent<RectTransform>();
        costRT.anchorMin   = Vector2.zero; costRT.anchorMax = Vector2.one;
        costRT.offsetMin   = new Vector2(0f, -28f); costRT.offsetMax = Vector2.zero;

        // "빈 곳을 탭하여 닫기" 힌트
        var closeHintGO = CreateRect("CloseHint", popupGO.transform);
        SetAnchors(closeHintGO, new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.10f), Vector2.zero, Vector2.zero);
        var closeHintTmp = closeHintGO.gameObject.AddComponent<TextMeshProUGUI>();
        closeHintTmp.text      = "빈 곳을 탭하여 닫기";
        closeHintTmp.fontSize  = 26f;
        closeHintTmp.alignment = TextAlignmentOptions.Center;
        closeHintTmp.color     = new Color(0.70f, 0.70f, 0.70f, 0.8f);

        // 다중 결과 그리드 (10회 뽑기용, 평소엔 비활성)
        var multiGridGO = CreateRect("MultiResultContent", popupGO.transform);
        SetAnchors(multiGridGO, new Vector2(0.02f, 0.25f), new Vector2(0.98f, 0.70f), Vector2.zero, Vector2.zero);
        var multiGrid = multiGridGO.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        multiGrid.cellSize        = new Vector2(120f, 140f);
        multiGrid.spacing         = new Vector2(8f, 8f);
        multiGrid.padding         = new RectOffset(8, 8, 8, 8);
        multiGrid.constraint      = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        multiGrid.constraintCount = 5;
        multiGridGO.gameObject.SetActive(false);

        // 단일 슬롯 앵커 (팝업 정중앙)
        var singleAnchorGO = CreateRect("SingleSlotAnchor", popupGO.transform);
        SetAnchors(singleAnchorGO, new Vector2(0.3f, 0.30f), new Vector2(0.7f, 0.70f), Vector2.zero, Vector2.zero);

        // GachaResultPopup 컴포넌트 연결
        var popup                  = popupGO.AddComponent<GachaResultPopup>();
        popup.gradeText            = gradeText;
        popup.itemNameText         = itemNameTmp;
        popup.itemIcon             = itemIconImg;
        popup.drawButton           = drawBtn;
        popup.drawCostText         = drawCostTmp;
        popup.closeHitArea         = closeBtn;
        popup.multiResultContent   = multiGridGO;
        popup.chestRect            = chestGO;
        popup.singleSlotAnchor     = singleAnchorGO;
        // popup.slotPrefab — Inspector에서 직접 연결 필요

        popupGO.SetActive(false);

        // ── 기물 결과 팝업 ───────────────────────────────────────────
        var shopFont2 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Font/mona12-Bold SDF.asset");
        var warePopup = BuildWareResultPopup(panelShop.transform, shopFont2);

        // WareDrawButton 전부 찾아서 팝업 직접 연결
        foreach (var wdb in panelShop.GetComponentsInChildren<WareDrawButton>(true))
            wdb.resultPopup = warePopup;

        // ── HomeShopPanel 컴포넌트 연결 (중복 방지) ─────────────────
        var existingShop = panelShop.GetComponent<HomeShopPanel>();
        if (existingShop != null) Object.DestroyImmediate(existingShop);
        var shopPanel        = panelShop.AddComponent<HomeShopPanel>();
        shopPanel.gachaPopup = popup;

        // ── 커스텀 폰트 적용 ────────────────────────────────────────
        var shopFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Font/mona12-Bold SDF.asset");
        if (shopFont != null)
        {
            foreach (var tmp in panelShop.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.font = shopFont;
            Debug.Log("[HomeUIBuilder] 폰트 적용 완료: mona12-Bold SDF");
        }
        else
        {
            Debug.LogWarning("[HomeUIBuilder] 폰트를 찾을 수 없음: Assets/TextMesh Pro/Font/mona12-Bold SDF.asset");
        }

        panelShop.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Shop Panel 빌드 완료.");
    }

    // ── 상점 탭별 ScrollRect 래퍼 ────────────────────────────────────
    static GameObject BuildShopScrollTab(string name, RectTransform parent, System.Action<RectTransform> populate)
    {
        var rootGO = new GameObject(name);
        rootGO.transform.SetParent(parent, false);
        var rootRT = rootGO.AddComponent<RectTransform>();
        SetAnchors(rootRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var sr       = rootGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical   = true;

        var viewport = CreateRect("Viewport", rootGO.transform);
        SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        var content       = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 16;
        vlg.padding = new RectOffset(16, 16, 16, 32);

        var csf         = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = content;

        populate?.Invoke(content);
        return rootGO;
    }

    static GameObject CreatePlaceholderTab(string name, RectTransform parent, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        SetAnchors(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        go.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.22f, 1f);
        var tmp       = CreateTMPText("Label", go.transform, $"[{label}]\n준비중", 48f, TextAlignmentOptions.Center);
        tmp.color     = new Color(0.5f, 0.5f, 0.5f, 1f);
        var tmpRT     = tmp.GetComponent<RectTransform>();
        tmpRT.anchorMin = new Vector2(0f, 0.4f); tmpRT.anchorMax = new Vector2(1f, 0.6f);
        tmpRT.offsetMin = tmpRT.offsetMax = Vector2.zero;
        return go;
    }

    // ── 섹션 헤더 (다이아몬드 데코 + 타이틀 필) ──────────────────────
    static void BuildGachaSectionHeader(RectTransform parent, string title)
    {
        var headerGO = new GameObject($"SectionHeader_{title}");
        headerGO.transform.SetParent(parent, false);
        var headerLE = headerGO.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 72;
        headerGO.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.28f, 0.8f);

        var pill = CreateRect("TitlePill", headerGO.transform);
        SetAnchors(pill, new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f), Vector2.zero, Vector2.zero);
        pill.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.18f, 0.38f, 1f);

        var titleTmp       = CreateTMPText("Title", pill, title, 34f, TextAlignmentOptions.Center);
        titleTmp.color     = Color.white;
        titleTmp.fontStyle = FontStyles.Bold;
        var ttRT            = titleTmp.GetComponent<RectTransform>();
        ttRT.anchorMin      = Vector2.zero; ttRT.anchorMax = Vector2.one;
        ttRT.offsetMin      = ttRT.offsetMax = Vector2.zero;

        // 좌우 다이아몬드 장식
        foreach (var side in new[] { new Vector2(0.02f, 0.2f), new Vector2(0.88f, 0.2f) })
        {
            var deco = CreateRect("Deco", headerGO.transform);
            SetAnchors(deco, side, side + new Vector2(0.10f, 0.60f), Vector2.zero, Vector2.zero);
            deco.gameObject.AddComponent<Image>().color = new Color(0.6f, 0.65f, 0.8f, 0.8f);
        }
    }

    // ── 가챠 카드 2열 행 래퍼 ─────────────────────────────────────────
    static void BuildGachaCardRow(RectTransform parent)
    {
        var rowGO = new GameObject("GachaCardRow");
        rowGO.transform.SetParent(parent, false);
        rowGO.AddComponent<Image>().color = Color.clear;
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 560;

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth       = hlg.childControlHeight       = true;
        hlg.childForceExpandWidth   = hlg.childForceExpandHeight   = true;
        hlg.spacing = 12;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        var rowRT = rowGO.GetComponent<RectTransform>();

        BuildGachaCard(rowRT, "일반 장비 상자",
            "일반 70% / 레어 30%", 80, 800,
            EquipBoxType.Normal);
        BuildGachaCard(rowRT, "레어 장비 상자",
            "레어 50% / 에픽 10%", 200, 2000,
            EquipBoxType.Rare);
    }

    // ── 기물 캡슐 2열 카드 (일반 / 레어) ─────────────────────────────
    static void BuildWareCapsuleRow(RectTransform parent)
    {
        var rowGO = new GameObject("WareCapsuleRow");
        rowGO.transform.SetParent(parent, false);
        rowGO.AddComponent<Image>().color = Color.clear;
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 560;

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        hlg.spacing = 12;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        var rowRT = rowGO.GetComponent<RectTransform>();

        BuildWareCapsuleCard(rowRT,
            "일반 기물 뽑기",
            "현재 챕터 + 이전 챕터",
            "NORMAL 70%  RARE 30%",
            new Color(0.25f, 0.55f, 0.30f, 1f),
            WareBoxType.Normal, 300, 2700);

        BuildWareCapsuleCard(rowRT,
            "레어 기물 뽑기",
            "현재 챕터 + 이전 챕터\n+5% 다음 챕터 선공개",
            "RARE 70%  EPIC 30%",
            new Color(0.50f, 0.18f, 0.72f, 1f),
            WareBoxType.Epic, 800, 7200);
    }

    static void BuildWareCapsuleCard(RectTransform parent, string title, string subDesc,
        string probDesc, Color accentColor, WareBoxType boxType, int cost1, int cost10)
    {
        var cardGO = new GameObject($"WareCard_{boxType}");
        cardGO.transform.SetParent(parent, false);
        cardGO.AddComponent<LayoutElement>();
        cardGO.AddComponent<Image>().color = new Color(0.18f, 0.12f, 0.30f, 1f);

        // 배너 (상단 55%)
        var banner = CreateRect("Banner", cardGO.transform);
        SetAnchors(banner, new Vector2(0f, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);
        banner.gameObject.AddComponent<Image>().color = accentColor;

        var titleTmp = CreateTMPText("Title", banner, title, 30f, TextAlignmentOptions.Center);
        titleTmp.color = Color.white; titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.enableWordWrapping = true;
        var ttRT = titleTmp.GetComponent<RectTransform>();
        ttRT.anchorMin = new Vector2(0.05f, 0.55f); ttRT.anchorMax = Vector2.one;
        ttRT.offsetMin = ttRT.offsetMax = Vector2.zero;

        var subTmp = CreateTMPText("Sub", banner, subDesc, 20f, TextAlignmentOptions.Center);
        subTmp.color = new Color(1f, 1f, 1f, 0.80f);
        subTmp.enableWordWrapping = true;
        var stRT = subTmp.GetComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0.05f, 0.05f); stRT.anchorMax = new Vector2(0.95f, 0.52f);
        stRT.offsetMin = stRT.offsetMax = Vector2.zero;

        // 확률
        var probTmp = CreateTMPText("Prob", cardGO.transform, probDesc, 20f, TextAlignmentOptions.Center);
        probTmp.color = new Color(0.70f, 0.90f, 0.70f, 1f);
        probTmp.enableWordWrapping = true;
        var ptRT = probTmp.GetComponent<RectTransform>();
        ptRT.anchorMin = new Vector2(0.05f, 0.32f); ptRT.anchorMax = new Vector2(0.95f, 0.45f);
        ptRT.offsetMin = ptRT.offsetMax = Vector2.zero;

        // 버튼 행
        var btnRow = CreateRect("BtnRow", cardGO.transform);
        SetAnchors(btnRow, Vector2.zero, new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);
        var bHLG = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        bHLG.childControlWidth = bHLG.childControlHeight = true;
        bHLG.childForceExpandWidth = bHLG.childForceExpandHeight = true;
        bHLG.spacing = 12; bHLG.padding = new RectOffset(20, 20, 12, 12);

        foreach (var (label, cost, count) in new[]
        {
            ($"1회 뽑기\nx{cost1}",   cost1,  1),
            ($"10회 뽑기\nx{cost10}", cost10, 10)
        })
        {
            var btnGO = new GameObject($"DrawBtn_{count}");
            btnGO.transform.SetParent(btnRow, false);
            btnGO.AddComponent<Image>().color = new Color(0.15f, 0.60f, 0.20f, 1f);
            btnGO.AddComponent<Button>();
            var wdb      = btnGO.AddComponent<WareDrawButton>();
            wdb.boxType  = boxType;
            wdb.drawCount = count;
            var btnTmp    = CreateTMPText("Label", btnGO.transform, label, 26f, TextAlignmentOptions.Center);
            btnTmp.color = Color.white; btnTmp.fontStyle = FontStyles.Bold;
            var bRT       = btnTmp.GetComponent<RectTransform>();
            bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
            bRT.offsetMin = bRT.offsetMax = Vector2.zero;
        }
    }

    // ── 가챠 카드 ─────────────────────────────────────────────────────
    static void BuildGachaCard(RectTransform parent, string title, string desc, int cost1, int cost10,
        EquipBoxType boxType = EquipBoxType.Normal)
    {
        var cardGO = new GameObject($"GachaCard_{title}");
        cardGO.transform.SetParent(parent, false);
        cardGO.AddComponent<LayoutElement>();  // 부모 HLG가 크기 제어
        cardGO.AddComponent<Image>().color = new Color(0.18f, 0.12f, 0.45f, 1f);

        // 배너 (왼 60%)
        var banner = CreateRect("Banner", cardGO.transform);
        SetAnchors(banner, Vector2.zero, new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero);
        banner.gameObject.AddComponent<Image>().color = new Color(0.45f, 0.20f, 0.75f, 1f);

        // 정보 영역 (우 40%)
        var info = CreateRect("Info", cardGO.transform);
        SetAnchors(info, new Vector2(0.58f, 0.38f), Vector2.one, Vector2.zero, Vector2.zero);

        var titleTmp       = CreateTMPText("Title", info, title, 30f, TextAlignmentOptions.Left);
        titleTmp.color     = Color.white;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.enableWordWrapping = true;
        var ttRT            = titleTmp.GetComponent<RectTransform>();
        ttRT.anchorMin      = new Vector2(0.05f, 0.65f); ttRT.anchorMax = Vector2.one;
        ttRT.offsetMin      = ttRT.offsetMax = Vector2.zero;

        var descTmp      = CreateTMPText("Desc", info, desc, 22f, TextAlignmentOptions.Left);
        descTmp.color    = new Color(0.60f, 0.90f, 0.50f, 1f);
        descTmp.enableWordWrapping = true;
        var dtRT          = descTmp.GetComponent<RectTransform>();
        dtRT.anchorMin    = new Vector2(0.05f, 0.30f); dtRT.anchorMax = new Vector2(1f, 0.65f);
        dtRT.offsetMin    = dtRT.offsetMax = Vector2.zero;

        // 패티 행 (9회 보장)
        for (int pi = 0; pi < 2; pi++)
        {
            var pityGO = new GameObject($"PityRow_{pi}");
            pityGO.transform.SetParent(info, false);
            var pityRT = pityGO.AddComponent<RectTransform>();
            pityRT.anchorMin = new Vector2(0.05f, 0.05f + pi * 0.12f);
            pityRT.anchorMax = new Vector2(0.98f, 0.05f + pi * 0.12f + 0.10f);
            pityRT.offsetMin = pityRT.offsetMax = Vector2.zero;
            pityGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.30f, 0.8f);
            int pityCnt = pi == 0 ? 9 : 26;
            var pityTmp = CreateTMPText("Text", pityGO.transform, $"{pityCnt}회 내 반드시 에픽 장비 획득", 18f, TextAlignmentOptions.Left);
            pityTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            var ptRT = pityTmp.GetComponent<RectTransform>();
            ptRT.anchorMin = new Vector2(0.04f, 0f); ptRT.anchorMax = Vector2.one;
            ptRT.offsetMin = ptRT.offsetMax = Vector2.zero;
        }

        // 뽑기 버튼 행 (하단)
        var btnRow = CreateRect("BtnRow", cardGO.transform);
        SetAnchors(btnRow, Vector2.zero, new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero);
        var btnHLG = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        btnHLG.childControlWidth = btnHLG.childControlHeight = true;
        btnHLG.childForceExpandWidth = btnHLG.childForceExpandHeight = true;
        btnHLG.spacing = 12; btnHLG.padding = new RectOffset(20, 20, 12, 12);

        foreach (var (label, cost, count) in new[] {
            ($"1회 뽑기\nx{cost1}",  cost1,  1),
            ($"10회 뽑기\nx{cost10}", cost10, 10) })
        {
            var btnGO  = new GameObject("DrawBtn");
            btnGO.transform.SetParent(btnRow, false);
            btnGO.AddComponent<Image>().color = new Color(0.15f, 0.65f, 0.20f, 1f);
            btnGO.AddComponent<Button>();
            var gdb           = btnGO.AddComponent<GachaDrawButton>();
            gdb.drawCount     = count;
            gdb.boxType       = boxType;
            var btnTmp        = CreateTMPText("Label", btnGO.transform, label, 28f, TextAlignmentOptions.Center);
            btnTmp.color      = Color.white; btnTmp.fontStyle = FontStyles.Bold;
            var btnTRT        = btnTmp.GetComponent<RectTransform>();
            btnTRT.anchorMin  = Vector2.zero; btnTRT.anchorMax = Vector2.one;
            btnTRT.offsetMin  = btnTRT.offsetMax = Vector2.zero;
        }
    }

    static void BuildGachaBannerSubTabs(RectTransform parent)
    {
        var tabGO = new GameObject("GachaSeriesTabs");
        tabGO.transform.SetParent(parent, false);
        var tabLE = tabGO.AddComponent<LayoutElement>(); tabLE.preferredHeight = 72;
        var hlg = tabGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        hlg.spacing = 8; hlg.padding = new RectOffset(8, 8, 4, 4);

        string[] series = { "S급 그리핀 시리즈", "S급 오라클 시리즈", "S급 용기사 시리즈" };
        for (int si = 0; si < series.Length; si++)
        {
            var stGO = new GameObject($"SeriesTab_{si}");
            stGO.transform.SetParent(tabGO.transform, false);
            stGO.AddComponent<Image>().color = si == 0
                ? new Color(1f, 0.80f, 0.20f, 1f)
                : new Color(0.18f, 0.22f, 0.38f, 1f);
            stGO.AddComponent<Button>();
            var stTmp = CreateTMPText("Text", stGO.transform, series[si], 22f, TextAlignmentOptions.Center);
            stTmp.color = si == 0 ? Color.black : Color.white;
            stTmp.enableWordWrapping = true;
            var stRT = stTmp.GetComponent<RectTransform>();
            stRT.anchorMin = Vector2.zero; stRT.anchorMax = Vector2.one;
            stRT.offsetMin = new Vector2(4, 2); stRT.offsetMax = new Vector2(-4, -2);
        }
    }

    // ── 일일상점 ─────────────────────────────────────────────────────
    static void BuildChapterPackageCard(RectTransform parent)
    {
        BuildGachaSectionHeader(parent, "챕터 패키지");

        var cardGO = new GameObject("ChapterPackageCard");
        cardGO.transform.SetParent(parent, false);
        var cardLE = cardGO.AddComponent<LayoutElement>(); cardLE.preferredHeight = 280;
        cardGO.AddComponent<Image>().color = new Color(0.75f, 0.22f, 0.22f, 1f);

        // 배지
        var badgeGO = CreateRect("Badge600", cardGO.transform);
        SetAnchors(badgeGO, new Vector2(0.55f, 0.55f), new Vector2(0.80f, 0.95f), Vector2.zero, Vector2.zero);
        badgeGO.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.20f, 0.20f, 1f);
        var badgeTmp = CreateTMPText("Text", badgeGO, "600%\n초특가", 30f, TextAlignmentOptions.Center);
        badgeTmp.color = Color.white; badgeTmp.fontStyle = FontStyles.Bold;
        var btRT = badgeTmp.GetComponent<RectTransform>();
        btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
        btRT.offsetMin = btRT.offsetMax = Vector2.zero;

        // 타이틀
        var titleTmp = CreateTMPText("Title", cardGO.transform, "챕터13 패키지", 46f, TextAlignmentOptions.Left);
        titleTmp.color = Color.white; titleTmp.fontStyle = FontStyles.Bold;
        var tRT = titleTmp.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.04f, 0.55f); tRT.anchorMax = new Vector2(0.55f, 0.95f);
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        // 가격
        var priceTmp = CreateTMPText("Price", cardGO.transform, "₩66,000", 38f, TextAlignmentOptions.Left);
        priceTmp.color = new Color(1f, 0.95f, 0.60f, 1f);
        var pRT = priceTmp.GetComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.04f, 0.05f); pRT.anchorMax = new Vector2(0.55f, 0.30f);
        pRT.offsetMin = pRT.offsetMax = Vector2.zero;

        var buyBtnGO = CreateRect("BuyButton", cardGO.transform);
        SetAnchors(buyBtnGO, new Vector2(0.05f, 0.30f), new Vector2(0.55f, 0.54f), Vector2.zero, Vector2.zero);
        buyBtnGO.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.70f, 0.25f, 1f);
        buyBtnGO.gameObject.AddComponent<Button>();
        var buyTmp = CreateTMPText("Text", buyBtnGO, "구매", 34f, TextAlignmentOptions.Center);
        buyTmp.color = Color.white; buyTmp.fontStyle = FontStyles.Bold;
        var buyTRT = buyTmp.GetComponent<RectTransform>();
        buyTRT.anchorMin = Vector2.zero; buyTRT.anchorMax = Vector2.one;
        buyTRT.offsetMin = buyTRT.offsetMax = Vector2.zero;
    }

    static void BuildDailyShopSection(RectTransform parent)
    {
        BuildGachaSectionHeader(parent, "일일상점");

        // 타이머
        var timerGO = new GameObject("TimerRow");
        timerGO.transform.SetParent(parent, false);
        var timerLE = timerGO.AddComponent<LayoutElement>(); timerLE.preferredHeight = 48;
        var timerTmp = timerGO.AddComponent<TextMeshProUGUI>();
        timerTmp.text = "새로고침까지: 21:06:28";
        timerTmp.fontSize = 28f;
        timerTmp.alignment = TextAlignmentOptions.Center;
        timerTmp.color = new Color(0.75f, 0.75f, 0.85f, 1f);

        // 아이템 그리드
        var gridGO = new GameObject("DailyGrid");
        gridGO.transform.SetParent(parent, false);
        var gridLE = gridGO.AddComponent<LayoutElement>(); gridLE.preferredHeight = 620;
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize       = new Vector2(300, 290);
        grid.spacing        = new Vector2(12, 12);
        grid.padding        = new RectOffset(12, 12, 12, 12);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        string[] dailyItems = { "헬릭스 조각", "속삭이는 자의\n펜던트", "속삭이는 자의\n장궁", "신발 스크롤", "아머 스크롤", "아머 스크롤" };
        int[] discounts = { 0, 0, 10, 40, 20, 50 };
        int[] costs     = { 90, 50, 45, 60, 40, 30 };
        for (int di = 0; di < dailyItems.Length; di++)
            BuildDailyItemCard(gridGO.transform, dailyItems[di], discounts[di], costs[di], di);
    }

    static void BuildDailyItemCard(Transform parent, string itemName, int discount, int gemCost, int slot)
    {
        var cardGO = new GameObject($"DailyItem_{slot}");
        cardGO.transform.SetParent(parent, false);
        cardGO.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.68f, 1f);
        cardGO.AddComponent<Button>();

        if (discount > 0)
        {
            var discGO = new GameObject("Discount");
            discGO.transform.SetParent(cardGO.transform, false);
            var discRT = discGO.AddComponent<RectTransform>();
            discRT.anchorMin = new Vector2(0f, 0.78f); discRT.anchorMax = new Vector2(0.42f, 1f);
            discRT.offsetMin = discRT.offsetMax = Vector2.zero;
            discGO.AddComponent<Image>().color = new Color(0.85f, 0.20f, 0.20f, 1f);
            var discTmp = CreateTMPText("Text", discGO.transform, $"{discount}%\nOff", 20f, TextAlignmentOptions.Center);
            discTmp.color = Color.white; discTmp.fontStyle = FontStyles.Bold;
            var dtRT = discTmp.GetComponent<RectTransform>();
            dtRT.anchorMin = Vector2.zero; dtRT.anchorMax = Vector2.one;
            dtRT.offsetMin = dtRT.offsetMax = Vector2.zero;
        }

        var nameTmp = CreateTMPText("Name", cardGO.transform, itemName, 24f, TextAlignmentOptions.Center);
        nameTmp.color = Color.white; nameTmp.enableWordWrapping = true;
        var nRT = nameTmp.GetComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0f, 0.68f); nRT.anchorMax = Vector2.one;
        nRT.offsetMin = nRT.offsetMax = Vector2.zero;

        var iconGO = CreateRect("Icon", cardGO.transform);
        SetAnchors(iconGO, new Vector2(0.15f, 0.28f), new Vector2(0.85f, 0.68f), Vector2.zero, Vector2.zero);
        iconGO.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.38f, 0.55f, 1f);

        var cntTmp = CreateTMPText("Count", cardGO.transform, "1회 더 구매 가능", 18f, TextAlignmentOptions.Center);
        cntTmp.color = new Color(0.75f, 0.90f, 1f, 1f);
        var cntRT = cntTmp.GetComponent<RectTransform>();
        cntRT.anchorMin = new Vector2(0f, 0.14f); cntRT.anchorMax = new Vector2(1f, 0.28f);
        cntRT.offsetMin = cntRT.offsetMax = Vector2.zero;

        var costGO = CreateRect("CostBar", cardGO.transform);
        SetAnchors(costGO, Vector2.zero, new Vector2(1f, 0.14f), Vector2.zero, Vector2.zero);
        costGO.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.16f, 0.30f, 0.9f);
        var costTmp = CreateTMPText("Cost", costGO, $"x{gemCost}", 26f, TextAlignmentOptions.Center);
        costTmp.color = new Color(0.50f, 1f, 0.50f, 1f); costTmp.fontStyle = FontStyles.Bold;
        var ctRT = costTmp.GetComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one;
        ctRT.offsetMin = ctRT.offsetMax = Vector2.zero;
    }

    // ── IAP 섹션 (보석 / 골드) ─────────────────────────────────────
    static void BuildIAPSection(RectTransform parent, string sectionName, Color cardColor,
        (string amount, string bonus, string prodName, string price)[] items)
    {
        BuildGachaSectionHeader(parent, sectionName);

        int cols = 3;
        int rows = Mathf.CeilToInt(items.Length / (float)cols);
        var gridGO = new GameObject($"IAPGrid_{sectionName}");
        gridGO.transform.SetParent(parent, false);
        var gridLE = gridGO.AddComponent<LayoutElement>(); gridLE.preferredHeight = rows * 340 + (rows - 1) * 12 + 24;
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(306, 330);
        grid.spacing         = new Vector2(12, 12);
        grid.padding         = new RectOffset(12, 12, 12, 12);
        grid.childAlignment  = TextAnchor.UpperCenter;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        foreach (var item in items)
            BuildIAPCard(gridGO.transform, item.amount, item.bonus, item.prodName, item.price, cardColor);
    }

    static void BuildIAPCard(Transform parent, string amount, string bonus, string prodName, string price, Color bgColor)
    {
        var cardGO = new GameObject($"IAPCard_{prodName}");
        cardGO.transform.SetParent(parent, false);
        cardGO.AddComponent<Image>().color = bgColor;
        cardGO.AddComponent<Button>();

        if (!string.IsNullOrEmpty(bonus))
        {
            var bonusGO = CreateRect("BonusBadge", cardGO.transform);
            SetAnchors(bonusGO, new Vector2(0f, 0.82f), Vector2.one, Vector2.zero, Vector2.zero);
            bonusGO.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.65f, 0.20f, 1f);
            var bonusTmp = CreateTMPText("Text", bonusGO, bonus, 20f, TextAlignmentOptions.Center);
            bonusTmp.color = Color.white;
            var btRT = bonusTmp.GetComponent<RectTransform>();
            btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
            btRT.offsetMin = btRT.offsetMax = Vector2.zero;
        }

        var amtTmp       = CreateTMPText("Amount", cardGO.transform, amount, 48f, TextAlignmentOptions.Center);
        amtTmp.color     = new Color(1f, 0.92f, 0.25f, 1f);
        amtTmp.fontStyle = FontStyles.Bold;
        var aRT           = amtTmp.GetComponent<RectTransform>();
        aRT.anchorMin     = new Vector2(0f, 0.58f); aRT.anchorMax = new Vector2(1f, 0.82f);
        aRT.offsetMin     = aRT.offsetMax = Vector2.zero;

        var iconGO = CreateRect("GemIcon", cardGO.transform);
        SetAnchors(iconGO, new Vector2(0.15f, 0.30f), new Vector2(0.85f, 0.58f), Vector2.zero, Vector2.zero);
        iconGO.gameObject.AddComponent<Image>().color = new Color(0.30f, 0.85f, 0.30f, 0.7f);

        var nameTmp      = CreateTMPText("ProdName", cardGO.transform, prodName, 24f, TextAlignmentOptions.Center);
        nameTmp.color    = new Color(0.9f, 0.9f, 0.9f, 1f);
        var pRT           = nameTmp.GetComponent<RectTransform>();
        pRT.anchorMin     = new Vector2(0f, 0.18f); pRT.anchorMax = new Vector2(1f, 0.30f);
        pRT.offsetMin     = pRT.offsetMax = Vector2.zero;

        var priceBG = CreateRect("PriceBar", cardGO.transform);
        SetAnchors(priceBG, Vector2.zero, new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
        priceBG.gameObject.AddComponent<Image>().color = new Color(0.88f, 0.90f, 0.95f, 1f);
        var priceTmp      = CreateTMPText("Price", priceBG, price, 28f, TextAlignmentOptions.Center);
        priceTmp.color    = new Color(0.15f, 0.15f, 0.20f, 1f);
        priceTmp.fontStyle = FontStyles.Bold;
        var prRT           = priceTmp.GetComponent<RectTransform>();
        prRT.anchorMin     = Vector2.zero; prRT.anchorMax = Vector2.one;
        prRT.offsetMin     = prRT.offsetMax = Vector2.zero;
    }

    static Button CreateShopSubTab(string name, RectTransform parent, string label, bool active)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img   = go.AddComponent<Image>();
        img.color = active ? new Color(1f, 0.80f, 0.20f, 1f) : new Color(0.12f, 0.16f, 0.28f, 1f);
        var btn   = go.AddComponent<Button>();

        var lbl = CreateTMPText("Text", go.transform, label, 24f, TextAlignmentOptions.Center);
        lbl.fontStyle = FontStyles.Bold;
        lbl.enableWordWrapping = true;
        lbl.color     = active ? Color.black : Color.white;
        var lRT        = lbl.GetComponent<RectTransform>();
        lRT.anchorMin  = Vector2.zero;
        lRT.anchorMax  = Vector2.one;
        lRT.offsetMin  = lRT.offsetMax = Vector2.zero;
        return btn;
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

    // ════════════════════════════════════════════════════════════════
    // Panel_기물 빌드
    // ════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Build Ware Panel")]
    public static void BuildWarePanel()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        GameObject panelWare = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene == scene && go.name == "Panel_기물") { panelWare = go; break; }

        if (panelWare == null)
        {
            Debug.LogError("[HomeUIBuilder] Panel_기물 을 찾을 수 없습니다. 먼저 Build Home UI 를 실행하세요.");
            return;
        }

        panelWare.SetActive(true);

        // 기존 자식 및 컴포넌트 전부 제거
        var old = panelWare.GetComponent<HomeWarePanel>();
        if (old != null) Object.DestroyImmediate(old);
        while (panelWare.transform.childCount > 0)
            Object.DestroyImmediate(panelWare.transform.GetChild(0).gameObject);

        var font      = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Font/mona12-Bold SDF.asset");
        var ColBg     = new Color(0.08f, 0.10f, 0.18f, 1f);
        var ColCard   = new Color(0.12f, 0.15f, 0.25f, 1f);
        var ColSec    = new Color(0.10f, 0.13f, 0.22f, 1f);
        var ColAccent = new Color(1f,    0.80f, 0.20f, 1f);
        var ColUpg    = new Color(0.20f, 0.70f, 0.30f, 1f);
        var ColDis    = new Color(0.70f, 0.25f, 0.20f, 1f);
        var ColSynth  = new Color(0.35f, 0.20f, 0.65f, 1f);
        var ColToggle = new Color(0.20f, 0.30f, 0.55f, 1f);

        var panelImg = panelWare.GetComponent<Image>() ?? panelWare.AddComponent<Image>();
        panelImg.color = ColBg;

        // ── 1. TopModeBar (패널 제목 + 모드 전환) ─────────────────────
        //   앵커: y = 0.94 ~ 1.0
        var topBarRT = CreateRect("TopModeBar", panelWare.transform);
        SetAnchors(topBarRT, new Vector2(0f, 0.94f), Vector2.one, Vector2.zero, Vector2.zero);
        topBarRT.gameObject.AddComponent<Image>().color = ColSec;

        var panelTitleTMP = CreateTMPText("PanelTitle", topBarRT, "기물", 36f, TextAlignmentOptions.Left);
        panelTitleTMP.color = ColAccent; panelTitleTMP.fontStyle = FontStyles.Bold;
        if (font) panelTitleTMP.font = font;
        SetAnchors(panelTitleTMP.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.1f), new Vector2(0.50f, 0.9f), Vector2.zero, Vector2.zero);

        var toggleGO  = new GameObject("ModeToggleButton");
        toggleGO.transform.SetParent(topBarRT, false);
        var toggleRT  = toggleGO.AddComponent<RectTransform>();
        SetAnchors(toggleRT, new Vector2(0.60f, 0.10f), new Vector2(0.97f, 0.90f), Vector2.zero, Vector2.zero);
        toggleGO.AddComponent<Image>().color = ColToggle;
        var toggleBtn = toggleGO.AddComponent<Button>();
        var toggleLbl = CreateTMPText("Label", toggleGO.transform, "합성 탭", 26f, TextAlignmentOptions.Center);
        toggleLbl.color = Color.white; if (font) toggleLbl.font = font;
        SetAnchors(toggleLbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 2. PresetArea (y=0.62~0.94, 프리셋 모드) ─────────────────
        var presetAreaGO = new GameObject("PresetArea");
        presetAreaGO.transform.SetParent(panelWare.transform, false);
        var paRT = presetAreaGO.AddComponent<RectTransform>();
        SetAnchors(paRT, new Vector2(0f, 0.62f), new Vector2(1f, 0.94f), Vector2.zero, Vector2.zero);
        presetAreaGO.AddComponent<Image>().color = ColCard;

        var presetTitleTMP = CreateTMPText("PresetTitle", presetAreaGO.transform, "전투 프리셋", 28f, TextAlignmentOptions.Left);
        presetTitleTMP.color = ColAccent; if (font) presetTitleTMP.font = font;
        SetAnchors(presetTitleTMP.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.80f), new Vector2(0.65f, 1f), Vector2.zero, Vector2.zero);

        var presetRow = new GameObject("PresetRow");
        presetRow.transform.SetParent(presetAreaGO.transform, false);
        var prRT = presetRow.AddComponent<RectTransform>();
        SetAnchors(prRT, Vector2.zero, new Vector2(1f, 0.80f), new Vector2(12f, 8f), new Vector2(-12f, -4f));
        var prHLG = presetRow.AddComponent<HorizontalLayoutGroup>();
        prHLG.spacing = 10f; prHLG.padding = new RectOffset(8, 8, 6, 6);
        prHLG.childAlignment = TextAnchor.MiddleCenter;
        prHLG.childForceExpandWidth = true; prHLG.childForceExpandHeight = true;
        prHLG.childControlWidth = true;     prHLG.childControlHeight = true;

        var presetSlots = new WareSlotView[4];
        for (int i = 0; i < 4; i++)
        {
            var slotGO = BuildWareSlotGO($"PresetSlot_{i}", presetRow.transform, font, ColCard);
            var sv     = slotGO.AddComponent<WareSlotView>();
            AssignWareSlotRefs(slotGO, sv);
            presetSlots[i] = sv;
        }

        // ── 3. SynthArea (y=0.62~0.94, 합성 모드, 초기 비활성) ────────
        var synthAreaGO = new GameObject("SynthArea");
        synthAreaGO.transform.SetParent(panelWare.transform, false);
        var saRT = synthAreaGO.AddComponent<RectTransform>();
        SetAnchors(saRT, new Vector2(0f, 0.62f), new Vector2(1f, 0.94f), Vector2.zero, Vector2.zero);
        synthAreaGO.AddComponent<Image>().color = new Color(0.09f, 0.07f, 0.17f, 1f);
        synthAreaGO.SetActive(false);

        // 3-a. 합성 헤더 (타이틀 + 시대 필터)
        var synthHeaderRT = CreateRect("SynthHeader", synthAreaGO.transform);
        SetAnchors(synthHeaderRT, new Vector2(0f, 0.85f), Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        synthHeaderRT.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var synthTitleTMP = CreateTMPText("SynthTitle", synthHeaderRT, "합성", 28f, TextAlignmentOptions.Left);
        synthTitleTMP.color = new Color(0.75f, 0.55f, 1f, 1f); synthTitleTMP.fontStyle = FontStyles.Bold;
        if (font) synthTitleTMP.font = font;
        SetAnchors(synthTitleTMP.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(0.35f, 1f), Vector2.zero, Vector2.zero);

        var synthEraText = CreateTMPText("SynthEraFilterText", synthHeaderRT,
            "시대 제한 없음", 20f, TextAlignmentOptions.Right);
        synthEraText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        if (font) synthEraText.font = font;
        SetAnchors(synthEraText.GetComponent<RectTransform>(),
            new Vector2(0.35f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        // 3-b. 재료 슬롯 그리드 (5열 × 2행 = 10칸, 런타임에 slotPrefab 으로 채워짐)
        var synthSlotsGO = new GameObject("SynthSlotsParent");
        synthSlotsGO.transform.SetParent(synthAreaGO.transform, false);
        var ssRT = synthSlotsGO.AddComponent<RectTransform>();
        SetAnchors(ssRT, new Vector2(0f, 0.28f), new Vector2(1f, 0.85f),
            new Vector2(8f, 4f), new Vector2(-8f, -4f));
        var ssGLG = synthSlotsGO.AddComponent<GridLayoutGroup>();
        ssGLG.cellSize        = new Vector2(175f, 110f);
        ssGLG.spacing         = new Vector2(8f, 6f);
        ssGLG.padding         = new RectOffset(6, 6, 4, 4);
        ssGLG.childAlignment  = TextAnchor.UpperCenter;
        ssGLG.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        ssGLG.constraintCount = 5;

        // 3-c. 미리보기 텍스트 (왼쪽)
        var synthPreviewTMP = CreateTMPText("SynthPreviewText", synthAreaGO.transform,
            "카드를 3~10장 선택하세요\n(같은 시대만 가능)", 20f, TextAlignmentOptions.Left);
        synthPreviewTMP.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        synthPreviewTMP.lineSpacing = -12f;
        if (font) synthPreviewTMP.font = font;
        SetAnchors(synthPreviewTMP.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.02f), new Vector2(0.60f, 0.28f), Vector2.zero, Vector2.zero);

        // 3-d. 합성 실행 버튼 (오른쪽)
        var synthConfGO  = new GameObject("SynthConfirmButton");
        synthConfGO.transform.SetParent(synthAreaGO.transform, false);
        var scRT = synthConfGO.AddComponent<RectTransform>();
        SetAnchors(scRT, new Vector2(0.62f, 0.04f), new Vector2(0.98f, 0.27f), Vector2.zero, Vector2.zero);
        synthConfGO.AddComponent<Image>().color = ColSynth;
        var synthConfBtn = synthConfGO.AddComponent<Button>();
        var synthConfLbl = CreateTMPText("Label", synthConfGO.transform, "합성", 26f, TextAlignmentOptions.Center);
        synthConfLbl.color = Color.white; if (font) synthConfLbl.font = font;
        SetAnchors(synthConfLbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 4. SortTabsRow (y=0.56~0.62) ─────────────────────────────
        var sortTabsGO = new GameObject("SortTabsRow");
        sortTabsGO.transform.SetParent(panelWare.transform, false);
        var stRT = sortTabsGO.AddComponent<RectTransform>();
        SetAnchors(stRT, new Vector2(0f, 0.56f), new Vector2(1f, 0.62f), Vector2.zero, Vector2.zero);
        sortTabsGO.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.15f, 1f);
        var stHLG = sortTabsGO.AddComponent<HorizontalLayoutGroup>();
        stHLG.childForceExpandWidth = true; stHLG.childForceExpandHeight = true;
        stHLG.childControlWidth = true;     stHLG.childControlHeight = true;
        stHLG.spacing = 2f; stHLG.padding = new RectOffset(4, 4, 4, 4);

        string[] tabLabels = { "전체", "등급순", "시대순", "역할순" };
        var tabBtns = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            var tGO = new GameObject($"Tab_{tabLabels[i]}");
            tGO.transform.SetParent(sortTabsGO.transform, false);
            tGO.AddComponent<RectTransform>();
            tGO.AddComponent<Image>().color = i == 0 ? ColAccent : new Color(0.25f, 0.25f, 0.25f, 1f);
            var tBtn = tGO.AddComponent<Button>();
            var tLbl = CreateTMPText("Label", tGO.transform, tabLabels[i], 22f, TextAlignmentOptions.Center);
            tLbl.color = i == 0 ? new Color(0.1f, 0.1f, 0.1f, 1f) : Color.white;
            if (font) tLbl.font = font;
            SetAnchors(tLbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tabBtns[i] = tBtn;
        }

        // ── 5. InventoryScroll (y=0~0.56) ────────────────────────────
        var scrollGO = new GameObject("InventoryScroll");
        scrollGO.transform.SetParent(panelWare.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        SetAnchors(scrollRT, Vector2.zero, new Vector2(1f, 0.56f), Vector2.zero, Vector2.zero);
        scrollGO.AddComponent<Image>().color = ColBg;
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        var viewport = CreateRect("Viewport", scrollGO.transform);
        SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;
        var contentRT = CreateGridContent("Content", viewport);
        sr.content = contentRT;

        // ── 6. WareSlotPrefab (비활성, 프리셋·인벤·합성 재료 공용) ─────
        var slotPrefabGO = BuildWareSlotGO("WareSlotPrefab", panelWare.transform, font, ColCard);
        slotPrefabGO.SetActive(false);
        var slotPrefabSV = slotPrefabGO.AddComponent<WareSlotView>();
        AssignWareSlotRefs(slotPrefabGO, slotPrefabSV);

        // ── 7. WareDetailPopup ────────────────────────────────────────
        var popup = BuildWareDetailPopup(panelWare.transform, font, ColCard, ColUpg, ColDis);

        // ── 8. WareSynthResultPopup ───────────────────────────────────
        var synthPopup = BuildWareSynthResultPopup(panelWare.transform, font, slotPrefabSV);

        // ── 9. HomeWarePanel 연결 ─────────────────────────────────────
        var warePanel                   = panelWare.AddComponent<HomeWarePanel>();
        warePanel.presetArea            = presetAreaGO;
        warePanel.presetSlots           = presetSlots;
        warePanel.synthArea             = synthAreaGO;
        warePanel.synthSlotsParent      = synthSlotsGO.transform;
        warePanel.synthConfirmButton    = synthConfBtn;
        warePanel.synthConfirmLabel     = synthConfLbl;
        warePanel.synthPreviewText      = synthPreviewTMP;
        warePanel.synthEraFilterText    = synthEraText;
        warePanel.modeToggleButton      = toggleBtn;
        warePanel.modeToggleLabel       = toggleLbl;
        warePanel.tabAll                = tabBtns[0];
        warePanel.tabByGrade            = tabBtns[1];
        warePanel.tabByEra              = tabBtns[2];
        warePanel.tabByRole             = tabBtns[3];
        warePanel.inventoryContent      = contentRT;
        warePanel.slotPrefab            = slotPrefabSV;
        warePanel.detailPopup           = popup;
        warePanel.synthResultPopup      = synthPopup;

        if (font)
            foreach (var tmp in panelWare.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (tmp.font == null) tmp.font = font;

        panelWare.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Ware Panel 빌드 완료.");
    }

    // ── 시너지 칩 프리팹 ─────────────────────────────────────────────
    static GameObject BuildSynergyChipPrefab(Transform root, TMP_FontAsset font)
    {
        // 씬 내부에 숨김 위치에 생성 (SetActive false)
        var chipGO = new GameObject("SynergyChipPrefab");
        chipGO.transform.SetParent(root, false);
        chipGO.SetActive(false);

        var chipRT = chipGO.AddComponent<RectTransform>();
        chipRT.sizeDelta = new Vector2(240f, 72f);

        var chipLE = chipGO.AddComponent<LayoutElement>();
        chipLE.preferredWidth  = 240f;
        chipLE.preferredHeight =  72f;

        chipGO.AddComponent<Image>().color = new Color(0.15f, 0.20f, 0.35f, 1f);

        var txt = CreateTMPText("Label", chipGO.transform, "구석기  ●●○○\n쿨타임 -15%", 20f, TextAlignmentOptions.Center);
        txt.color = Color.white;
        txt.lineSpacing = -10f;
        if (font) txt.font = font;
        SetAnchors(txt.GetComponent<RectTransform>(), new Vector2(0.05f,0f), new Vector2(0.95f,1f), Vector2.zero, Vector2.zero);

        return chipGO;
    }

    // ── WareSlotView GameObject 생성 ─────────────────────────────────
    static GameObject BuildWareSlotGO(string name, Transform parent, TMP_FontAsset font, Color cardCol)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = 5;

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta     = new Vector2(180f, 220f); // 고정 크기
        rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);

        var le             = go.AddComponent<LayoutElement>();
        le.preferredWidth  = 180f;
        le.preferredHeight = 220f;

        // 배경 이미지 (Root)
        var rootImg = go.AddComponent<Image>();
        rootImg.color = cardCol;
        rootImg.raycastTarget = true;

        // frameImage — 슬롯 테두리 (슬롯 꽉 채움)
        var frameGO = new GameObject("FrameImage");
        frameGO.transform.SetParent(go.transform, false);
        frameGO.layer = 5;
        var frameRT = frameGO.AddComponent<RectTransform>();
        SetAnchors(frameRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fImg = frameGO.AddComponent<Image>();
        fImg.color = new Color(1f,1f,1f,0.3f);
        // fImg.sprite = ... (보통 테두리용 얇은 라인 스프라이트 사용)

        // iconImage — 기물 아이콘 (중앙 상단)
        var iconGO = new GameObject("IconImage");
        iconGO.transform.SetParent(go.transform, false);
        iconGO.layer = 5;
        var iconRT = iconGO.AddComponent<RectTransform>();
        SetAnchors(iconRT, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, Vector2.zero);
        iconRT.sizeDelta = new Vector2(120f, 120f); // 아이콘 고정 크기
        iconGO.AddComponent<Image>().color = Color.white;

        // eraBadge — 좌상단 시대 표시
        var eraBadgeGO = new GameObject("EraBadge");
        eraBadgeGO.transform.SetParent(go.transform, false);
        eraBadgeGO.layer = 5;
        var ebRT = eraBadgeGO.AddComponent<RectTransform>();
        ebRT.anchorMin = new Vector2(0f,1f); ebRT.anchorMax = new Vector2(0f,1f);
        ebRT.pivot = new Vector2(0f,1f);
        ebRT.anchoredPosition = new Vector2(8f,-8f); ebRT.sizeDelta = new Vector2(60f,30f);
        eraBadgeGO.AddComponent<Image>().color = new Color(0.2f,0.2f,0.25f,0.9f);

        var eraTxtGO = new GameObject("EraText");
        eraTxtGO.transform.SetParent(eraBadgeGO.transform, false);
        eraTxtGO.layer = 5;
        SetAnchors(eraTxtGO.AddComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var eraTMP = eraTxtGO.AddComponent<TextMeshProUGUI>();
        eraTMP.text = "구"; eraTMP.fontSize = 20f;
        eraTMP.alignment = TextAlignmentOptions.Center; eraTMP.color = Color.white;
        if (font) eraTMP.font = font;

        // starText — 하단 별점 (강화도)
        var starGO = new GameObject("StarText");
        starGO.transform.SetParent(go.transform, false);
        starGO.layer = 5;
        var starRT = starGO.AddComponent<RectTransform>();
        SetAnchors(starRT, new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), Vector2.zero, Vector2.zero);
        starRT.sizeDelta = new Vector2(160f, 24f);
        var starTMP = starGO.AddComponent<TextMeshProUGUI>();
        starTMP.text = "★"; starTMP.fontSize = 18f;
        starTMP.alignment = TextAlignmentOptions.Center;
        starTMP.color = new Color(1f,0.85f,0.2f,1f);
        if (font) starTMP.font = font;

        // nameText — 기물 이름 (아이콘 아래)
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(go.transform, false);
        nameGO.layer = 5;
        var nameRT = nameGO.AddComponent<RectTransform>();
        SetAnchors(nameRT, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.42f), Vector2.zero, Vector2.zero);
        nameRT.sizeDelta = new Vector2(0f, 28f);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "기물명"; nameTMP.fontSize = 16f;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = Color.white;
        nameTMP.lineSpacing = -20f;
        if (font) nameTMP.font = font;

        // roleText — 역할 (왼쪽)
        var roleGO = new GameObject("RoleText");
        roleGO.transform.SetParent(go.transform, false);
        roleGO.layer = 5;
        var roleRT = roleGO.AddComponent<RectTransform>();
        SetAnchors(roleRT, new Vector2(0.05f, 0.27f), new Vector2(0.5f, 0.27f), Vector2.zero, Vector2.zero);
        roleRT.sizeDelta = new Vector2(0f, 20f);
        var roleTMP = roleGO.AddComponent<TextMeshProUGUI>();
        roleTMP.text = "역할"; roleTMP.fontSize = 14f;
        roleTMP.alignment = TextAlignmentOptions.Left;
        roleTMP.color = new Color(0.7f, 0.9f, 0.7f, 1f);
        if (font) roleTMP.font = font;

        // gradeText — 등급 (오른쪽)
        var gradeGO = new GameObject("GradeText");
        gradeGO.transform.SetParent(go.transform, false);
        gradeGO.layer = 5;
        var gradeRT = gradeGO.AddComponent<RectTransform>();
        SetAnchors(gradeRT, new Vector2(0.5f, 0.27f), new Vector2(0.95f, 0.27f), Vector2.zero, Vector2.zero);
        gradeRT.sizeDelta = new Vector2(0f, 20f);
        var gradeTMP = gradeGO.AddComponent<TextMeshProUGUI>();
        gradeTMP.text = "등급"; gradeTMP.fontSize = 14f;
        gradeTMP.alignment = TextAlignmentOptions.Right;
        gradeTMP.color = new Color(1f, 0.8f, 0.2f, 1f);
        if (font) gradeTMP.font = font;

        // countText — 우상단 수량
        var cntGO = new GameObject("CountText");
        cntGO.transform.SetParent(go.transform, false);
        cntGO.layer = 5;
        var cntRT = cntGO.AddComponent<RectTransform>();
        SetAnchors(cntRT, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        cntRT.anchoredPosition = new Vector2(-10f, -10f);
        cntRT.sizeDelta = new Vector2(80f, 30f);
        var cntTMP = cntGO.AddComponent<TextMeshProUGUI>();
        cntTMP.text = "×1"; cntTMP.fontSize = 20f;
        cntTMP.alignment = TextAlignmentOptions.Right;
        cntTMP.color = new Color(1f,1f,1f,0.8f);
        if (font) cntTMP.font = font;

        go.AddComponent<Button>();
        return go;
    }

    static void AssignWareSlotRefs(GameObject go, WareSlotView sv)
    {
        // Image들을 이름으로 찾기
        var imgs = go.GetComponentsInChildren<Image>(true);
        if (imgs.Length > 0) SetField(sv, "backgroundImage", imgs[0]);
        foreach (var img in imgs)
        {
            if (img.gameObject.name == "FrameImage") SetField(sv, "frameImage", img);
            if (img.gameObject.name == "IconImage")  SetField(sv, "iconImage", img);
        }

        // TMP들을 이름으로 찾기
        var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmps)
        {
            if (tmp.gameObject.name == "EraText")    SetField(sv, "eraText",   tmp);
            if (tmp.gameObject.name == "NameText")   SetField(sv, "nameText",  tmp);
            if (tmp.gameObject.name == "RoleText")   SetField(sv, "roleText",  tmp);
            if (tmp.gameObject.name == "GradeText")  SetField(sv, "gradeText", tmp);
            if (tmp.gameObject.name == "StarText")   SetField(sv, "starText",  tmp);
            if (tmp.gameObject.name == "CountText")  SetField(sv, "countText", tmp);
        }
    }

    static void SetField(object obj, string field, object value)
    {
        var f = obj.GetType().GetField(field,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(obj, value);
    }

    // ── 기물 결과 팝업 빌드 ──────────────────────────────────────────
    static WareResultPopup BuildWareResultPopup(Transform parent, TMP_FontAsset font)
    {
        // 전체 화면 오버레이
        var overlayGO = new GameObject("WareResultPopup");
        overlayGO.transform.SetParent(parent, false);
        var overlayRT = overlayGO.AddComponent<RectTransform>();
        SetAnchors(overlayRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        // 닫기 히트 에어리어 (전체)
        var closeHit = overlayGO.AddComponent<Button>();

        // 타이틀
        var titleTmp = CreateTMPText("TitleText", overlayGO.transform, "기물 획득!", 52f, TextAlignmentOptions.Center);
        titleTmp.color = new Color(1f, 0.85f, 0.20f, 1f);
        titleTmp.fontStyle = FontStyles.Bold;
        if (font) titleTmp.font = font;
        var ttRT = titleTmp.GetComponent<RectTransform>();
        SetAnchors(ttRT, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.92f), Vector2.zero, Vector2.zero);

        // 1회 결과 앵커 (중앙, 230×280 슬롯 1개)
        var singleGO = new GameObject("SingleAnchor");
        singleGO.transform.SetParent(overlayGO.transform, false);
        var singleRT = singleGO.AddComponent<RectTransform>();
        SetAnchors(singleRT, new Vector2(0.3f, 0.38f), new Vector2(0.7f, 0.78f), Vector2.zero, Vector2.zero);

        // 10회 결과 그리드
        var multiGO = new GameObject("MultiContent");
        multiGO.transform.SetParent(overlayGO.transform, false);
        var multiRT = multiGO.AddComponent<RectTransform>();
        SetAnchors(multiRT, new Vector2(0.02f, 0.25f), new Vector2(0.98f, 0.80f), Vector2.zero, Vector2.zero);
        var grid = multiGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(180f, 220f);
        grid.spacing         = new Vector2(10f, 10f);
        grid.padding         = new RectOffset(8, 8, 8, 8);
        grid.childAlignment  = TextAnchor.UpperCenter;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        multiGO.SetActive(false);

        // 닫기 힌트
        var hintTmp = CreateTMPText("CloseHint", overlayGO.transform, "탭하여 닫기", 26f, TextAlignmentOptions.Center);
        hintTmp.color = new Color(0.65f, 0.65f, 0.65f, 0.85f);
        if (font) hintTmp.font = font;
        var htRT = hintTmp.GetComponent<RectTransform>();
        SetAnchors(htRT, new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.25f), Vector2.zero, Vector2.zero);

        // 슬롯 프리팹 (비활성)
        var slotPrefabGO = BuildWareSlotGO("WareSlotPrefab_Result", overlayGO.transform, font,
            new Color(0.12f, 0.15f, 0.25f, 1f));
        slotPrefabGO.SetActive(false);
        var slotPrefabSV = slotPrefabGO.AddComponent<WareSlotView>();
        AssignWareSlotRefs(slotPrefabGO, slotPrefabSV);

        // WareResultPopup 연결
        var warePopup = overlayGO.AddComponent<WareResultPopup>();
        SetField(warePopup, "closeHitArea",  closeHit);
        SetField(warePopup, "titleText",     titleTmp);
        SetField(warePopup, "singleAnchor",  singleGO);
        SetField(warePopup, "multiContent",  multiGO);
        SetField(warePopup, "slotPrefab",    slotPrefabSV);

        if (font)
            foreach (var tmp in overlayGO.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.font = font;

        overlayGO.SetActive(false);
        return warePopup;
    }

    // ── WareDetailPopup 빌드 ─────────────────────────────────────────
    static WareDetailPopup BuildWareDetailPopup(Transform parent, TMP_FontAsset font,
        Color cardCol, Color upgradeCol, Color discardCol)
    {
        // 전체 화면 오버레이
        var overlayGO = new GameObject("WareDetailPopup");
        overlayGO.transform.SetParent(parent, false);
        overlayGO.layer = 5;
        var overlayRT = overlayGO.AddComponent<RectTransform>();
        SetAnchors(overlayRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlayGO.AddComponent<LayoutElement>().ignoreLayout = true;
        overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        overlayGO.SetActive(false);

        // 카드 패널 (화면 중앙)
        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(overlayGO.transform, false);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.04f, 0.08f);
        cardRT.anchorMax = new Vector2(0.96f, 0.92f);
        cardRT.offsetMin = cardRT.offsetMax = Vector2.zero;
        cardGO.AddComponent<Image>().color = cardCol;

        // 닫기 버튼 (우상단)
        var closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(cardGO.transform, false);
        SetAnchors(closeGO.AddComponent<RectTransform>(),
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-76f, -76f), new Vector2(-6f, -6f));
        closeGO.AddComponent<Image>().color = new Color(0.55f, 0.18f, 0.18f, 1f);
        var closeBtn = closeGO.AddComponent<Button>();
        var closeLbl = CreateTMPText("Label", closeGO.transform, "✕", 34f, TextAlignmentOptions.Center);
        closeLbl.color = Color.white; if (font) closeLbl.font = font;
        SetAnchors(closeLbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 아이콘 영역 (좌상단) ─────────────────────────────────────
        var iconFrameGO = new GameObject("FrameImage");
        iconFrameGO.transform.SetParent(cardGO.transform, false);
        SetAnchors(iconFrameGO.AddComponent<RectTransform>(),
            new Vector2(0.04f, 0.62f), new Vector2(0.34f, 0.98f), Vector2.zero, Vector2.zero);
        iconFrameGO.AddComponent<Image>().color = Color.white;

        var iconGO = new GameObject("IconImage");
        iconGO.transform.SetParent(iconFrameGO.transform, false);
        SetAnchors(iconGO.AddComponent<RectTransform>(),
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        iconGO.AddComponent<Image>().color = Color.white;

        // ── 정보 텍스트 (아이콘 오른쪽) ─────────────────────────────
        TMP_Text MakeTxt(string n, Transform p, Vector2 amin, Vector2 amax, float sz,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = CreateTMPText(n, p, n, sz, align);
            t.color = Color.white; if (font) t.font = font;
            SetAnchors(t.GetComponent<RectTransform>(), amin, amax, Vector2.zero, Vector2.zero);
            return t;
        }

        var nameText    = MakeTxt("NameText",    cardGO.transform, new Vector2(0.37f,0.88f), new Vector2(0.92f,0.98f), 34f);
        nameText.fontStyle = FontStyles.Bold;
        var docuText    = MakeTxt("DocuText",    cardGO.transform, new Vector2(0.37f,0.80f), new Vector2(0.92f,0.88f), 19f);
        docuText.color  = new Color(0.75f, 0.75f, 0.75f, 1f);
        var eraRoleText = MakeTxt("EraRoleText", cardGO.transform, new Vector2(0.37f,0.72f), new Vector2(0.92f,0.80f), 22f);
        eraRoleText.color = new Color(0.60f, 0.90f, 1.00f, 1f);
        var levelText   = MakeTxt("LevelText",   cardGO.transform, new Vector2(0.37f,0.63f), new Vector2(0.75f,0.71f), 28f);
        levelText.color = new Color(1f, 0.84f, 0f, 1f); levelText.fontStyle = FontStyles.Bold;
        var countText   = MakeTxt("CountText",   cardGO.transform, new Vector2(0.75f,0.63f), new Vector2(0.92f,0.71f), 22f, TextAlignmentOptions.Right);
        countText.color = new Color(0.80f, 0.80f, 0.80f, 1f);

        // ── 구분선 ───────────────────────────────────────────────────
        void Divider(Vector2 y0, Vector2 y1)
        {
            var d = new GameObject("Divider"); d.transform.SetParent(cardGO.transform, false);
            SetAnchors(d.AddComponent<RectTransform>(),
                new Vector2(0.04f, y0.x), new Vector2(0.96f, y1.x), Vector2.zero, Vector2.zero);
            d.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);
        }
        Divider(new Vector2(0.61f, 0f), new Vector2(0.62f, 0f));

        // ── 수치 ─────────────────────────────────────────────────────
        var statText     = MakeTxt("StatText",     cardGO.transform, new Vector2(0.04f,0.53f), new Vector2(0.96f,0.61f), 24f);
        var nextStatText = MakeTxt("NextStatText", cardGO.transform, new Vector2(0.04f,0.45f), new Vector2(0.96f,0.53f), 22f);
        nextStatText.color = new Color(0.55f, 1f, 0.55f, 1f);

        Divider(new Vector2(0.43f, 0f), new Vector2(0.44f, 0f));

        // ── 프리셋 버튼 4개 ──────────────────────────────────────────
        var presetBtns   = new Button[4];
        var presetLabels = new TMP_Text[4];
        float bw = 0.215f;
        for (int i = 0; i < 4; i++)
        {
            float x0 = 0.04f + i * (bw + 0.013f);
            var bGO = new GameObject($"PresetBtn_{i + 1}");
            bGO.transform.SetParent(cardGO.transform, false);
            SetAnchors(bGO.AddComponent<RectTransform>(),
                new Vector2(x0, 0.31f), new Vector2(x0 + bw, 0.42f), Vector2.zero, Vector2.zero);
            bGO.AddComponent<Image>().color = new Color(0.22f, 0.33f, 0.52f, 1f);
            presetBtns[i] = bGO.AddComponent<Button>();
            var lbl = CreateTMPText("Label", bGO.transform, $"슬롯{i+1}", 20f, TextAlignmentOptions.Center);
            lbl.color = Color.white; if (font) lbl.font = font;
            SetAnchors(lbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            presetLabels[i] = lbl;
        }

        Divider(new Vector2(0.29f, 0f), new Vector2(0.30f, 0f));

        // ── 시너지 텍스트 ────────────────────────────────────────────
        var synergyText = MakeTxt("SynergyText", cardGO.transform,
            new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.29f), 19f);
        synergyText.color = new Color(0.70f, 0.90f, 0.70f, 1f);
        synergyText.lineSpacing = -12f;

        // ── 강화 버튼 ────────────────────────────────────────────────
        var upgGO = new GameObject("UpgradeButton");
        upgGO.transform.SetParent(cardGO.transform, false);
        SetAnchors(upgGO.AddComponent<RectTransform>(),
            new Vector2(0.04f, 0.04f), new Vector2(0.57f, 0.16f), Vector2.zero, Vector2.zero);
        upgGO.AddComponent<Image>().color = upgradeCol;
        var upgradeBtn = upgGO.AddComponent<Button>();
        var upgLbl = CreateTMPText("Label", upgGO.transform, "강화 (0 / 2장)", 24f, TextAlignmentOptions.Center);
        upgLbl.color = Color.white; if (font) upgLbl.font = font;
        SetAnchors(upgLbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 분해 버튼 ────────────────────────────────────────────────
        var disGO = new GameObject("DismantleButton");
        disGO.transform.SetParent(cardGO.transform, false);
        SetAnchors(disGO.AddComponent<RectTransform>(),
            new Vector2(0.60f, 0.04f), new Vector2(0.96f, 0.16f), Vector2.zero, Vector2.zero);
        disGO.AddComponent<Image>().color = discardCol;
        var dismantleBtn = disGO.AddComponent<Button>();
        var disLbl = CreateTMPText("Label", disGO.transform, "분해 +50G", 24f, TextAlignmentOptions.Center);
        disLbl.color = Color.white; if (font) disLbl.font = font;
        SetAnchors(disLbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── WareDetailPopup 컴포넌트 연결 ─────────────────────────────
        var popup = overlayGO.AddComponent<WareDetailPopup>();
        SetField(popup, "iconImage",       iconGO.GetComponent<Image>());
        SetField(popup, "frameImage",      iconFrameGO.GetComponent<Image>());
        SetField(popup, "nameText",        nameText);
        SetField(popup, "docuText",        docuText);
        SetField(popup, "eraRoleText",     eraRoleText);
        SetField(popup, "levelText",       levelText);
        SetField(popup, "countText",       countText);
        SetField(popup, "statText",        statText);
        SetField(popup, "nextStatText",    nextStatText);
        SetField(popup, "upgradeButton",   upgradeBtn);
        SetField(popup, "upgradeLabel",    upgLbl);
        SetField(popup, "dismantleButton", dismantleBtn);
        SetField(popup, "synergyText",     synergyText);
        SetField(popup, "presetButtons",   presetBtns);
        SetField(popup, "presetLabels",    presetLabels);
        SetField(popup, "closeButton",     closeBtn);

        return popup;
    }

    // ════════════════════════════════════════════════════════════════
    // WareSynthResultPopup 빌드
    // ════════════════════════════════════════════════════════════════

    static WareSynthResultPopup BuildWareSynthResultPopup(
        Transform parent, TMP_FontAsset font, WareSlotView slotPrefab)
    {
        var ColBg      = new Color(0f, 0f, 0f, 0.80f);
        var ColCard    = new Color(0.10f, 0.12f, 0.22f, 1f);
        var ColAccent  = new Color(0.75f, 0.55f, 1f, 1f);
        var ColClose   = new Color(0.55f, 0.18f, 0.18f, 1f);

        // 전체화면 반투명 오버레이
        var overlayGO = new GameObject("WareSynthResultPopup");
        overlayGO.transform.SetParent(parent, false);
        overlayGO.layer = 5;
        var overlayRT = overlayGO.AddComponent<RectTransform>();
        SetAnchors(overlayRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlayGO.AddComponent<LayoutElement>().ignoreLayout = true;
        overlayGO.AddComponent<Image>().color = ColBg;
        overlayGO.SetActive(false);

        // 중앙 카드 패널
        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(overlayGO.transform, false);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.05f, 0.12f);
        cardRT.anchorMax = new Vector2(0.95f, 0.93f);
        cardRT.offsetMin = cardRT.offsetMax = Vector2.zero;
        cardGO.AddComponent<Image>().color = ColCard;

        // 타이틀
        var titleTMP = CreateTMPText("TitleText", cardGO.transform, "합성 결과!", 38f, TextAlignmentOptions.Center);
        titleTMP.color = ColAccent; titleTMP.fontStyle = FontStyles.Bold;
        if (font) titleTMP.font = font;
        SetAnchors(titleTMP.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.85f), new Vector2(0.85f, 1.00f), Vector2.zero, Vector2.zero);

        // 닫기 버튼 (우상단)
        var closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(cardGO.transform, false);
        SetAnchors(closeGO.AddComponent<RectTransform>(),
            new Vector2(0.87f, 0.87f), new Vector2(0.99f, 0.99f), Vector2.zero, Vector2.zero);
        closeGO.AddComponent<Image>().color = ColClose;
        var closeBtn = closeGO.AddComponent<Button>();
        var closeLbl = CreateTMPText("Label", closeGO.transform, "✕", 32f, TextAlignmentOptions.Center);
        closeLbl.color = Color.white; if (font) closeLbl.font = font;
        SetAnchors(closeLbl.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // singleSlotAnchor — 단일 결과 표시용 중앙 앵커
        var singleAnchorGO = new GameObject("SingleSlotAnchor");
        singleAnchorGO.transform.SetParent(cardGO.transform, false);
        var saRT = singleAnchorGO.AddComponent<RectTransform>();
        SetAnchors(saRT, new Vector2(0.15f, 0.30f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);

        // resultContent — 다중 결과 GridLayoutGroup
        var contentGO = new GameObject("ResultContent");
        contentGO.transform.SetParent(cardGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        SetAnchors(contentRT, new Vector2(0f, 0.28f), new Vector2(1f, 0.85f),
            new Vector2(8f, 4f), new Vector2(-8f, -4f));
        var glg = contentGO.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(170f, 200f);
        glg.spacing         = new Vector2(10f, 8f);
        glg.padding         = new RectOffset(8, 8, 4, 4);
        glg.childAlignment  = TextAnchor.MiddleCenter;
        glg.constraint      = GridLayoutGroup.Constraint.Flexible;

        // resultSummaryText — 결과 목록 (카드 이름 × 수량)
        var summaryTMP = CreateTMPText("ResultSummaryText", cardGO.transform,
            "", 21f, TextAlignmentOptions.Center);
        summaryTMP.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        summaryTMP.lineSpacing = -10f;
        if (font) summaryTMP.font = font;
        SetAnchors(summaryTMP.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.27f), Vector2.zero, Vector2.zero);

        // WareSynthResultPopup 컴포넌트 연결
        var comp = overlayGO.AddComponent<WareSynthResultPopup>();
        comp.resultContent    = contentRT;
        comp.slotPrefab       = slotPrefab;
        comp.singleSlotAnchor = saRT;
        comp.titleText        = titleTMP;
        comp.resultSummaryText = summaryTMP;
        comp.closeButton      = closeBtn;

        return comp;
    }
}
