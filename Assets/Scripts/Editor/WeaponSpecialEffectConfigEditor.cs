#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 무기별 특수효과 프리팹 에디터 (MuAge/Weapon Special Effect Config Editor).
/// DB_무기에서 프리팹이 필요한 특성(HIT_CLONE 등)을 가진 무기를 챕터별로 표시합니다.
/// StatusEffectConfig.weaponEntries 에 저장됩니다.
/// </summary>
public class WeaponSpecialEffectConfigEditor : EditorWindow
{
    private static readonly string[] PrefabRequiredTraits =
    {
        "HIT_ICE", "HIT_STUN", "HIT_FIRE", "HIT_CLONE", "HIT_SHOCK", "HIT_SPIN",
        "BOSS_SPIKE", "BOSS_GROGY", "BOSS_BIND", "PASS_DMG"
    };

    private StatusEffectConfig _config;
    private SerializedObject   _so;
    private SerializedProperty _entriesProp;

    private int     _tab;
    private Vector2 _scroll;

    private readonly List<(int chapter, List<DB_무기> weapons)> _chapters = new();
    private readonly Dictionary<string, int> _indexMap = new();

    [MenuItem("MuAge/Weapon Special Effect Config Editor")]
    public static void Open() => GetWindow<WeaponSpecialEffectConfigEditor>("특수효과 프리팹");

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
            if (!NeedsPrefab(row.f_특성)) return;
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

        DrawToolbar();

        if (_chapters.Count == 0)
        {
            EditorGUILayout.HelpBox("HIT_CLONE 등 프리팹이 필요한 특성을 가진 무기가 없습니다.", MessageType.Info);
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
        GUILayout.Label("발동확률: 0 입력 시 효과별 기본값 사용. HIT_ICE=30% HIT_STUN=25% HIT_FIRE=35% HIT_CLONE=25% HIT_SHOCK=40% HIT_SPIN=30% BOSS_SPIKE=25% BOSS_GROGY=20% BOSS_BIND=15%", footer);

        if (_so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            RebuildIndexMap();
        }
    }

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

    private static readonly string[] Headers = { "무기ID", "이름", "특성", "발동확률", "클론 프리팹", "전진(s)", "정지(s)", "복귀(s)", "사거리", "스파이크 프리팹" };
    private static readonly float[]  ColW    = { 100f, 80f, 80f, 55f, 160f, 50f, 50f, 50f, 50f, 160f };

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
        string wid = row.f_무기ID ?? "";
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        var lbl = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label(wid,               lbl, GUILayout.Width(ColW[0]));
        GUILayout.Label(row.f_이름 ?? "-", lbl, GUILayout.Width(ColW[1]));
        GUILayout.Label(row.f_특성 ?? "-", lbl, GUILayout.Width(ColW[2]));

        int idx  = GetOrCreateEntry(wid, row.f_특성 ?? "");
        var elem = _entriesProp.GetArrayElementAtIndex(idx);

        bool isClone = row.f_특성 == "HIT_CLONE";
        bool isSpike = row.f_특성 == "BOSS_SPIKE";
        DrawFloatField (elem.FindPropertyRelative("triggerChance"),          3, true);
        DrawPrefabField(elem.FindPropertyRelative("cloneProjectilePrefab"),  4, isClone);
        DrawFloatField (elem.FindPropertyRelative("cloneOutTime"),           5, isClone);
        DrawFloatField (elem.FindPropertyRelative("clonePauseTime"),         6, isClone);
        DrawFloatField (elem.FindPropertyRelative("cloneReturnTime"),        7, isClone);
        DrawFloatField (elem.FindPropertyRelative("cloneRange"),             8, isClone);
        DrawPrefabField(elem.FindPropertyRelative("spikePrefab"),            9, isSpike);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFloatField(SerializedProperty prop, int col, bool active)
    {
        var gray = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
        if (!active) { GUILayout.Label("-", gray, GUILayout.Width(ColW[col])); return; }
        EditorGUI.BeginChangeCheck();
        float val = EditorGUILayout.FloatField(prop.floatValue, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck()) prop.floatValue = val;
    }

    private void DrawPrefabField(SerializedProperty prop, int col, bool active)
    {
        var gray = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
        if (!active) { GUILayout.Label("-", gray, GUILayout.Width(ColW[col])); return; }
        EditorGUI.BeginChangeCheck();
        var obj = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck()) prop.objectReferenceValue = obj;
    }

    private int GetOrCreateEntry(string weaponId, string traitId)
    {
        if (_indexMap.TryGetValue(weaponId, out int existing)) return existing;

        int newIdx = _entriesProp.arraySize;
        _entriesProp.arraySize++;
        var elem = _entriesProp.GetArrayElementAtIndex(newIdx);
        elem.FindPropertyRelative("weaponId").stringValue              = weaponId;
        elem.FindPropertyRelative("traitId").stringValue               = traitId;
        elem.FindPropertyRelative("cloneProjectilePrefab").objectReferenceValue = null;
        _so.ApplyModifiedPropertiesWithoutUndo();
        _indexMap[weaponId] = newIdx;
        return newIdx;
    }

    private static bool NeedsPrefab(string trait)
    {
        if (string.IsNullOrEmpty(trait)) return false;
        foreach (var t in PrefabRequiredTraits)
            if (trait == t) return true;
        return false;
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
