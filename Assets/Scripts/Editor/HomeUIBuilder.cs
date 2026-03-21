using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class HomeUIBuilder
{
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
