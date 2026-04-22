#if UNITY_EDITOR
using System.Collections.Generic;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

public class UnitBalanceViewer : EditorWindow
{
    private int     _tab;
    private string  _typeTab = "ALL";
    private Vector2 _scroll;
    private bool    _dirty;
    private int     _wps = 30; // waves per stage

    private readonly List<(string name, List<UnitEntry> entries)> _chapters = new();

    private class UnitEntry
    {
        // DB 읽기 전용
        public string unitId;
        public string typeId;
        public int    designGW;
        public int    baseHp;
        public float  hpRate;
        public int    baseDmg;

        // 프리팹 편집
        public GameObject  go;
        public SerializedObject so;
        public readonly Dictionary<string, SerializedProperty> props = new();
    }

    // 타입 → Behaviour 컴포넌트 타입
    private static System.Type BehaviourType(string t) => t switch
    {
        "MELEE"    => typeof(MeleeBehaviour),
        "RANGED"   => typeof(RangedBehaviour),
        "MAGIC"    => typeof(MagicBehaviour),
        "CHARGE"   => typeof(ChargeBehaviour),
        "TANK"     => typeof(TankBehaviour),
        "SWARM"    => typeof(HordeBehaviour),
        "SLAM"     => typeof(SlamBehaviour),
        "DASHCOMBO"=> typeof(DashComboBehaviour),
        "HEALER"   => typeof(HealerBehaviour),
        _          => null,
    };

    // 타입별 편집 컬럼 정의: (헤더, 프로퍼티명, 너비)
    private static readonly Dictionary<string, (string h, string p, float w)[]> TypeCols = new()
    {
        ["MELEE"]   = new[] { ("공격범위", "attackRange", 56f),   ("히트범위", "meleeHitRange", 60f),         ("공격간격", "attackInterval", 60f) },
        ["CHARGE"]  = new[] { ("돌진간격", "chargeInterval", 60f), ("돌진시간", "chargeDuration", 60f),        ("돌진배율", "chargeSpeedMultiplier", 60f), ("준비시간", "preChargeDuration", 60f) },
        ["TANK"]    = new[] { ("공격범위", "attackRange", 56f),   ("공격간격", "attackInterval", 60f),         ("피해배율", "damageMultiplier", 60f) },
        ["RANGED"]  = new[] { ("선호거리", "preferredRange", 60f), ("발사간격", "shootInterval", 60f),          ("투사체DMG","rangedDamage", 66f) },
        ["MAGIC"]   = new[] { ("시전간격", "castInterval", 60f),   ("시전범위", "castRange", 56f),              ("마법DMG",  "spellDamage", 60f) },
        ["SWARM"]   = new[] { ("분열수",   "splitCount", 52f),    ("집결반경", "clusterRadius", 60f) },
        ["SLAM"]    = new[] { ("슬램범위", "slamRange", 56f),     ("슬램DMG",  "slamDamage", 56f),             ("착지범위", "slamRadius", 56f), ("슬램쿨타임","slamCooldown", 66f) },
        ["DASHCOMBO"]= new[] { ("돌진속도","dashSpeed", 60f),     ("돌진시간", "dashDuration", 60f),           ("타격횟수", "hitCount", 52f),   ("타격간격","hitInterval", 60f),  ("타격범위","hitRange", 56f) },
        ["HEALER"]   = new[] { ("회복간격","healInterval", 60f),  ("회복범위", "healRange", 56f),               ("회복량",  "healAmount", 56f),  ("안전거리","safeDistance", 60f) },
    };

    private static readonly (int s, int w)[] Previews =
        { (1,1),(1,15),(1,30),(2,1),(2,15),(2,30),(3,1),(3,15),(3,30) };

    private static readonly string[] TypeTabs =
        { "ALL","MELEE","CHARGE","TANK","RANGED","MAGIC","SWARM","SLAM","DASHCOMBO","HEALER" };

    private const float W_ID   = 72;
    private const float W_TYPE = 60;
    private const float W_GW   = 52;
    private const float W_HP   = 46;
    private const float W_RATE = 46;
    private const float W_DMG  = 46;
    private const float W_PRV  = 44;
    private const float ROW_H  = 18;

    [MenuItem("MuAge/Unit Balance Viewer")]
    public static void Open() => GetWindow<UnitBalanceViewer>("유닛 밸런스 뷰어");

