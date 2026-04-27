#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 챕터별 무기 프리셋 에디터 (MuAge/Weapon Preset Editor).
///
/// DB값 (공격력·탐지·속도·범위·넉백·치명타·출혈·흡혈) → 읽기 전용 표시
/// ★표 값 (투사체속도·주변배율)                        → 직접 편집 → 저장 버튼으로 프리팹에 반영
/// </summary>
public class WeaponPresetEditor : EditorWindow
{
    private int     _tab;
    private Vector2 _scroll;
    private bool    _dirty;

    private readonly List<(int chapter, List<DB_무기> weapons)> _chapters = new();

    // weaponId → 편집 가능한 프리팹 엔트리
    private readonly Dictionary<string, PrefabEntry> _prefabs = new();

    private class PrefabEntry
    {
        public GameObject         go;
        public SerializedObject   so;
        public SerializedProperty speedProp;
        public SerializedProperty ratioProp;
        public SerializedProperty magicElementProp;
        public SerializedProperty zapBoltVfxPrefabProp;
        public SerializedProperty pullbackProp;
        public SerializedProperty strikeProp;
        public SerializedProperty returnProp;
        public SerializedProperty overshootDistProp;
        public SerializedProperty overshootSpeedProp;
        public Sprite             icon;
    }

    // ── 열 정의 ──────────────────────────────────────────────────────
    private static readonly string[] ColHeaders =
    {
        "",      "무기ID", "이름",   "타입",  "등급",
        "공격력", "탐지",  "쿨타임", "범위",
        "튕김",   "넉백",  "크리%",  "출혈",  "흡혈",
        "★투사체속도", "★배율", "★마법속성", "★번개VFX",
        "★백스윙", "★돌진", "★복귀", "★오버슛거리", "★오버슛속도",
    };
    private static readonly float[] ColW =
    {
        36,  90,  72,  50,  50,
        50,  44,  52,  52,
        38,  44,  48,  42,  42,
        72,  52,  70,  70,
        52,  48,  48,  68,  68,
    };

    private static readonly Color[] TypeBg =
    {
        new Color(1f,   0.75f, 0.75f, 0.22f), // Melee
        new Color(0.7f, 0.9f,  1f,    0.22f), // Ranged
        new Color(0.8f, 0.7f,  1f,    0.22f), // Magic
        new Color(1f,   0.9f,  0.55f, 0.22f), // Boomerang
        new Color(0.7f, 1f,    0.8f,  0.22f), // Spin
        new Color(1f,   0.6f,  0.8f,  0.22f), // Slash
    };

    [MenuItem("MuAge/Weapon Preset Editor")]
    public static void Open() => GetWindow<WeaponPresetEditor>("무기 프리셋");

    private void OnEnable()  => Reload();
    private void OnFocus()   => Reload();

    // ── 데이터 로드 ──────────────────────────────────────────────────
    private void Reload()
    {
        _chapters.Clear();
        _prefabs.Clear();
        _dirty = false;

        var byChapter = new SortedDictionary<int, List<DB_무기>>();
        DB_무기.ForEachEntity(row =>
        {
            int ch = row.f_챕터;
            if (!byChapter.ContainsKey(ch)) byChapter[ch] = new List<DB_무기>();
            byChapter[ch].Add(row);
        });
        foreach (var kv in byChapter)
            _chapters.Add((kv.Key, kv.Value));

        if (_tab >= _chapters.Count) _tab = 0;

        // 전체 챕터 프리팹 미리 로드
        foreach (var ch in _chapters)
            foreach (var row in ch.weapons)
                TryLoadPrefab(row);

        Repaint();
    }

    private void TryLoadPrefab(DB_무기 row)
    {
        string id = row.f_무기ID;
        if (string.IsNullOrEmpty(id) || _prefabs.ContainsKey(id)) return;

        string path = $"Assets/Resources/Weapons/Ch{row.f_챕터}/{id.ToLower()}.prefab";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) return;

        var wp = go.GetComponentInChildren<WeaponPoint>();
        if (wp == null) return;

        var so = new SerializedObject(wp);
        var sprite = go.GetComponent<SpriteRenderer>()?.sprite
                  ?? go.GetComponentInChildren<SpriteRenderer>()?.sprite;

