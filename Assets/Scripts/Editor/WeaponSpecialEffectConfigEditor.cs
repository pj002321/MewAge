#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 무기 오디오·VFX·특수효과 통합 에디터 (MuAge/Weapon Effect Config Editor).
/// ● 오디오·VFX 탭 : 모든 무기의 발사음·명중음·볼륨·타격VFX 설정
/// ● 특성 설정 탭 : 특성(Trait) 있는 무기의 발동확률·클론·스파이크 등 기존 설정
/// </summary>
public class WeaponEffectConfigEditor : EditorWindow
{
    // ── SO 레퍼런스 ──────────────────────────────────────────────────
    private StatusEffectConfig _config;
    private SerializedObject   _so;
    private SerializedProperty _entriesProp;
    private SoundLibrarySO     _soundLib;

    // ── 상태 ──────────────────────────────────────────────────────
    private int     _contentTab;  // 0=오디오·VFX, 1=스킬 할당
    private int     _chapterTab;
    private Vector2 _scroll;
    private bool    _chapterVfxFoldout = true;

    // ── 데이터 캐시 ───────────────────────────────────────────────
    private readonly List<(int chapter, List<DB_무기> weapons)> _chapters      = new();
    private readonly Dictionary<string, int>                     _indexMap      = new();
    private readonly Dictionary<string, SoundLibrarySO.WeaponSoundEntry> _soundMap = new();

    // ── 스킬 ID 목록 ──────────────────────────────────────────────
    private static readonly string[] SkillIds =
    {
        "",
        "HIT_STUN", "HIT_ICE", "HIT_FIRE",
        "HIT_CLONE_RETURN", "HIT_CLONE_SPREAD", "HIT_CLONE_BURST",
        "SHOOT_SUB",
        "HIT_SLASH",
        "HIT_SPIN",
        "HIT_SHOCK",
        "BOSS_SPIKE", "HIT_SPIKE",
        "BOSS_BIND",
        "BOSS_GROGY",
        "HIT_SPEAR",
        "HIT_CLONE_TRAIL",
        "BUFF_ATK", "BUFF_DEF", "BUFF_HP", "BUFF_SPD", "BUFF_CRI",
    };
    private static readonly string[] SkillLabels;
    static WeaponEffectConfigEditor()
    {
        SkillLabels = new string[SkillIds.Length];
        SkillLabels[0] = "(없음)";
        for (int i = 1; i < SkillIds.Length; i++) SkillLabels[i] = SkillIds[i];
    }

    [MenuItem("MuAge/Weapon Effect Config Editor")]
    public static void Open() => GetWindow<WeaponEffectConfigEditor>("무기 효과 설정");

    private void OnEnable() => Reload();
    private void OnFocus()  => Reload();

    // ─────────────────────────────────────────────────────────────
    // 데이터 로드 / 재빌드
    // ─────────────────────────────────────────────────────────────

