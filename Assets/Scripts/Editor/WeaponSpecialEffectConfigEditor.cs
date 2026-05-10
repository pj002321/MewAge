#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 무기별 특수효과 설정 에디터 (MuAge/Weapon Special Effect Config Editor).
/// 특성ID가 있는 모든 무기를 챕터별로 표시합니다.
/// StatusEffectConfig.weaponEntries 에 저장됩니다.
/// </summary>
public class WeaponSpecialEffectConfigEditor : EditorWindow
{
    private StatusEffectConfig _config;
    private SerializedObject   _so;
    private SerializedProperty _entriesProp;

    private int     _tab;
    private Vector2 _scroll;
    private bool    _chapterVfxFoldout = true;

    private readonly List<(int chapter, List<DB_무기> weapons)> _chapters = new();
    private readonly Dictionary<string, int> _indexMap = new();

    [MenuItem("MuAge/Weapon Special Effect Config Editor")]
    public static void Open() => GetWindow<WeaponSpecialEffectConfigEditor>("특수효과 설정");

    private void OnEnable() => Reload();
    private void OnFocus()  => Reload();

    private void Reload()
    {
        LoadOrCreateConfig();
        RebuildChapters();
        RebuildIndexMap();
        if (_tab >= _chapters.Count) _tab = 0;
        Repaint();
    }

    private void LoadOrCreateConfig()
    {
        _config = Resources.Load<StatusEffectConfig>("StatusEffectConfig");
        if (_config == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            _config = CreateInstance<StatusEffectConfig>();
            AssetDatabase.CreateAsset(_config, "Assets/Resources/StatusEffectConfig.asset");
            AssetDatabase.SaveAssets();
        }
        _so          = new SerializedObject(_config);
        _entriesProp = _so.FindProperty("weaponEntries");
    }

    private void RebuildChapters()
    {
        _chapters.Clear();
        var byChapter = new SortedDictionary<int, List<DB_무기>>();
        DB_무기.ForEachEntity(row =>
        {
            if (string.IsNullOrEmpty(row.f_특성)) return;
            int ch = ChapterFromId(row.f_무기ID, row.f_챕터);
            if (!byChapter.ContainsKey(ch)) byChapter[ch] = new List<DB_무기>();
            byChapter[ch].Add(row);
        });
        foreach (var kv in byChapter)
            _chapters.Add((kv.Key, kv.Value));
    }

    private void RebuildIndexMap()
    {
        _indexMap.Clear();
        if (_entriesProp == null) return;
        for (int i = 0; i < _entriesProp.arraySize; i++)
        {
            var wid = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("weaponId").stringValue;
            if (!string.IsNullOrEmpty(wid)) _indexMap[wid] = i;
        }
    }