    private void OnEnable() => Reload();

    // ── 데이터 로드 ──────────────────────────────────────────────────────

    private void Reload()
    {
        _chapters.Clear();
        _dirty = false;

        var byChapter = new SortedDictionary<int, List<UnitEntry>>();

        DB_유닛.ForEachEntity(row =>
        {
            string id  = row.f_유닛ID ?? "";
            int    sep = id.IndexOf('_');
            if (sep < 2 || id[0] != 'C') return;
            if (!int.TryParse(id.Substring(1, sep - 1), out int ch)) return;

            if (!byChapter.ContainsKey(ch))
                byChapter[ch] = new List<UnitEntry>();

            var e = new UnitEntry
            {
                unitId   = id,
                typeId   = row.f_타입ID ?? "",
                designGW = Mathf.Max(1, row.f_최초기준),
                baseHp   = row.f_체력,
                hpRate   = row.f_체력비율,
                baseDmg  = row.f_데미지,
            };

            // 프리팹 로드
            string path = $"Assets/Resources/Unit/Ch{ch}/{id.ToLower()}.prefab";
            e.go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (e.go != null)
            {
                var bType = BehaviourType(e.typeId);
                if (bType != null)
                {
                    var comp = e.go.GetComponentInChildren(bType);
                    if (comp != null)
                    {
                        e.so = new SerializedObject(comp);
                        if (TypeCols.TryGetValue(e.typeId, out var cols))
                            foreach (var (_, pName, _) in cols)
                            {
                                var prop = e.so.FindProperty(pName);
                                if (prop != null) e.props[pName] = prop;
                            }
                    }
                }
            }

            byChapter[ch].Add(e);
        });

        foreach (var kv in byChapter)
        {
            kv.Value.Sort((a, b) => a.designGW.CompareTo(b.designGW));
            _chapters.Add(($"Ch.{kv.Key}", kv.Value));
        }

        if (_tab >= _chapters.Count) _tab = 0;
        Repaint();
    }

    // ── 저장 (프리팹만) ──────────────────────────────────────────────────

    private void Save()
    {
        foreach (var (_, entries) in _chapters)
        foreach (var e in entries)
        {
            if (e.so == null || e.go == null) continue;
            e.so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(e.go);
        }
        AssetDatabase.SaveAssets();
        _dirty = false;
        Debug.Log("[UnitBalanceViewer] 프리팹 저장 완료");
    }

    // ── GUI ──────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        DrawChapterToolbar();
        DrawTypeToolbar();

        if (_chapters.Count == 0)
        {
            EditorGUILayout.HelpBox("DB_유닛 데이터가 없습니다.", MessageType.Warning);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll, alwaysShowHorizontal: true, alwaysShowVertical: true);
        DrawHeader();

        int row = 0;
        foreach (var e in _chapters[_tab].entries)
        {
            if (_typeTab != "ALL" && e.typeId != _typeTab) continue;
            DrawRow(e, row++ % 2 == 1);
        }

        EditorGUILayout.EndScrollView();

