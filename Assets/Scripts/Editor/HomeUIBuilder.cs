using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class HomeUIBuilder
{
    // ────────────────────────────────────────────────────────────────
    // 장비 패널 빌더
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Equipment Panel")]
    public static void BuildEquipmentPanel()
    {
        string scenePath = "Assets/_Recovery/Home.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Panel_장비 찾기 (비활성 오브젝트 포함)
        GameObject panelJangbi = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Panel_장비" && go.scene == scene)
            {
                panelJangbi = go;
                break;
            }
        }
        if (panelJangbi == null)
        {
            Debug.LogError("[HomeUIBuilder] Panel_장비 를 찾을 수 없습니다. 먼저 Tools/Build Home UI 를 실행하세요.");
            return;
        }
        panelJangbi.SetActive(true); // 빌드 중 활성화

        // 기존 자식 제거 (PanelLabel 포함)
        while (panelJangbi.transform.childCount > 0)
            GameObject.DestroyImmediate(panelJangbi.transform.GetChild(0).gameObject);

        var panelRect = panelJangbi.GetComponent<RectTransform>();

        // ── 1. 캐릭터 뷰 (상단 55%) ──────────────────────────────────
        var charView = CreateRect("CharacterView", panelJangbi.transform);
        SetAnchors(charView, new Vector2(0, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);

        // 배경
        var charBg = charView.gameObject.AddComponent<Image>();
        charBg.color = new Color(0.08f, 0.12f, 0.22f, 1f);

        // 왼쪽 슬롯 컨테이너 (3개)
        var leftSlots = CreateRect("SlotsLeft", charView);
        SetAnchors(leftSlots, new Vector2(0f, 0.1f), new Vector2(0.3f, 0.95f), Vector2.zero, Vector2.zero);
        var leftVLG = leftSlots.gameObject.AddComponent<VerticalLayoutGroup>();
        leftVLG.childControlWidth  = true;
        leftVLG.childControlHeight = true;
        leftVLG.childForceExpandWidth  = true;
        leftVLG.childForceExpandHeight = true;
        leftVLG.spacing = 10;
        leftVLG.padding = new RectOffset(12, 12, 8, 8);

        // 오른쪽 슬롯 컨테이너 (3개)
        var rightSlots = CreateRect("SlotsRight", charView);
        SetAnchors(rightSlots, new Vector2(0.7f, 0.1f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);
        var rightVLG = rightSlots.gameObject.AddComponent<VerticalLayoutGroup>();
        rightVLG.childControlWidth  = true;
        rightVLG.childControlHeight = true;
        rightVLG.childForceExpandWidth  = true;
        rightVLG.childForceExpandHeight = true;
        rightVLG.spacing = 10;
        rightVLG.padding = new RectOffset(12, 12, 8, 8);

        // 장착 슬롯 6개 생성 (0~2 왼쪽, 3~5 오른쪽)
        var equipFrames = new Image[6];
        var equipIcons  = new Image[6];
        for (int i = 0; i < 6; i++)
        {
            Transform parent = i < 3 ? leftSlots : rightSlots;
            var slotGO = new GameObject($"EquipSlot_{i}");
            slotGO.transform.SetParent(parent, false);

            // 프레임
            var frame = slotGO.AddComponent<Image>();
            frame.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            equipFrames[i] = frame;

            // 아이콘
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slotGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.1f);
            iconRect.anchorMax = new Vector2(0.9f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var icon = iconGO.AddComponent<Image>();
            icon.color   = Color.white;
            icon.enabled = false;
            equipIcons[i] = icon;
        }

        // 캐릭터 홀더 (가운데)
        var charHolder = CreateRect("CharacterHolder", charView);
        SetAnchors(charHolder, new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.95f), Vector2.zero, Vector2.zero);

        var charPlaceholder = charHolder.gameObject.AddComponent<Image>();
        charPlaceholder.color = new Color(0f, 0f, 0f, 0f); // 투명 – 실제 캐릭터로 교체

        // 받침대
        var pedestal = CreateRect("Pedestal", charHolder);
        SetAnchors(pedestal, new Vector2(0.1f, 0f), new Vector2(0.9f, 0.2f), Vector2.zero, Vector2.zero);
        var pedestalImg = pedestal.gameObject.AddComponent<Image>();
        pedestalImg.color = new Color(0.55f, 0.38f, 0.18f, 1f);

        // 캐릭터 플레이스홀더 아이콘
        var charIcon = CreateRect("CharacterIcon", charHolder);
        SetAnchors(charIcon, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 1f), Vector2.zero, Vector2.zero);
        var charIconImg = charIcon.gameObject.AddComponent<Image>();
        charIconImg.color = new Color(0.6f, 0.6f, 0.7f, 0.5f);

        // 스탯 바 (캐릭터뷰 하단)
        var statsBar = CreateRect("StatsBar", charView);
        SetAnchors(statsBar, Vector2.zero, new Vector2(1f, 0.12f), Vector2.zero, Vector2.zero);
        var statsBarBg = statsBar.gameObject.AddComponent<Image>();
        statsBarBg.color = new Color(0.05f, 0.08f, 0.15f, 0.9f);

        var statsHLG = statsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        statsHLG.childControlWidth  = true;
        statsHLG.childControlHeight = true;
        statsHLG.childForceExpandWidth  = true;
        statsHLG.childForceExpandHeight = true;
        statsHLG.padding = new RectOffset(20, 20, 8, 8);
        statsHLG.spacing = 16;

        var attackText  = CreateTMPText("AttackStat", statsBar, "공격력  —", 28, TextAlignmentOptions.Center);
        attackText.color = Color.white;
        var hpText = CreateTMPText("HPStat",     statsBar, "HP  —",     28, TextAlignmentOptions.Center);
        hpText.color = new Color(1f, 0.4f, 0.4f);

        // ── 2. 내부 탭 바 (장비 / 성물) ──────────────────────────────
        var innerTabBar = CreateRect("InnerTabBar", panelJangbi.transform);
        SetAnchors(innerTabBar, new Vector2(0f, 0.39f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
        var innerTabBg = innerTabBar.gameObject.AddComponent<Image>();
        innerTabBg.color = new Color(0.1f, 0.14f, 0.24f, 1f);

        var innerHLG = innerTabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        innerHLG.childControlWidth  = true;
        innerHLG.childControlHeight = true;
        innerHLG.childForceExpandWidth  = true;
        innerHLG.childForceExpandHeight = true;
        innerHLG.spacing = 2;
        innerHLG.padding = new RectOffset(4, 4, 4, 4);

        var tabWeaponBtn   = CreateTabButton("Tab_장비",   innerTabBar, "장비",   true);
        var tabArtifactBtn = CreateTabButton("Tab_성물",   innerTabBar, "성물",   false);

        // ── 3. 스크롤 뷰 (하단 39%) ──────────────────────────────────
        var scrollGO = new GameObject("InventoryScroll");
        scrollGO.transform.SetParent(panelJangbi.transform, false);
        var scrollRect2 = scrollGO.AddComponent<RectTransform>();
        SetAnchors(scrollRect2, Vector2.zero, new Vector2(1f, 0.39f), Vector2.zero, Vector2.zero);
        scrollGO.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.16f, 1f);

        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical   = true;

        var viewport = CreateRect("Viewport", scrollGO.transform);
        SetAnchors(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        // 장비 Content
        var weaponContent   = CreateGridContent("WeaponContent",   viewport);
        var artifactContent = CreateGridContent("ArtifactContent", viewport);
        artifactContent.gameObject.SetActive(false);

        sr.content = weaponContent;

        // 더미 슬롯 16개 (장비탭)
        for (int i = 0; i < 16; i++)
            CreateDummyItemSlot($"WeaponSlot_{i}", weaponContent);

        // ── 4. HomeEquipPanel 컴포넌트 붙이기 ────────────────────────
        var equipPanel = panelJangbi.AddComponent<HomeEquipPanel>();
        equipPanel.equipFrames    = equipFrames;
        equipPanel.equipIcons     = equipIcons;
        equipPanel.tabWeapon      = tabWeaponBtn;
        equipPanel.tabArtifact    = tabArtifactBtn;
        equipPanel.weaponContent   = weaponContent;
        equipPanel.artifactContent = artifactContent;
        equipPanel.attackText = attackText;
        equipPanel.hpText     = hpText;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HomeUIBuilder] Equipment Panel 빌드 완료.");
    }

    // ────────────────────────────────────────────────────────────────
    // 유틸
    // ────────────────────────────────────────────────────────────────

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

    static Button CreateTabButton(string name, RectTransform parent, string label, bool active)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = active ? new Color(1f, 0.8f, 0.2f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);
        var btn = go.AddComponent<Button>();

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 32;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return btn;
    }

    static TextMeshProUGUI CreateTMPText(string name, RectTransform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        return tmp;
    }

    static RectTransform CreateGridContent(string name, RectTransform parent)
    {
        var rt = CreateRect(name, parent);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var grid = rt.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(170, 170);
        grid.spacing         = new Vector2(12, 12);
        grid.padding         = new RectOffset(16, 16, 16, 16);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    static void CreateDummyItemSlot(string name, RectTransform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.25f, 0.4f, 1f);

        // 아이콘 자리
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var rt = iconGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.1f);
        rt.anchorMax = new Vector2(0.9f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        iconGO.AddComponent<Image>().color = new Color(0.3f, 0.35f, 0.55f, 1f);
    }

    // ────────────────────────────────────────────────────────────────
    // 기존 Home UI 빌더
    // ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Home UI")]
    public static void BuildHomeUI()
    {
        // Open the Home scene
        string scenePath = "Assets/_Recovery/Home.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Clear existing Canvas if any
        var existingCanvases = GameObject.FindObjectsOfType<Canvas>();
        foreach (var c in existingCanvases)
        {
            if (c.gameObject.name == "HomeUICanvas")
                GameObject.DestroyImmediate(c.gameObject);
        }

        // ---- Create Canvas ----
        var canvasGO = new GameObject("HomeUICanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1080, 1920);
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- Create EventSystem if not present ----
        if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ---- Create PanelContainer ----
        var containerGO = new GameObject("PanelContainer");
        containerGO.transform.SetParent(canvasGO.transform, false);
        var containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = new Vector2(0, 120); // bottom offset = 120
        containerRect.offsetMax = Vector2.zero;

        // ---- Panel definitions ----
        string[] panelNames = { "Panel_상점", "Panel_대원", "Panel_전투", "Panel_장비", "Panel_기물" };
        Color[] panelColors = {
            new Color(0.2f, 0.4f, 0.8f, 1f),   // 상점 - blue
            new Color(0.2f, 0.7f, 0.4f, 1f),   // 대원 - green
            new Color(0.6f, 0.2f, 0.8f, 1f),   // 전투 - purple
            new Color(0.8f, 0.5f, 0.1f, 1f),   // 장비 - orange
            new Color(0.1f, 0.6f, 0.7f, 1f),   // 기물 - teal
        };

        GameObject[] panels = new GameObject[5];

        for (int i = 0; i < panelNames.Length; i++)
        {
            var panelGO = new GameObject(panelNames[i]);
            panelGO.transform.SetParent(containerGO.transform, false);

            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = panelColors[i];

            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Default: only Panel_전투 (index 2) is active
            panelGO.SetActive(i == 2);
            panels[i] = panelGO;
        }

        // ---- Add "게임 시작" button to Panel_전투 ----
        var battlePanel = panels[2];
        var startBtnGO = new GameObject("StartButton");
        startBtnGO.transform.SetParent(battlePanel.transform, false);

        var startBtn = startBtnGO.AddComponent<Button>();
        var startBtnImg = startBtnGO.AddComponent<Image>();
        startBtnImg.color = new Color(1f, 0.3f, 0.3f, 1f);

        var startBtnRect = startBtnGO.GetComponent<RectTransform>();
        startBtnRect.anchorMin = new Vector2(0.25f, 0.4f);
        startBtnRect.anchorMax = new Vector2(0.75f, 0.6f);
        startBtnRect.offsetMin = Vector2.zero;
        startBtnRect.offsetMax = Vector2.zero;

        // Add text to start button
        var startBtnTextGO = new GameObject("Text");
        startBtnTextGO.transform.SetParent(startBtnGO.transform, false);
        var startBtnText = startBtnTextGO.AddComponent<Text>();
        startBtnText.text = "게임 시작";
        startBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        startBtnText.fontSize = 48;
        startBtnText.fontStyle = FontStyle.Bold;
        startBtnText.alignment = TextAnchor.MiddleCenter;
        startBtnText.color = Color.white;

        var startBtnTextRect = startBtnTextGO.GetComponent<RectTransform>();
        startBtnTextRect.anchorMin = Vector2.zero;
        startBtnTextRect.anchorMax = Vector2.one;
        startBtnTextRect.offsetMin = Vector2.zero;
        startBtnTextRect.offsetMax = Vector2.zero;

        // ---- Add label texts to each panel ----
        string[] labelNames = { "상점", "대원", "전투", "장비", "기물" };
        for (int i = 0; i < panels.Length; i++)
        {
            var labelGO = new GameObject("PanelLabel");
            labelGO.transform.SetParent(panels[i].transform, false);
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = labelNames[i];
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 80;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;

            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.7f);
            labelRect.anchorMax = new Vector2(1f, 0.9f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        // ---- Create TabBar ----
        var tabBarGO = new GameObject("TabBar");
        tabBarGO.transform.SetParent(canvasGO.transform, false);

        var tabBarImg = tabBarGO.AddComponent<Image>();
        tabBarImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var tabBarRect = tabBarGO.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0f, 0f);
        tabBarRect.anchorMax = new Vector2(1f, 0f);
        tabBarRect.pivot = new Vector2(0.5f, 0f);
        tabBarRect.sizeDelta = new Vector2(0, 120);
        tabBarRect.anchoredPosition = Vector2.zero;

        // Add horizontal layout group to tab bar
        var hLayout = tabBarGO.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = true;
        hLayout.spacing = 2;
        hLayout.padding = new RectOffset(4, 4, 4, 4);

        // ---- Tab Button definitions ----
        string[] tabLabels = { "상점", "대원", "전투", "장비", "기물" };
        Button[] tabButtons = new Button[5];

        for (int i = 0; i < tabLabels.Length; i++)
        {
            var tabBtnGO = new GameObject("Tab_" + tabLabels[i]);
            tabBtnGO.transform.SetParent(tabBarGO.transform, false);

            var tabBtn = tabBtnGO.AddComponent<Button>();
            var tabBtnImg = tabBtnGO.AddComponent<Image>();
            // Default inactive color; active will be set by HomeUIManager
            tabBtnImg.color = (i == 2)
                ? new Color(1f, 0.8f, 0.2f, 1f)   // active: yellow-gold
                : new Color(0.5f, 0.5f, 0.5f, 1f); // inactive: grey

            // Tab label text
            var tabLabelGO = new GameObject("Text");
            tabLabelGO.transform.SetParent(tabBtnGO.transform, false);
            var tabLabelText = tabLabelGO.AddComponent<Text>();
            tabLabelText.text = tabLabels[i];
            tabLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tabLabelText.fontSize = 36;
            tabLabelText.fontStyle = FontStyle.Bold;
            tabLabelText.alignment = TextAnchor.MiddleCenter;
            tabLabelText.color = Color.white;

            var tabLabelRect = tabLabelGO.GetComponent<RectTransform>();
            tabLabelRect.anchorMin = Vector2.zero;
            tabLabelRect.anchorMax = Vector2.one;
            tabLabelRect.offsetMin = Vector2.zero;
            tabLabelRect.offsetMax = Vector2.zero;

            tabButtons[i] = tabBtn;
        }

        // ---- Attach HomeUIManager ----
        var manager = canvasGO.AddComponent<HomeUIManager>();
        manager.panels = panels;
        manager.tabButtons = tabButtons;

        // Wire up StartButton onClick to manager.StartGame (persistent — serialized in scene)
        UnityEventTools.AddPersistentListener(startBtn.onClick, manager.StartGame);

        // ---- Save the scene ----
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[HomeUIBuilder] Home UI built and scene saved successfully.");
    }
}