    private void OnGUI()
    {
        if (_config == null) { LoadOrCreateConfig(); return; }
        _so.Update();

        DrawChapterVfxSection();
        EditorGUILayout.Space(2);

        DrawToolbar();

        if (_chapters.Count == 0)
        {
            EditorGUILayout.HelpBox("특성ID가 있는 무기가 없습니다.", MessageType.Info);
        }
        else
        {
            if (_tab >= _chapters.Count) _tab = _chapters.Count - 1;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawColumnHeader();
            foreach (var row in _chapters[_tab].weapons)
                DrawRow(row);
            EditorGUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();
        var footer = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
        GUILayout.Label(
            "발동확률 0 = 효과별 기본값. HIT_ICE=30% HIT_STUN=25% HIT_FIRE=35% HIT_CLONE_*=25% HIT_DOT=35% HIT_SHOCK=40% HIT_SPIN=30% BOSS_SPIKE=25% BOSS_GROGY=20% BOSS_BIND=15%",
            footer);

        if (_so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            RebuildIndexMap();
        }
    }

    // ── 챕터별 VFX 섹션 ──────────────────────────────────────────────

    private void DrawChapterVfxSection()
    {
        _chapterVfxFoldout = EditorGUILayout.Foldout(_chapterVfxFoldout, "챕터별 스턴/빙결/화상 VFX 프리팹", true, EditorStyles.foldoutHeader);
        if (!_chapterVfxFoldout) return;

        var chaptersProp = _so.FindProperty("chapters");

        var hStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var lbl    = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("챕터",     hStyle, GUILayout.Width(50));
        GUILayout.Label("스턴 VFX", hStyle, GUILayout.Width(160));
        GUILayout.Label("빙결 VFX", hStyle, GUILayout.Width(160));
        GUILayout.Label("화상 VFX", hStyle, GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < chaptersProp.arraySize; i++)
        {
            var entry = chaptersProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"Ch.{entry.FindPropertyRelative("chapter").intValue}", lbl, GUILayout.Width(50));
            DrawVfxProp(entry.FindPropertyRelative("stunVfxPrefab"),   160);
            DrawVfxProp(entry.FindPropertyRelative("freezeVfxPrefab"), 160);
            DrawVfxProp(entry.FindPropertyRelative("fireVfxPrefab"),   160);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("챕터 추가", GUILayout.Width(80)))
        {
            int newIdx = chaptersProp.arraySize;
            chaptersProp.arraySize++;
            var newEntry = chaptersProp.GetArrayElementAtIndex(newIdx);
            newEntry.FindPropertyRelative("chapter").intValue = newIdx + 1;
            newEntry.FindPropertyRelative("stunVfxPrefab").objectReferenceValue   = null;
            newEntry.FindPropertyRelative("freezeVfxPrefab").objectReferenceValue = null;
            newEntry.FindPropertyRelative("fireVfxPrefab").objectReferenceValue   = null;
        }
        if (chaptersProp.arraySize > 0 && GUILayout.Button("마지막 제거", GUILayout.Width(80)))
            chaptersProp.arraySize--;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawVfxProp(SerializedProperty prop, float width)
    {
        EditorGUI.BeginChangeCheck();
        var obj = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(width));
        if (EditorGUI.EndChangeCheck()) prop.objectReferenceValue = obj;
    }

    // ── 컬럼 정의 ────────────────────────────────────────────────────