        var footer = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
        GUILayout.Label("DB값(HP·비율·DMG·타입·최초기준): 읽기 전용   ★컬럼: 프리팹 편집 후 [저장] → 프리팹에만 반영", footer);
    }

    private void DrawChapterToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        for (int i = 0; i < _chapters.Count; i++)
        {
            bool sel = _tab == i;
            if (GUILayout.Toggle(sel, _chapters[i].name, EditorStyles.toolbarButton, GUILayout.Width(50)) && !sel)
            { _tab = i; _scroll = Vector2.zero; }
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("스테이지당 웨이브:", EditorStyles.miniLabel);
        _wps = EditorGUILayout.IntField(_wps, GUILayout.Width(32));
        GUILayout.Space(6);

        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60)))
            Reload();

        var prev = GUI.contentColor;
        GUI.contentColor = _dirty ? new Color(1f, 0.75f, 0.3f) : Color.white;
        if (GUILayout.Button(_dirty ? "● 저장" : "저장", EditorStyles.toolbarButton, GUILayout.Width(60)))
            Save();
        GUI.contentColor = prev;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTypeToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        foreach (var t in TypeTabs)
        {
            bool sel = _typeTab == t;
            float w   = t == "ALL" ? 40f : (t.Length > 6 ? 72f : 60f);
            if (GUILayout.Toggle(sel, t, EditorStyles.toolbarButton, GUILayout.Width(w)) && !sel)
            { _typeTab = t; _scroll = Vector2.zero; }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
        var h = new GUIStyle(EditorStyles.miniLabel)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        L("유닛ID",   W_ID,   h);
        L("타입",     W_TYPE, h);
        L("최초기준", W_GW,   h);
        L("기본HP",   W_HP,   h);
        L("HP비율",   W_RATE, h);
        L("기본DMG",  W_DMG,  h);

        if (_typeTab != "ALL" && TypeCols.TryGetValue(_typeTab, out var cols))
            foreach (var (hdr, _, w) in cols)
                L($"★{hdr}", w, h);

        foreach (var p in Previews)
            L($"S{p.s}W{p.w}\nHP", W_PRV, h);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(UnitEntry e, bool alt)
    {
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = alt ? new Color(0.85f, 0.85f, 0.85f, 0.12f) : Color.clear;
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        var lbl  = new GUIStyle(EditorStyles.miniLabel)    { alignment = TextAnchor.MiddleCenter };
        var gray = new GUIStyle(lbl)                        { normal = { textColor = new Color(0.45f, 0.45f, 0.45f) } };
        var edit = new GUIStyle(EditorStyles.miniTextField) { alignment = TextAnchor.MiddleCenter };

        // DB 읽기 전용
        L(e.unitId,              W_ID,   lbl);
        L(e.typeId,              W_TYPE, lbl);
        L(GwLabel(e.designGW),   W_GW,   lbl);
        L(e.baseHp.ToString(),   W_HP,   lbl);
        L(e.hpRate.ToString("0.##"), W_RATE, lbl);
        L(e.baseDmg.ToString(),  W_DMG,  lbl);

        // 타입별 프리팹 편집
        if (_typeTab != "ALL" && TypeCols.TryGetValue(_typeTab, out var cols))
        {
            if (e.so != null && e.typeId == _typeTab)
            {
                e.so.Update();
                foreach (var (_, pName, w) in cols)
                {
                    if (!e.props.TryGetValue(pName, out var prop)) { L("-", w, gray); continue; }

                    EditorGUI.BeginChangeCheck();
                    if (prop.propertyType == SerializedPropertyType.Integer)
                    {
                        int v = EditorGUILayout.IntField(prop.intValue, edit, GUILayout.Width(w), GUILayout.Height(ROW_H));
                        if (EditorGUI.EndChangeCheck()) { prop.intValue = v; e.so.ApplyModifiedPropertiesWithoutUndo(); _dirty = true; }
                    }
                    else
                    {
                        float v = EditorGUILayout.FloatField(prop.floatValue, edit, GUILayout.Width(w), GUILayout.Height(ROW_H));
                        if (EditorGUI.EndChangeCheck()) { prop.floatValue = v; e.so.ApplyModifiedPropertiesWithoutUndo(); _dirty = true; }
                    }
                }
            }
            else
            {
                // 타입 불일치 → 전체 회색 처리
                foreach (var (_, _, w) in cols) L("—", w, gray);
            }
        }

        // HP 미리보기
        foreach (var p in Previews)
        {
            int gw = (p.s - 1) * _wps + p.w;
            L(gw < e.designGW ? "—" : HpPreview(e.baseHp, e.hpRate, gw, e.designGW), W_PRV, gw < e.designGW ? gray : lbl);
        }

        EditorGUILayout.EndHorizontal();
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────

    private static void L(string text, float w, GUIStyle s)
        => GUILayout.Label(text, s, GUILayout.Width(w), GUILayout.Height(ROW_H));

    private string GwLabel(int gw)
    {
        int s = (gw - 1) / _wps + 1;
        int w = (gw - 1) % _wps + 1;
        return $"S{s}W{w}";
    }

    private string HpPreview(int baseHp, float rate, int gw, int designGW)
    {
        int total = Mathf.Max(0, gw - designGW);
        if (total == 0) return baseHp.ToString();
        int curS  = (gw       - 1) / _wps + 1;
        int desS  = (designGW - 1) / _wps + 1;
        int sD    = Mathf.Max(0, curS - desS);
        int wD    = Mathf.Max(0, total - sD * _wps);
        float mul = 1f + rate * sD + rate / _wps * wD;
        return Mathf.RoundToInt(baseHp * mul).ToString();
    }
}
#endif