    private void Reload()
    {
        LoadOrCreateConfig();
        LoadSoundLib();
        RebuildChapters();
        RebuildIndexMap();
        RebuildSoundMap();
        if (_chapterTab >= _chapters.Count) _chapterTab = 0;
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

    private void LoadSoundLib()
    {
        _soundLib = Resources.Load<SoundLibrarySO>("SoundLibrary");
    }

    private void RebuildChapters()
    {
        _chapters.Clear();
        var all = new SortedDictionary<int, List<DB_무기>>();
        DB_무기.ForEachEntity(row =>
        {
            int ch = ChapterFromId(row.f_무기ID, row.f_챕터);
            if (!all.ContainsKey(ch)) all[ch] = new List<DB_무기>();
            all[ch].Add(row);
        });
        foreach (var kv in all) _chapters.Add((kv.Key, kv.Value));
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

    private void RebuildSoundMap()
    {
        _soundMap.Clear();
        if (_soundLib == null) return;
        foreach (var preset in _soundLib.weaponTypePresets)
            foreach (var w in preset.weapons)
                if (w != null && !string.IsNullOrEmpty(w.weaponId))
                    _soundMap[w.weaponId] = w;
    }

    // ─────────────────────────────────────────────────────────────
    // OnGUI
    // ─────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();
        if (_config == null) { LoadOrCreateConfig(); return; }
        _so.Update();

        DrawChapterVfxSection();
        EditorGUILayout.Space(2);
        DrawContentToolbar();
        DrawChapterToolbar();

        if (_chapters.Count == 0)
        {
            EditorGUILayout.HelpBox("DB에 무기가 없습니다.", MessageType.Info);
        }
        else
        {
            if (_chapterTab >= _chapters.Count) _chapterTab = _chapters.Count - 1;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_contentTab == 0) DrawAVTable(_chapters[_chapterTab].weapons);
            else                  DrawSkillAssignList(_chapters[_chapterTab].weapons);
            EditorGUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();

        if (_contentTab == 1)
        {
            var footer = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            GUILayout.Label(
                "발동확률 0 = 효과별 기본값. HIT_STUN=25% HIT_ICE=30% HIT_FIRE=35% HIT_CLONE=25% HIT_SHOCK=40% HIT_SPIN=30% BOSS_SPIKE=25% BOSS_BIND=15% BOSS_GROGY=20% HIT_SPEAR=25% HIT_CLONE_TRAIL=30%",
                footer);
        }

        if (_so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            RebuildIndexMap();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 툴바
    // ─────────────────────────────────────────────────────────────

    private void DrawContentToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        string[] labels = { "오디오 · VFX", "스킬 할당" };
        for (int i = 0; i < labels.Length; i++)
        {
            bool sel = _contentTab == i;
            if (GUILayout.Toggle(sel, labels[i], EditorStyles.toolbarButton, GUILayout.Width(100)) && !sel)
            {
                _contentTab  = i;
                _chapterTab  = 0;
                _scroll      = Vector2.zero;
            }
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawChapterToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        for (int i = 0; i < _chapters.Count; i++)
        {
            bool sel = _chapterTab == i;
            if (GUILayout.Toggle(sel, $"Ch.{_chapters[i].chapter}", EditorStyles.toolbarButton, GUILayout.Width(46)) && !sel)
            {
                _chapterTab = i;
                _scroll     = Vector2.zero;
            }
        }
        GUILayout.FlexibleSpace();
        int cnt = _chapters.Count > 0 ? _chapters[Mathf.Clamp(_chapterTab, 0, _chapters.Count - 1)].weapons.Count : 0;
        GUILayout.Label($"{cnt}종", EditorStyles.miniLabel);
        GUILayout.Space(8);
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70)))
            Reload();
        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────
    // 오디오 · VFX 탭
    // ─────────────────────────────────────────────────────────────

    private static readonly string[] AVHeaders = { "무기ID", "이름", "타입", "발사음", "명중음", "볼륨", "타격 VFX" };
    private static readonly float[]  AVColW    = { 100f, 85f, 65f, 130f, 130f, 50f, 150f };

    private void DrawAVTable(List<DB_무기> weapons)
    {
        var h = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        for (int i = 0; i < AVHeaders.Length; i++)
            GUILayout.Label(AVHeaders[i], h, GUILayout.Width(AVColW[i]));
        EditorGUILayout.EndHorizontal();

        foreach (var row in weapons)
            DrawAVRow(row);
    }

    private void DrawAVRow(DB_무기 row)
    {
        string wid  = (row.f_무기ID  ?? "").Trim();
        string name = (row.f_이름    ?? "-").Trim();
        string type = (row.f_타입    ?? "-").Trim();

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        var lbl = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label(wid,  lbl, GUILayout.Width(AVColW[0]));
        GUILayout.Label(name, lbl, GUILayout.Width(AVColW[1]));
        GUILayout.Label(type, lbl, GUILayout.Width(AVColW[2]));

        // ── 사운드 (SoundLibrarySO) ─────────────────────────────
        if (_soundLib != null)
        {
            var soundEntry = GetOrCreateSoundEntry(wid, type);
            EditorGUI.BeginChangeCheck();
            var newShoot = (AudioClip)EditorGUILayout.ObjectField(
                soundEntry.shoot, typeof(AudioClip), false, GUILayout.Width(AVColW[3]));
            var newHit = (AudioClip)EditorGUILayout.ObjectField(
                soundEntry.hit, typeof(AudioClip), false, GUILayout.Width(AVColW[4]));
            float newVol = EditorGUILayout.Slider(soundEntry.volume, 0f, 1f, GUILayout.Width(AVColW[5]));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_soundLib, "Weapon Sound Edit");
                soundEntry.shoot  = newShoot;
                soundEntry.hit    = newHit;
                soundEntry.volume = newVol;
                EditorUtility.SetDirty(_soundLib);
            }
        }
        else
        {
            var gray = GetGrayLabel();
            GUILayout.Label("SoundLibrary 없음", gray, GUILayout.Width(AVColW[3] + AVColW[4] + AVColW[5]));
        }