    private static readonly string[] Headers =
    {
        "무기ID", "이름", "특성", "발동확률",
        "클론 프리팹", "전진(s)", "정지(s)", "복귀(s)", "사거리",
        "스파이크 프리팹",
        "스턴타입", "스턴색상",
        "영구공전",
        "서브 프리팹",
        "버프%", "버프 VFX",
    };
    private static readonly float[] ColW =
    {
        90f, 80f, 105f, 55f,
        140f, 45f, 45f, 45f, 45f,
        130f,
        70f, 80f,
        55f,
        130f,
        45f, 130f,
    };

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        for (int i = 0; i < _chapters.Count; i++)
        {
            bool sel = _tab == i;
            if (GUILayout.Toggle(sel, $"Ch.{_chapters[i].chapter}", EditorStyles.toolbarButton, GUILayout.Width(46)) && !sel)
            {
                _tab    = i;
                _scroll = Vector2.zero;
            }
        }
        GUILayout.FlexibleSpace();
        int cnt = _chapters.Count > 0 ? _chapters[_tab].weapons.Count : 0;
        GUILayout.Label($"{cnt}종", EditorStyles.miniLabel);
        GUILayout.Space(8);
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70)))
            Reload();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawColumnHeader()
    {
        var h = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        for (int i = 0; i < Headers.Length; i++)
            GUILayout.Label(Headers[i], h, GUILayout.Width(ColW[i]));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(DB_무기 row)
    {
        string wid   = row.f_무기ID ?? "";
        string trait = row.f_특성  ?? "";

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        var lbl = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label(wid,               lbl, GUILayout.Width(ColW[0]));
        GUILayout.Label(row.f_이름 ?? "-", lbl, GUILayout.Width(ColW[1]));
        GUILayout.Label(trait,             lbl, GUILayout.Width(ColW[2]));

        int idx  = GetOrCreateEntry(wid, trait);
        var elem = _entriesProp.GetArrayElementAtIndex(idx);

        bool isClone = trait.StartsWith("HIT_CLONE");
        bool isSpike = trait == "BOSS_SPIKE" || trait == "HIT_SPIKE";
        bool isStun  = trait == "HIT_STUN" || trait == "HIT_ICE" || trait == "HIT_FIRE";
        bool isSpin  = trait == "HIT_SPIN";
        bool isBuff  = trait.StartsWith("BUFF_");

        // 발동확률 (모든 특성 공통)
        DrawFloatField(elem.FindPropertyRelative("triggerChance"), 3, true);

        // HIT_CLONE 전용
        DrawPrefabField(elem.FindPropertyRelative("cloneProjectilePrefab"), 4, isClone);
        DrawFloatField (elem.FindPropertyRelative("cloneOutTime"),          5, isClone);
        DrawFloatField (elem.FindPropertyRelative("clonePauseTime"),        6, isClone);
        DrawFloatField (elem.FindPropertyRelative("cloneReturnTime"),       7, isClone);
        DrawFloatField (elem.FindPropertyRelative("cloneRange"),            8, isClone);

        // HIT_SPIKE 전용
        DrawPrefabField(elem.FindPropertyRelative("spikePrefab"), 9, isSpike);

        // HIT_STUN 전용
        DrawEnumField (elem.FindPropertyRelative("hitStunType"),  10, isStun);
        DrawColorField(elem.FindPropertyRelative("hitStunColor"), 11, isStun);

        // HIT_SPIN 전용
        DrawToggleField(elem.FindPropertyRelative("hitSpinPermanent"), 12, isSpin);

        // SHOOT_SUB 전용
        bool isSub = trait == "SHOOT_SUB";
        DrawPrefabField(elem.FindPropertyRelative("subProjectilePrefab"), 13, isSub);

        // BUFF_* 전용
        DrawFloatField (elem.FindPropertyRelative("passiveBuffPct"),       14, isBuff);
        DrawPrefabField(elem.FindPropertyRelative("passiveBuffVfxPrefab"), 15, isBuff);

        EditorGUILayout.EndHorizontal();
    }

    // ── 필드 드로어 헬퍼 ─────────────────────────────────────────────

    private static readonly GUIStyle GrayLabel = null; // 런타임 초기화

    private static GUIStyle GetGrayLabel()
    {
        return new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
    }

    private void DrawFloatField(SerializedProperty prop, int col, bool active)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(ColW[col])); return; }
        EditorGUI.BeginChangeCheck();
        float val = EditorGUILayout.FloatField(prop.floatValue, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck()) prop.floatValue = val;
    }

    private void DrawPrefabField(SerializedProperty prop, int col, bool active)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(ColW[col])); return; }
        EditorGUI.BeginChangeCheck();
        var obj = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck()) prop.objectReferenceValue = obj;
    }

    private void DrawEnumField(SerializedProperty prop, int col, bool active)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(ColW[col])); return; }
        EditorGUI.BeginChangeCheck();
        int val = EditorGUILayout.Popup(prop.enumValueIndex, prop.enumDisplayNames, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck()) prop.enumValueIndex = val;
    }

    private void DrawColorField(SerializedProperty prop, int col, bool active)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(ColW[col])); return; }
        EditorGUI.BeginChangeCheck();
        Color val = EditorGUILayout.ColorField(prop.colorValue, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck()) prop.colorValue = val;
    }

    private void DrawToggleField(SerializedProperty prop, int col, bool active)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(ColW[col])); return; }
        EditorGUI.BeginChangeCheck();
        bool val = EditorGUILayout.Toggle(prop.boolValue, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck()) prop.boolValue = val;
    }

    // ── 유틸 ────────────────────────────────────────────────────────

    private int GetOrCreateEntry(string weaponId, string traitId)
    {
        if (_indexMap.TryGetValue(weaponId, out int existing)) return existing;

        int newIdx = _entriesProp.arraySize;
        _entriesProp.arraySize++;
        var elem = _entriesProp.GetArrayElementAtIndex(newIdx);
        elem.FindPropertyRelative("weaponId").stringValue = weaponId;
        elem.FindPropertyRelative("traitId").stringValue  = traitId;
        _so.ApplyModifiedPropertiesWithoutUndo();
        _indexMap[weaponId] = newIdx;
        return newIdx;
    }

    private static int ChapterFromId(string weaponId, int fallback)
    {
        if (!string.IsNullOrEmpty(weaponId))
        {
            var p0 = weaponId.ToLower().Split('_')[0];
            if (p0.StartsWith("ch") && int.TryParse(p0.Substring(2), out int n)) return n;
        }
        return fallback;
    }
}
#endif
