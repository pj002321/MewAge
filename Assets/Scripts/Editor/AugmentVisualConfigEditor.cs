using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MuAge ▸ Augment Visual Config
/// 증강 ID별 아이콘·프리팹을 한 창에서 편집합니다.
/// </summary>
public class AugmentVisualConfigEditor : EditorWindow
{
    private ImprovVisualConfig _config;
    private Vector2 _scroll;

    private static readonly (string label, int min, int max)[] Chapters =
    {
        ("공통 (CMN)",  0,   99),
        ("C1 원시시대", 100, 199),
    };

    private static readonly HashSet<ImprovType> NeedsCompanion = new()
    {
        ImprovType.AUG_SPAWN_TURRET, // 달팽이 설치물 프리팹
        ImprovType.AUG_PET_BEAST,
    };

    private static readonly HashSet<ImprovType> NeedsProjectile = new()
    {
        ImprovType.AUG_SPAWN_TURRET, // 달팽이가 발사하는 투사체 프리팹
        ImprovType.AUG_MUD_OPENING,
    };

    private static readonly HashSet<ImprovType> NeedsPlayerVfx = new()
    {
        ImprovType.AUG_ICE_AGE,
        ImprovType.AUG_BURNING_PAWS,
    };

    [MenuItem("MuAge/Augment Visual Config")]
    static void Open()
    {
        var w = GetWindow<AugmentVisualConfigEditor>("증강 비주얼 설정");
        w.minSize = new Vector2(680, 400);
    }

    private void OnEnable() => LoadConfig();

    private void LoadConfig()
    {
        _config = Resources.Load<ImprovVisualConfig>("AUG/ImprovVisualConfig");
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (_config == null)
        {
            EditorGUILayout.HelpBox(
                "Resources/AUG/ImprovVisualConfig.asset 를 찾을 수 없습니다.\n" +
                "Create > Mewage > Improv Visual Config 로 생성 후 Resources/AUG/ 에 배치하세요.",
                MessageType.Error);
            if (GUILayout.Button("Assets/Resources/AUG/ 에 자동 생성"))
                CreateAsset();
            return;
        }

        DrawColumnHeader();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        Undo.RecordObject(_config, "Edit Augment Visual Config");

        foreach (var (label, min, max) in Chapters)
            DrawChapterSection(label, min, max);

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
            EditorUtility.SetDirty(_config);
    }

    // ── 툴바 ────────────────────────────────────────────────────────
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("누락 항목 추가", EditorStyles.toolbarButton, GUILayout.Width(120)))
            PopulateMissing();

        if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            if (_config != null) EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }

        GUILayout.FlexibleSpace();

        var newCfg = (ImprovVisualConfig)EditorGUILayout.ObjectField(
            _config, typeof(ImprovVisualConfig), false, GUILayout.Width(220));
        if (newCfg != _config)
            _config = newCfg;

        EditorGUILayout.EndHorizontal();
    }

    // ── 컬럼 헤더 ────────────────────────────────────────────────────
    private void DrawColumnHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("증강 ID",     EditorStyles.boldLabel, GUILayout.Width(200));
        GUILayout.Label("아이콘",       EditorStyles.boldLabel, GUILayout.Width(84));
        GUILayout.Label("동반자 프리팹", EditorStyles.boldLabel, GUILayout.Width(134));
        GUILayout.Label("투사체 프리팹", EditorStyles.boldLabel, GUILayout.Width(134));
        GUILayout.Label("플레이어 VFX", EditorStyles.boldLabel, GUILayout.Width(134));
        EditorGUILayout.EndHorizontal();
    }

    // ── 챕터 섹션 ────────────────────────────────────────────────────
    private void DrawChapterSection(string title, int min, int max)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        foreach (ImprovType t in Enum.GetValues(typeof(ImprovType)))
        {
            int v = (int)t;
            if (v < min || v > max) continue;
            DrawRow(t);
        }
    }

    // ── 행 그리기 ────────────────────────────────────────────────────
    private void DrawRow(ImprovType type)
    {
        int idx = FindOrCreate(type);
        var e   = _config.entries[idx];

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(type.ToString(), GUILayout.Width(200));

        e.icon = (Sprite)EditorGUILayout.ObjectField(
            e.icon, typeof(Sprite), false, GUILayout.Width(84));

        if (NeedsCompanion.Contains(type))
            e.companionPrefab = (GameObject)EditorGUILayout.ObjectField(
                e.companionPrefab, typeof(GameObject), false, GUILayout.Width(134));
        else
            GUILayout.Label("—", GUILayout.Width(134));

        if (NeedsProjectile.Contains(type))
            e.projectilePrefab = (GameObject)EditorGUILayout.ObjectField(
                e.projectilePrefab, typeof(GameObject), false, GUILayout.Width(134));
        else
            GUILayout.Label("—", GUILayout.Width(134));

        if (NeedsPlayerVfx.Contains(type))
            e.playerVfxPrefab = (GameObject)EditorGUILayout.ObjectField(
                e.playerVfxPrefab, typeof(GameObject), false, GUILayout.Width(134));
        else
            GUILayout.Label("—", GUILayout.Width(134));

        _config.entries[idx] = e;

        EditorGUILayout.EndHorizontal();
    }

    // ── 유틸 ────────────────────────────────────────────────────────
    private int FindOrCreate(ImprovType type)
    {
        var arr = _config.entries;
        if (arr != null)
            for (int i = 0; i < arr.Length; i++)
                if (arr[i].type == type) return i;

        var list = new List<ImprovVisualConfig.Entry>(arr ?? Array.Empty<ImprovVisualConfig.Entry>())
        {
            new ImprovVisualConfig.Entry { type = type }
        };
        _config.entries = list.ToArray();
        return _config.entries.Length - 1;
    }

    private void PopulateMissing()
    {
        if (_config == null) return;
        Undo.RecordObject(_config, "Populate Augment Entries");
        foreach (ImprovType t in Enum.GetValues(typeof(ImprovType)))
            FindOrCreate(t);
        EditorUtility.SetDirty(_config);
        AssetDatabase.SaveAssets();
    }

    private void CreateAsset()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/AUG");
        var asset = CreateInstance<ImprovVisualConfig>();
        AssetDatabase.CreateAsset(asset, "Assets/Resources/AUG/ImprovVisualConfig.asset");
        AssetDatabase.SaveAssets();
        _config = asset;
    }
}