        // ── 타격 VFX (StatusEffectConfig) ──────────────────────
        int idx  = GetOrCreateEntry(wid, "");
        var elem = _entriesProp.GetArrayElementAtIndex(idx);
        DrawPrefabField(elem.FindPropertyRelative("hitVfxPrefab"), 6, true, AVColW);

        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────
    // 스킬 할당 탭
    // ─────────────────────────────────────────────────────────────

    private void DrawSkillAssignList(List<DB_무기> weapons)
    {
        foreach (var row in weapons)
            DrawSkillCard(row);
    }

    private GUIStyle _subLblStyle;
    private GUIStyle _centerLbl;

    private void EnsureStyles()
    {
        _subLblStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        _centerLbl   ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
    }

    private void DrawSkillCard(DB_무기 row)
    {
        string wid    = (row.f_무기ID ?? "").Trim();
        string name   = (row.f_이름  ?? "-").Trim();
        string type   = (row.f_타입  ?? "-").Trim();
        string dbSkill = (row.f_특성 ?? "").Trim();

        int entryIdx = GetOrCreateEntry(wid, dbSkill);
        var elem     = _entriesProp.GetArrayElementAtIndex(entryIdx);
        var soSkillProp = elem.FindPropertyRelative("skillOverride");
        string soSkill  = soSkillProp.stringValue;

        // 실효 스킬 = SO override > DB 값
        string effectiveSkill = string.IsNullOrEmpty(soSkill) ? dbSkill : soSkill;

        EditorGUILayout.BeginVertical(GUI.skin.box);

        // ── Row 1: 무기 정보 + 스킬 드롭다운 ────────────────────
        EditorGUILayout.BeginHorizontal();

        // 무기 정보
        GUILayout.Label(wid,  _centerLbl, GUILayout.Width(90));
        GUILayout.Label(name, _centerLbl, GUILayout.Width(80));
        GUILayout.Label(type, _centerLbl, GUILayout.Width(60));
        GUILayout.Label("스킬:", _subLblStyle, GUILayout.Width(30));

        // 스킬 드롭다운
        int curIdx = System.Array.IndexOf(SkillIds, effectiveSkill);
        if (curIdx < 0) curIdx = 0;
        EditorGUI.BeginChangeCheck();
        int newIdx = EditorGUILayout.Popup(curIdx, SkillLabels, GUILayout.Width(155));
        if (EditorGUI.EndChangeCheck())
        {
            soSkillProp.stringValue = SkillIds[newIdx]; // skillOverride에 저장
        }

        // DB 원본 표시 (override 중일 때)
        if (!string.IsNullOrEmpty(soSkill) && soSkill != dbSkill)
        {
            var dbBadge = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.7f, 1f) } };
            GUILayout.Label($"DB:{(string.IsNullOrEmpty(dbSkill) ? "없음" : dbSkill)}", dbBadge, GUILayout.Width(90));
        }
        else if (!string.IsNullOrEmpty(dbSkill) && string.IsNullOrEmpty(soSkill))
        {
            var dbBadge = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            GUILayout.Label("DB", dbBadge, GUILayout.Width(25));
        }

        // 클리어 버튼
        if (!string.IsNullOrEmpty(soSkill))
        {
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                soSkillProp.stringValue = "";
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // ── Row 2: 스킬별 파라미터 ───────────────────────────────
        if (!string.IsNullOrEmpty(effectiveSkill))
            DrawSkillParams(elem, effectiveSkill);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(1);
    }

    private void DrawSkillParams(SerializedProperty elem, string skillId)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20); // 들여쓰기

        bool isStun   = skillId is "HIT_STUN" or "HIT_ICE" or "HIT_FIRE";
        bool isTrail  = skillId == "HIT_CLONE_TRAIL";
        bool isClone  = !isTrail && skillId.StartsWith("HIT_CLONE");
        bool isSub    = skillId == "SHOOT_SUB";
        bool isSlash  = skillId == "HIT_SLASH";
        bool isSpin   = skillId == "HIT_SPIN";
        bool isShock  = skillId == "HIT_SHOCK";
        bool isSpike  = skillId is "BOSS_SPIKE" or "HIT_SPIKE";
        bool isBind   = skillId == "BOSS_BIND";
        bool isGroggy = skillId == "BOSS_GROGY";
        bool isSpear  = skillId == "HIT_SPEAR";
        bool isBuff   = skillId.StartsWith("BUFF_");

        // 발동확률 (공통 - 스킬이 있는 모든 케이스)
        if (!isSpin && !isSlash && !isBuff)
        {
            DrawLabeledFloat(elem.FindPropertyRelative("triggerChance"), "발동확률", 55f);
        }

        if (isStun)
        {
            DrawLabeledFloat(elem.FindPropertyRelative("p_stunDuration"), "지속(s)", 50f);
            DrawLabeledEnum (elem.FindPropertyRelative("hitStunType"),    "타입",    70f);
            DrawLabeledColor(elem.FindPropertyRelative("hitStunColor"),   "색상",    80f);
            DrawLabeledPrefab(elem.FindPropertyRelative("hitEffectVfxPrefab"), "효과VFX", 130f);
            if (skillId == "HIT_ICE")
                DrawLabeledFloat(elem.FindPropertyRelative("p_freezeThawDmg"), "해빙피해", 55f);
        }
        else if (isClone)
        {
            DrawLabeledPrefab(elem.FindPropertyRelative("cloneProjectilePrefab"), "클론프리팹", 130f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_cloneSpeed"),    "속도",   50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_cloneLifetime"), "생존(s)", 50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_cloneScale"),    "크기",   45f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_cloneAlpha"),    "투명도", 50f);
            if (skillId == "HIT_CLONE_RETURN")
            {
                DrawLabeledFloat(elem.FindPropertyRelative("cloneRange"),       "사거리",  45f);
                DrawLabeledFloat(elem.FindPropertyRelative("cloneOutTime"),     "전진(s)", 45f);
                DrawLabeledFloat(elem.FindPropertyRelative("clonePauseTime"),   "정지(s)", 45f);
                DrawLabeledFloat(elem.FindPropertyRelative("cloneReturnTime"),  "복귀(s)", 45f);
            }
        }
        else if (isSub)
        {
            DrawLabeledPrefab(elem.FindPropertyRelative("subProjectilePrefab"), "서브프리팹", 130f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spreadAngle"),  "각도",    50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_subDmgRatio"),  "피해비율", 55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_subSpeedRatio"),"속도비율", 55f);
        }
        else if (isSlash)
        {
            DrawLabeledPrefab(elem.FindPropertyRelative("p_slashPrefab"), "슬래시프리팹", 140f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_slashDmgRatio"), "피해비율", 55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_slashSpeed"),    "속도",    50f);
        }
        else if (isSpin)
        {
            DrawLabeledPrefab(elem.FindPropertyRelative("cloneProjectilePrefab"), "클론프리팹", 130f);
            DrawLabeledToggle(elem.FindPropertyRelative("hitSpinPermanent"), "영구공전", 55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spinRadiusMult"),  "반경배율",  55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spinSpeedMult"),   "속도배율",  55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spinCloneChance"), "클론확률",  55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spinCloneRadius"), "클론반경",  55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spinCloneSpeed"),  "클론속도",  55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spinCloneDuration"),"클론시간", 55f);
            DrawLabeledInt  (elem.FindPropertyRelative("p_spinCloneMax"),    "최대클론",  50f);
        }
        else if (isShock)
        {
            DrawLabeledFloat(elem.FindPropertyRelative("p_shockRadius"),   "반경",    50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_shockDmgRatio"), "피해비율", 55f);
        }
        else if (isSpike)
        {
            DrawLabeledPrefab(elem.FindPropertyRelative("spikePrefab"),        "스파이크프리팹", 140f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spikeTickDmg"),     "틱피해",   50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spikeTickInterval"), "틱간격",   45f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spikeDuration"),    "지속(s)",  45f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spikeDeathRadius"), "폭발반경", 50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spikeDeathDmg"),    "폭발피해", 50f);
        }
        else if (isBind)
        {
            DrawLabeledFloat(elem.FindPropertyRelative("p_bindDuration"), "구속(s)", 50f);
        }
        else if (isGroggy)
        {
            DrawLabeledFloat(elem.FindPropertyRelative("p_groggySeconds"), "그로기(s)", 55f);
        }
        else if (isSpear)
        {
            DrawLabeledPrefab(elem.FindPropertyRelative("p_spearPrefab"),      "창프리팹",    140f);
            DrawLabeledPrefab(elem.FindPropertyRelative("p_spearLandingVfx"), "착지VFX",    140f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spearDropHeight"),   "낙하높이",   50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spearSideOffset"),   "수평오프셋", 50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spearSpeed"),        "속도",       50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spearDmgRatio"),     "피해비율",   55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_spearHitRadius"),    "타격반경",   50f);
        }
        else if (isTrail)
        {
            DrawLabeledPrefab(elem.FindPropertyRelative("p_trailClonePrefab"), "클론프리팹", 140f);
            DrawLabeledPrefab(elem.FindPropertyRelative("p_trailMarkerPrefab"),"마커프리팹", 140f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_trailSpeed"),        "속도",      50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_trailRange"),        "거리",      50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_trailDmgRatio"),     "피해비율",  55f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_trailDuration"),     "마커지속",  50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_trailSpawnInterval"),"생성간격",  50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_trailHitRadius"),    "타격반경",  50f);
            DrawLabeledFloat(elem.FindPropertyRelative("p_trailCloneAlpha"),   "투명도",    50f);
        }
        else if (isBuff)
        {
            DrawLabeledFloat(elem.FindPropertyRelative("passiveBuffPct"),       "버프%",  50f);
            DrawLabeledPrefab(elem.FindPropertyRelative("passiveBuffVfxPrefab"),"버프VFX",130f);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // ── 인라인 레이블 + 필드 헬퍼 ───────────────────────────────────

    private float LblW(string label)
    {
        var style = _subLblStyle ?? EditorStyles.miniLabel;
        return style.CalcSize(new GUIContent(label)).x + 4f;
    }

    private void DrawLabeledFloat(SerializedProperty p, string label, float fieldW)
    {
        GUILayout.Label(label, _subLblStyle, GUILayout.Width(LblW(label)));
        EditorGUI.BeginChangeCheck();
        float v = EditorGUILayout.FloatField(p.floatValue, GUILayout.Width(fieldW));
        if (EditorGUI.EndChangeCheck()) p.floatValue = v;
        GUILayout.Space(4);
    }

    private void DrawLabeledInt(SerializedProperty p, string label, float fieldW)
    {
        GUILayout.Label(label, _subLblStyle, GUILayout.Width(LblW(label)));
        EditorGUI.BeginChangeCheck();
        int v = EditorGUILayout.IntField(p.intValue, GUILayout.Width(fieldW));
        if (EditorGUI.EndChangeCheck()) p.intValue = v;
        GUILayout.Space(4);
    }

    private void DrawLabeledEnum(SerializedProperty p, string label, float fieldW)
    {
        GUILayout.Label(label, _subLblStyle, GUILayout.Width(LblW(label)));
        EditorGUI.BeginChangeCheck();
        int v = EditorGUILayout.Popup(p.enumValueIndex, p.enumDisplayNames, GUILayout.Width(fieldW));
        if (EditorGUI.EndChangeCheck()) p.enumValueIndex = v;
        GUILayout.Space(4);
    }

    private void DrawLabeledColor(SerializedProperty p, string label, float fieldW)
    {
        GUILayout.Label(label, _subLblStyle, GUILayout.Width(LblW(label)));
        EditorGUI.BeginChangeCheck();
        Color v = EditorGUILayout.ColorField(p.colorValue, GUILayout.Width(fieldW));
        if (EditorGUI.EndChangeCheck()) p.colorValue = v;
        GUILayout.Space(4);
    }

    private void DrawLabeledToggle(SerializedProperty p, string label, float fieldW)
    {
        GUILayout.Label(label, _subLblStyle, GUILayout.Width(LblW(label)));
        EditorGUI.BeginChangeCheck();
        bool v = EditorGUILayout.Toggle(p.boolValue, GUILayout.Width(fieldW));
        if (EditorGUI.EndChangeCheck()) p.boolValue = v;
        GUILayout.Space(4);
    }

    private void DrawLabeledPrefab(SerializedProperty p, string label, float fieldW)
    {
        GUILayout.Label(label, _subLblStyle, GUILayout.Width(LblW(label)));
        EditorGUI.BeginChangeCheck();
        var v = EditorGUILayout.ObjectField(p.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(fieldW));
        if (EditorGUI.EndChangeCheck()) p.objectReferenceValue = v;
        GUILayout.Space(4);
    }

    // ─────────────────────────────────────────────────────────────
    // 챕터별 VFX 섹션 (공통)
    // ─────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────
    // 공통 필드 드로어 헬퍼
    // ─────────────────────────────────────────────────────────────

    private static GUIStyle GetGrayLabel()
        => new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };

    private void DrawFloatField(SerializedProperty prop, int col, bool active, float[] colW)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(colW[col])); return; }
        EditorGUI.BeginChangeCheck();
        float val = EditorGUILayout.FloatField(prop.floatValue, GUILayout.Width(colW[col]));
        if (EditorGUI.EndChangeCheck()) prop.floatValue = val;
    }

    private void DrawPrefabField(SerializedProperty prop, int col, bool active, float[] colW)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(colW[col])); return; }
        EditorGUI.BeginChangeCheck();
        var obj = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(colW[col]));
        if (EditorGUI.EndChangeCheck()) prop.objectReferenceValue = obj;
    }

    private void DrawEnumField(SerializedProperty prop, int col, bool active, float[] colW)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(colW[col])); return; }
        EditorGUI.BeginChangeCheck();
        int val = EditorGUILayout.Popup(prop.enumValueIndex, prop.enumDisplayNames, GUILayout.Width(colW[col]));
        if (EditorGUI.EndChangeCheck()) prop.enumValueIndex = val;
    }

    private void DrawColorField(SerializedProperty prop, int col, bool active, float[] colW)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(colW[col])); return; }
        EditorGUI.BeginChangeCheck();
        Color val = EditorGUILayout.ColorField(prop.colorValue, GUILayout.Width(colW[col]));
        if (EditorGUI.EndChangeCheck()) prop.colorValue = val;
    }

    private void DrawToggleField(SerializedProperty prop, int col, bool active, float[] colW)
    {
        if (!active) { GUILayout.Label("-", GetGrayLabel(), GUILayout.Width(colW[col])); return; }
        EditorGUI.BeginChangeCheck();
        bool val = EditorGUILayout.Toggle(prop.boolValue, GUILayout.Width(colW[col]));
        if (EditorGUI.EndChangeCheck()) prop.boolValue = val;
    }

    // ─────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────

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

    private SoundLibrarySO.WeaponSoundEntry GetOrCreateSoundEntry(string weaponId, string weaponTypeStr)
    {
        if (_soundMap.TryGetValue(weaponId, out var e)) return e;
        if (_soundLib == null) return new SoundLibrarySO.WeaponSoundEntry { weaponId = weaponId };

        var type   = ParseWeaponType(weaponTypeStr);
        var preset = _soundLib.weaponTypePresets.Find(p => p.weaponType == type);
        if (preset == null)
        {
            preset = new SoundLibrarySO.WeaponTypeSoundPreset { weaponType = type };
            Undo.RecordObject(_soundLib, "Add Weapon Sound Entry");
            _soundLib.weaponTypePresets.Add(preset);
        }
        var entry = new SoundLibrarySO.WeaponSoundEntry { weaponId = weaponId, volume = 1f };
        if (!preset.weapons.Contains(entry))
        {
            Undo.RecordObject(_soundLib, "Add Weapon Sound Entry");
            preset.weapons.Add(entry);
            EditorUtility.SetDirty(_soundLib);
        }
        _soundMap[weaponId] = entry;
        return entry;
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

    private static WeaponType ParseWeaponType(string s)
    {
        if (string.IsNullOrEmpty(s)) return WeaponType.Melee;
        if (System.Enum.TryParse<WeaponType>(s, true, out var t)) return t;
        return s switch
        {
            "근거리"                          => WeaponType.Melee,
            "원거리"                          => WeaponType.Ranged,
            "마법" or "번개" or "불" or "얼음" => WeaponType.Magic,
            "부메랑"                          => WeaponType.Boomerang,
            "돌리기"                          => WeaponType.Spin,
            "슬래시" or "베기"                => WeaponType.Slash,
            "차지" or "차징"                  => WeaponType.Charge,
            "해머" or "망치"                  => WeaponType.Hammer,
            _                                 => WeaponType.Melee,
        };
    }
}
#endif