        _prefabs[id] = new PrefabEntry
        {
            go                   = go,
            so                   = so,
            speedProp            = so.FindProperty("projectileSpeed"),
            ratioProp            = so.FindProperty("areaHitDamageRatio"),
            magicElementProp     = so.FindProperty("magicElement"),
            zapBoltVfxPrefabProp = so.FindProperty("zapBoltVfxPrefab"),
            pullbackProp         = so.FindProperty("pullbackDist"),
            strikeProp           = so.FindProperty("strikeSpeed"),
            returnProp           = so.FindProperty("returnSpeed"),
            overshootDistProp    = so.FindProperty("overshootDistance"),
            overshootSpeedProp   = so.FindProperty("overshootSpeed"),
            icon                 = sprite,
        };
    }

    // ── 저장 ──────────────────────────────────────────────────────────
    private void Save()
    {
        foreach (var e in _prefabs.Values)
        {
            if (e.go == null) continue;
            e.so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(e.go);
        }
        AssetDatabase.SaveAssets();
        _dirty = false;
        Debug.Log("[WeaponPresetEditor] 저장 완료");
    }

    // ── GUI ──────────────────────────────────────────────────────────
    private void OnGUI()
    {
        DrawToolbar();

        if (_chapters.Count == 0)
        {
            EditorGUILayout.HelpBox("DB_무기 데이터가 없습니다.", MessageType.Warning);
            return;
        }

        if (_tab >= _chapters.Count) _tab = _chapters.Count - 1;

        _scroll = EditorGUILayout.BeginScrollView(_scroll, alwaysShowHorizontal: true, alwaysShowVertical: false);
        DrawColumnHeader();
        foreach (var row in _chapters[_tab].weapons)
            DrawRow(row);
        EditorGUILayout.EndScrollView();

        var footer = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
        GUILayout.Label("DB값: 읽기 전용 (BansheeGz에서 수정)  |  ★표 값: 직접 편집 후 저장", footer);
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

        int count = _chapters.Count > 0 ? _chapters[_tab].weapons.Count : 0;
        GUILayout.Label($"{count}종", EditorStyles.miniLabel);
        GUILayout.Space(8);

        // 미저장 변경이 있으면 저장 버튼 강조
        var saveLabel = _dirty ? "● 저장" : "저장";
        var saveColor = _dirty ? new Color(1f, 0.75f, 0.3f) : Color.white;
        var prevColor = GUI.contentColor;
        GUI.contentColor = saveColor;
        if (GUILayout.Button(saveLabel, EditorStyles.toolbarButton, GUILayout.Width(70)))
            Save();
        GUI.contentColor = prevColor;

        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70)))
            Reload();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawColumnHeader()
    {
        var h = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        for (int i = 0; i < ColHeaders.Length; i++)
            GUILayout.Label(ColHeaders[i], h, GUILayout.Width(ColW[i]));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(DB_무기 row)
    {
        int tIdx = TypeIndex(row.f_타입);
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = tIdx >= 0 ? TypeBg[tIdx] : new Color(1f, 1f, 1f, 0.08f);
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        var lbl  = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        var gray = new GUIStyle(lbl) { normal = { textColor = Color.gray } };
        var star = new GUIStyle(lbl) { normal = { textColor = new Color(0.9f, 0.8f, 0.2f) } };

        // ── 아이콘 열 ────────────────────────────────────────────────
        string id0 = row.f_무기ID ?? "";
        if (_prefabs.TryGetValue(id0, out var iconEntry) && iconEntry.icon != null)
        {
            var tex  = AssetPreview.GetAssetPreview(iconEntry.icon);
            var size = GUILayout.Width(ColW[0]);
            if (tex != null)
                GUILayout.Label(tex, GUILayout.Width(ColW[0]), GUILayout.Height(ColW[0]));
            else
                GUILayout.Label("", size);
        }
        else
        {
            GUILayout.Label("", GUILayout.Width(ColW[0]));
        }

        // ── DB 읽기 전용 열 ──────────────────────────────────────────
        L(row.f_무기ID ?? "-",          1, lbl);
        L(row.f_이름   ?? "-",          2, lbl);
        L(TypeStr(row.f_타입),           3, lbl);
        L(GradeStr(row.f_등급),          4, lbl);
        F(row.f_기본공격력,              5, lbl);
        F(row.f_탐지범위,                6, lbl);
        L($"{row.f_공격속도:0.##}s",     7, lbl);
        F(row.f_주변범위,                8, lbl);
        L(row.f_튕김횟수 > 0 ? row.f_튕김횟수.ToString() : "-", 9, lbl);
        F(row.f_넉백,                   10, lbl);
        L($"{row.f_치명타:0.#}%",       11, lbl);
        F(row.f_출혈,                   12, lbl);
        F(row.f_흡혈,                   13, lbl);

        // ── ★ 프리팹 편집 가능 열 ────────────────────────────────────
        string id = row.f_무기ID ?? "";
        if (_prefabs.TryGetValue(id, out var entry) && entry.speedProp != null)
        {
            entry.so.Update();
            EditFloat(entry, entry.speedProp,         14, star);
            EditFloat(entry, entry.ratioProp,         15, star);
            // ★마법속성 — Magic 타입만 드롭다운, 나머지는 "-"
            bool isMagic = TypeIndex(row.f_타입) == 2;
            if (isMagic && entry.magicElementProp != null)
                EditMagicElement(entry, 16, star);
            else
                L("-", 16, gray);
            // ★번개VFX — 마법속성이 Lightning일 때만 편집 가능
            bool isLightning = isMagic && entry.magicElementProp != null && (MagicElement)entry.magicElementProp.enumValueIndex == MagicElement.Lightning;
            if (isLightning && entry.zapBoltVfxPrefabProp != null)
                EditObject(entry, entry.zapBoltVfxPrefabProp, 17, star);
            else
                L("-", 17, gray);
            EditFloat(entry, entry.pullbackProp,      18, star);
            EditFloat(entry, entry.strikeProp,        19, star);
            EditFloat(entry, entry.returnProp,        20, star);
            EditFloat(entry, entry.overshootDistProp, 21, star);
            EditFloat(entry, entry.overshootSpeedProp,22, star);
        }
        else
        {
            for (int ci = 14; ci <= 22; ci++) L("-", ci, gray);
        }

        EditorGUILayout.EndHorizontal();
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────
    private void L(string text, int col, GUIStyle s)
        => GUILayout.Label(text, s, GUILayout.Width(ColW[col]));

    private void F(float v, int col, GUIStyle s)
        => L(v == 0f ? "-" : v.ToString("0.##"), col, s);

    private void EditFloat(PrefabEntry entry, SerializedProperty prop, int col, GUIStyle s)
    {
        if (prop == null) { L("-", col, s); return; }
        EditorGUI.BeginChangeCheck();
        float val = EditorGUILayout.FloatField(prop.floatValue, s, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck())
        {
            prop.floatValue = val;
            entry.so.ApplyModifiedPropertiesWithoutUndo();
            _dirty = true;
        }
    }

    private void EditMagicElement(PrefabEntry entry, int col, GUIStyle s)
    {
        var prop = entry.magicElementProp;
        EditorGUI.BeginChangeCheck();
        var cur = (MagicElement)prop.enumValueIndex;
        var next = (MagicElement)EditorGUILayout.EnumPopup(cur, s, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck())
        {
            prop.enumValueIndex = (int)next;
            entry.so.ApplyModifiedPropertiesWithoutUndo();
            _dirty = true;
        }
    }

    private void EditObject(PrefabEntry entry, SerializedProperty prop, int col, GUIStyle s)
    {
        if (prop == null) { L("-", col, s); return; }
        EditorGUI.BeginChangeCheck();
        var obj = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(ColW[col]));
        if (EditorGUI.EndChangeCheck())
        {
            prop.objectReferenceValue = obj;
            entry.so.ApplyModifiedPropertiesWithoutUndo();
            _dirty = true;
        }
    }

    private static int TypeIndex(string t) => t switch
    {
        "Melee"     or "근거리"            => 0,
        "Ranged"    or "원거리"            => 1,
        "Magic"     or "마법"              => 2,
        "Boomerang" or "부메랑"            => 3,
        "Spin"      or "돌리기"            => 4,
        "Slash"     or "슬래시" or "베기"  => 5,
        _                                  => -1,
    };

    private static string TypeStr(string t) => t switch
    {
        "Melee"     or "근거리"            => "근접",
        "Ranged"    or "원거리"            => "원거리",
        "Magic"     or "마법"              => "마법",
        "Boomerang" or "부메랑"            => "부메랑",
        "Spin"      or "돌리기"            => "스핀",
        "Slash"     or "슬래시" or "베기"  => "슬래시",
        _                                  => t ?? "?",
    };

    private static string GradeStr(string g) => g switch
    {
        "Common"    or "일반" => "일반",
        "Rare"      or "레어" => "레어",
        "Epic"      or "희귀" => "희귀",
        "Legendary" or "전설" => "전설",
        "Mythic"    or "신화" => "신화",
        _                    => g ?? "?",
    };
}
#endif
