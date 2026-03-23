using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// MuAge ▸ Enemy Preset Editor
/// 챕터별 적 프리팹을 한 창에서 통합 편집합니다.
/// </summary>
public class EnemyPresetWindow : EditorWindow
{
    // ── 목록 상태 ──────────────────────────────────────────────────
    private List<ChapterRosterSO> rosters    = new();
    private int                   selChapter = 0;
    private int                   selIdx     = -1;
    private const int             BOSS_IDX   = -2;

    private Vector2 scrollLeft, scrollRight;
    private int     activeTab = 0;
    private bool    showWaveRules = false;

    private static readonly string[] TabLabels =
        { "  Visual  ", "  Stats  ", "  Behaviour  ", "  VFX / SFX  ", "  Animation  " };

    private ReorderableList  reoList;
    private ChapterRosterSO  reoListRoster;

    private int newBehaviourIdx = 0;
    private static readonly string[] BehaviourNames =
        { "-- 선택 --", "Melee", "Ranged", "Horde", "Tank", "Charge", "Boss", "Magic", "Healer" };

    // ── 애니메이션 미리보기 ────────────────────────────────────────
    private AnimationClip   animClip;
    private AnimationClip[] animClips;
    private string[]        animClipNames;
    private int             animClipIdx    = 0;
    private float           animTime       = 0f;
    private bool            animPlaying    = false;
    private bool            animLoop       = true;
    private double          lastUpdateTime = 0;
    private float           vfxFlashTimer  = 0f;
    private const float     VFX_FLASH_DUR  = 0.4f;
    private int             selEventIdx    = -1;
    private GameObject      lastAnimPrefab = null;

    // (AnimationMode 프리뷰 씬은 사용하지 않음 — 클립 커브를 직접 읽습니다)

    // ── 저장 설정 ─────────────────────────────────────────────────
    private string saveFolder   = "Assets/Data";
    private string prefabFolder = "Assets/Prefabs/Enemies";
    private bool   showSaveCfg  = false;
    private const string PREF_SAVE   = "MuAge_PresetSaveFolder";
    private const string PREF_PREFAB = "MuAge_PrefabSaveFolder";

    // ══════════════════════════════════════════════════════════════
    //  Open / Enable / Disable
    // ══════════════════════════════════════════════════════════════

    [MenuItem("MuAge/Enemy Preset Editor", priority = 200)]
    public static void Open()
    {
        var w = GetWindow<EnemyPresetWindow>("Enemy Presets");
        w.minSize = new Vector2(860, 560);
        w.Show();
    }

    private void OnEnable()
    {
        saveFolder   = EditorPrefs.GetString(PREF_SAVE,   "Assets/Data");
        prefabFolder = EditorPrefs.GetString(PREF_PREFAB, "Assets/Prefabs/Enemies");
        EditorApplication.update += OnEditorUpdate;
        Refresh();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        animPlaying = false;
    }

    // ══════════════════════════════════════════════════════════════
    //  Data helpers
    // ══════════════════════════════════════════════════════════════

    private void Refresh()
    {
        rosters = AssetDatabase.FindAssets("t:ChapterRosterSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<ChapterRosterSO>(
                             AssetDatabase.GUIDToAssetPath(g)))
            .Where(r => r != null)
            .OrderBy(r => r.chapter)
            .ToList();

        selChapter = Mathf.Clamp(selChapter, 0, Mathf.Max(0, rosters.Count - 1));
        selIdx     = -1;
        reoList    = null;
    }

    private ChapterRosterSO Roster =>
        rosters.Count > 0 ? rosters[selChapter] : null;

    private GameObject SelPrefab
    {
        get
        {
            var r = Roster;
            if (r == null) return null;
            if (selIdx == BOSS_IDX) return r.bossPrefab;
            if (selIdx >= 0 && selIdx < r.normalEnemies.Count)
                return r.normalEnemies[selIdx].prefab;
            return null;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Root GUI
    // ══════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
        DrawLeft();
        DrawDivider();
        DrawRight();
        EditorGUILayout.EndHorizontal();
    }

    // ══════════════════════════════════════════════════════════════
    //  Toolbar
    // ══════════════════════════════════════════════════════════════

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(56)))
            Refresh();

        GUILayout.Space(4);

        if (GUILayout.Button("+ 챕터 로스터", EditorStyles.toolbarButton, GUILayout.Width(100)))
            CreateRoster();

        if (GUILayout.Button("+ 새 적 프리팹", EditorStyles.toolbarButton, GUILayout.Width(100)))
            CreatePrefab();

        GUILayout.Space(6);

        // 저장 버튼
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.45f, 1f, 0.55f, 1f);
        if (GUILayout.Button("💾  저장", EditorStyles.toolbarButton, GUILayout.Width(68)))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyPresets] 저장 완료");
        }
        GUI.backgroundColor = prevBg;

        // 저장 경로 설정 토글
        showSaveCfg = GUILayout.Toggle(showSaveCfg, "⚙", EditorStyles.toolbarButton, GUILayout.Width(22));

        GUILayout.FlexibleSpace();

        if (rosters.Count > 0)
        {
            var labels = rosters.Select(r => r.chapterName).ToArray();
            int next   = GUILayout.Toolbar(selChapter, labels, EditorStyles.toolbarButton);
            if (next != selChapter) { selChapter = next; selIdx = -1; reoList = null; }
        }
        else
        {
            GUILayout.Label("챕터 없음", EditorStyles.toolbarButton);
        }

        EditorGUILayout.EndHorizontal();

        // 저장 경로 설정 패널
        if (showSaveCfg)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField("로스터 폴더", GUILayout.Width(70));
            saveFolder = EditorGUILayout.TextField(saveFolder, GUILayout.Width(200));
            if (GUILayout.Button("…", GUILayout.Width(22)))
            {
                var s = EditorUtility.OpenFolderPanel("로스터 저장 폴더", saveFolder, "");
                if (!string.IsNullOrEmpty(s))
                {
                    saveFolder = ToAssetPath(s);
                    EditorPrefs.SetString(PREF_SAVE, saveFolder);
                }
            }

            GUILayout.Space(12);

            EditorGUILayout.LabelField("프리팹 폴더", GUILayout.Width(70));
            prefabFolder = EditorGUILayout.TextField(prefabFolder, GUILayout.Width(200));
            if (GUILayout.Button("…", GUILayout.Width(22)))
            {
                var s = EditorUtility.OpenFolderPanel("프리팹 저장 폴더", prefabFolder, "");
                if (!string.IsNullOrEmpty(s))
                {
                    prefabFolder = ToAssetPath(s);
                    EditorPrefs.SetString(PREF_PREFAB, prefabFolder);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Left panel
    // ══════════════════════════════════════════════════════════════

    private void DrawLeft()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(220), GUILayout.ExpandHeight(true));

        var roster = Roster;
        if (roster == null)
        {
            EditorGUILayout.HelpBox("상단 '+ 챕터 로스터' 로 챕터를 추가하세요.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        var rSo = new SerializedObject(roster);
        rSo.Update();
        EditorGUILayout.LabelField("챕터 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rSo.FindProperty("chapter"),     new GUIContent("번호"));
        EditorGUILayout.PropertyField(rSo.FindProperty("chapterName"), new GUIContent("이름"));
        rSo.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        showWaveRules = EditorGUILayout.Foldout(showWaveRules, "웨이브 스폰 규칙", true, EditorStyles.foldoutHeader);
        if (showWaveRules)
        {
            var rulesSo = new SerializedObject(roster);
            rulesSo.Update();
            EditorGUILayout.PropertyField(rulesSo.FindProperty("waveRules"), new GUIContent("규칙 목록"), true);
            rulesSo.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("적 목록", EditorStyles.boldLabel);

        scrollLeft = EditorGUILayout.BeginScrollView(scrollLeft, GUILayout.ExpandHeight(true));

        if (reoList == null || reoListRoster != roster)
            BuildReorderableList(roster);

        var listSo = new SerializedObject(roster);
        listSo.Update();
        reoList.DoLayoutList();
        listSo.ApplyModifiedProperties();

        EditorGUILayout.Space(4);

        bool bossSelected = selIdx == BOSS_IDX;
        var  bossRect     = EditorGUILayout.BeginHorizontal();
        if (bossSelected) EditorGUI.DrawRect(bossRect, new Color(1f, 0.75f, 0.2f, 0.18f));
        EditorGUILayout.LabelField("★  보스", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(bossSelected ? "▶" : "  ", GUILayout.Width(22)))
        {
            selIdx = BOSS_IDX; activeTab = 0;
            RefreshAnimClips(SelPrefab);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ 항목 추가", GUILayout.Height(22)))
        {
            Undo.RecordObject(roster, "Add Spawn Entry");
            roster.normalEnemies.Add(new EnemySpawnEntry());
            EditorUtility.SetDirty(roster);
            reoList = null;
        }

        GUILayout.Space(2);
        if (GUILayout.Button("로스터 Asset 찾기", EditorStyles.miniButton))
            EditorGUIUtility.PingObject(roster);

        EditorGUILayout.EndVertical();
    }

    private void BuildReorderableList(ChapterRosterSO roster)
    {
        reoListRoster = roster;
        var so   = new SerializedObject(roster);
        var prop = so.FindProperty("normalEnemies");

        reoList = new ReorderableList(so, prop, true, false, false, true)
        {
            elementHeight = EditorGUIUtility.singleLineHeight + 5
        };

        reoList.drawElementCallback = (rect, i, active, focused) =>
        {
            if (i >= roster.normalEnemies.Count) return;
            rect.y += 2; rect.height = EditorGUIUtility.singleLineHeight;

            if (selIdx == i || active)
                EditorGUI.DrawRect(rect, new Color(0.3f, 0.55f, 1f, 0.2f));

            if (Event.current.type == EventType.MouseDown &&
                rect.Contains(Event.current.mousePosition))
            {
                selIdx = i; activeTab = 0;
                RefreshAnimClips(roster.normalEnemies[i].prefab);
                Repaint();
            }

            var   e    = roster.normalEnemies[i];
            float half = rect.width * 0.55f;
            string nm  = string.IsNullOrEmpty(e.displayName) ? $"적 {i}" : e.displayName;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, half, rect.height), nm);
            EditorGUI.LabelField(new Rect(rect.x + half, rect.y, rect.width - half, rect.height),
                $"W:{e.weight}  [{e.minWave}~{(e.maxWave == 0 ? "∞" : e.maxWave.ToString())}]",
                EditorStyles.miniLabel);
        };

        reoList.onSelectCallback = l =>
        {
            selIdx = l.index; activeTab = 0;
            RefreshAnimClips(roster.normalEnemies[l.index].prefab);
        };

        reoList.onRemoveCallback = l =>
        {
            Undo.RecordObject(roster, "Remove Spawn Entry");
            roster.normalEnemies.RemoveAt(l.index);
            EditorUtility.SetDirty(roster);
            selIdx  = -1;
            reoList = null;
        };
    }

    // ══════════════════════════════════════════════════════════════
    //  Right panel
    // ══════════════════════════════════════════════════════════════

    private void DrawRight()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        var prefab = SelPrefab;

        if (prefab == null)
        {
            if (selIdx == BOSS_IDX) DrawBossAssign();
            else                    DrawEmptyHint();
            EditorGUILayout.EndVertical();
            return;
        }

        // 보스 선택 시 프리팹 필드 + 생성 버튼을 항상 상단에 표시
        if (selIdx == BOSS_IDX) DrawBossAssign();

        DrawHeader(prefab);
        activeTab   = GUILayout.Toolbar(activeTab, TabLabels, GUILayout.Height(22));
        scrollRight = EditorGUILayout.BeginScrollView(scrollRight);
        EditorGUILayout.Space(4);

        switch (activeTab)
        {
            case 0: DrawVisual(prefab);    break;
            case 1: DrawStats(prefab);     break;
            case 2: DrawBehaviour(prefab); break;
            case 3: DrawFX(prefab);        break;
            case 4: DrawAnimation(prefab); break;
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static void DrawEmptyHint()
    {
        GUILayout.FlexibleSpace();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("← 왼쪽에서 적을 선택하거나 추가하세요",
                            EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
        }
        GUILayout.FlexibleSpace();
    }

    private void DrawBossAssign()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("보스 프리팹 설정", EditorStyles.boldLabel);

        if (Roster == null)
        {
            EditorGUILayout.HelpBox("먼저 챕터 로스터를 생성하세요.", MessageType.Warning);
            return;
        }

        var rSo = new SerializedObject(Roster);
        rSo.Update();
        EditorGUILayout.PropertyField(rSo.FindProperty("bossPrefab"), new GUIContent("프리팹"));
        rSo.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("★  새 보스 프리팹 생성", GUILayout.Height(28)))
            CreateBossPrefab();
        EditorGUILayout.Space(6);
    }

    private void CreateBossPrefab()
    {
        EnsureFolder(prefabFolder);
        var path = EditorUtility.SaveFilePanelInProject(
            "보스 프리팹 생성", "NewBoss", "prefab",
            "저장 위치 선택", prefabFolder);
        if (string.IsNullOrEmpty(path)) return;

        var go = new GameObject("NewBoss");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<Animator>();
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        go.AddComponent<EnemyController>();
        go.AddComponent<BossBehaviour>();
        go.AddComponent<BossController>();
        go.AddComponent<EnemyFXConfig>();

        var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();

        // 현재 챕터 로스터의 bossPrefab 에 자동 할당
        var roster = Roster;
        if (roster != null)
        {
            Undo.RecordObject(roster, "Set Boss Prefab");
            roster.bossPrefab = saved;
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
        }

        // 바로 편집 가능하도록 선택 유지 (SelPrefab이 이제 non-null)
        Repaint();
    }

    private void DrawHeader(GameObject prefab)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        var sr = prefab.GetComponent<SpriteRenderer>();
        if (sr?.sprite != null)
        {
            var tex = AssetPreview.GetAssetPreview(sr.sprite);
            if (tex != null) GUILayout.Label(tex, GUILayout.Width(52), GUILayout.Height(52));
        }

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(prefab.name, EditorStyles.boldLabel);

        if (selIdx >= 0 && selIdx < Roster.normalEnemies.Count)
        {
            var e = Roster.normalEnemies[selIdx];
            e.displayName = EditorGUILayout.TextField("이름", e.displayName);

            // 가중치 / 웨이브 범위 — 레이블 고정폭, 값칸은 남은 공간 분할
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("가중치", GUILayout.Width(44));
            e.weight  = EditorGUILayout.IntField(e.weight,  GUILayout.Width(40));
            GUILayout.Space(6);
            EditorGUILayout.LabelField("최소", GUILayout.Width(28));
            e.minWave = EditorGUILayout.IntField(e.minWave, GUILayout.Width(40));
            GUILayout.Space(6);
            EditorGUILayout.LabelField("최대(0=∞)", GUILayout.Width(62));
            e.maxWave = EditorGUILayout.IntField(e.maxWave, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
            EditorUtility.SetDirty(Roster);
        }
        else if (selIdx == BOSS_IDX)
        {
            EditorGUILayout.LabelField("★  보스", EditorStyles.miniLabel);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Prefab 편집기 열기",  GUILayout.Width(120))) AssetDatabase.OpenAsset(prefab);
        if (GUILayout.Button("Project에서 찾기",    GUILayout.Width(110))) EditorGUIUtility.PingObject(prefab);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    // ══════════════════════════════════════════════════════════════
    //  Tab 0 — Visual
    // ══════════════════════════════════════════════════════════════

    private static void DrawVisual(GameObject prefab)
    {
        var sr   = prefab.GetComponent<SpriteRenderer>();
        var anim = prefab.GetComponent<Animator>();

        if (sr != null)
        {
            EditorGUILayout.LabelField("Sprite Renderer", EditorStyles.boldLabel);
            var so = new SerializedObject(sr);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("m_Sprite"), new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(so.FindProperty("m_Color"),  new GUIContent("Color"));
            EditorGUILayout.PropertyField(so.FindProperty("m_FlipX"),  new GUIContent("Flip X"));
            so.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(8);

        if (anim != null)
        {
            EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel);
            var so = new SerializedObject(anim);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("m_Controller"), new GUIContent("Controller"));
            so.ApplyModifiedProperties();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Tab 1 — Stats
    // ══════════════════════════════════════════════════════════════

    private static void DrawStats(GameObject prefab)
    {
        var ec = prefab.GetComponent<EnemyController>();
        if (ec == null) { EditorGUILayout.HelpBox("EnemyController 없음", MessageType.Warning); return; }

        var so = new SerializedObject(ec);
        so.Update();

        EditorGUILayout.LabelField("스탯", EditorStyles.boldLabel);
        Prop(so, "maxHP",          "최대 체력");
        Prop(so, "moveSpeed",      "이동 속도");
        Prop(so, "contactDamage",  "접촉 데미지");
        Prop(so, "damageCooldown", "데미지 쿨타임");
        Prop(so, "contactMaxDist", "접촉 최대 거리");

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("보상", EditorStyles.boldLabel);
        Prop(so, "goldDrop",   "골드");
        Prop(so, "expDrop",    "경험치");
        Prop(so, "scoreValue", "점수");

        so.ApplyModifiedProperties();
    }

    // ══════════════════════════════════════════════════════════════
    //  Tab 2 — Behaviour
    // ══════════════════════════════════════════════════════════════

    private void DrawBehaviour(GameObject prefab)
    {
        var behaviour = prefab.GetComponents<EnemyBehaviourBase>().FirstOrDefault();

        if (behaviour != null)
        {
            EditorGUILayout.LabelField($"타입:  {behaviour.GetType().Name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            var so = new SerializedObject(behaviour);
            so.Update();
            var iter  = so.GetIterator();
            bool enter = true;
            while (iter.NextVisible(enter))
            {
                enter = false;
                if (iter.name == "m_Script") continue;
                EditorGUILayout.PropertyField(iter, includeChildren: true);
            }
            so.ApplyModifiedProperties();
        }
        else
        {
            EditorGUILayout.HelpBox("부착된 Behaviour가 없습니다.", MessageType.Info);
        }

        var bc = prefab.GetComponent<BossController>();
        if (bc != null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("BossController", EditorStyles.boldLabel);
            var so = new SerializedObject(bc);
            so.Update();
            var iter  = so.GetIterator();
            bool enter = true;
            while (iter.NextVisible(enter))
            {
                enter = false;
                if (iter.name == "m_Script") continue;
                EditorGUILayout.PropertyField(iter, includeChildren: true);
            }
            so.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Behaviour 추가 / 교체", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        newBehaviourIdx = EditorGUILayout.Popup("타입 선택", newBehaviourIdx, BehaviourNames);
        GUI.enabled     = newBehaviourIdx > 0;
        if (GUILayout.Button("적용", GUILayout.Width(56))) ApplyBehaviour(prefab, newBehaviourIdx);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    // ══════════════════════════════════════════════════════════════
    //  Tab 3 — VFX / SFX
    // ══════════════════════════════════════════════════════════════

    private static void DrawFX(GameObject prefab)
    {
        var fx = prefab.GetComponent<EnemyFXConfig>();
        if (fx == null)
        {
            EditorGUILayout.HelpBox("EnemyFXConfig 없음.", MessageType.Info);
            if (GUILayout.Button("EnemyFXConfig 추가", GUILayout.Height(28)))
                AddToPrefab<EnemyFXConfig>(prefab);
            return;
        }

        var so = new SerializedObject(fx);
        so.Update();
        EffectGroup(so, "onAttack", "⚔   공격");
        EffectGroup(so, "onHit",    "💥  피격");
        EffectGroup(so, "onDeath",  "💀  사망");
        EffectGroup(so, "onSpawn",  "✨  스폰");
        so.ApplyModifiedProperties();
    }

    private static void EffectGroup(SerializedObject so, string field, string label)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        var p = so.FindProperty(field);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(p.FindPropertyRelative("vfxPrefab"),   new GUIContent("VFX 프리팹"));
        EditorGUILayout.PropertyField(p.FindPropertyRelative("spawnOffset"),  new GUIContent("스폰 오프셋"));
        EditorGUILayout.PropertyField(p.FindPropertyRelative("sfxClip"),     new GUIContent("사운드 클립"));
        EditorGUILayout.PropertyField(p.FindPropertyRelative("volume"),      new GUIContent("볼륨"));
        EditorGUI.indentLevel--;
    }

    // ══════════════════════════════════════════════════════════════
    //  Tab 4 — Animation
    // ══════════════════════════════════════════════════════════════

    private void DrawAnimation(GameObject prefab)
    {
        if (lastAnimPrefab != prefab)
            RefreshAnimClips(prefab);

        if (animClips == null || animClips.Length == 0)
        {
            EditorGUILayout.HelpBox("AnimatorController 가 없거나 클립이 없습니다.", MessageType.Info);
            return;
        }

        // ── 재생 컨트롤 ──────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("클립", GUILayout.Width(28));
        int newIdx = EditorGUILayout.Popup(animClipIdx, animClipNames, GUILayout.Width(160));
        if (newIdx != animClipIdx)
        {
            animClipIdx = newIdx;
            animClip    = animClips[animClipIdx];
            animTime    = 0f;
            animPlaying = false;
            selEventIdx = -1;
        }

        GUILayout.Space(8);

        if (GUILayout.Button(animPlaying ? "⏸" : "▶", GUILayout.Width(28)))
        {
            animPlaying    = !animPlaying;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }
        if (GUILayout.Button("■", GUILayout.Width(24)))
        {
            animPlaying = false; animTime = 0f; Repaint();
        }
        animLoop = GUILayout.Toggle(animLoop, "🔁", EditorStyles.miniButton, GUILayout.Width(28));

        GUILayout.Space(6);
        EditorGUILayout.LabelField($"{animTime:0.00}s / {(animClip != null ? animClip.length : 0f):0.00}s",
                                   GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // ── 미리보기 + 이벤트 목록 ───────────────────────────────
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(false));

        // 스프라이트 미리보기
        var previewRect = GUILayoutUtility.GetRect(224f, 224f,
                              GUILayout.Width(224f), GUILayout.Height(224f));
        DrawAnimPreview(previewRect, prefab);

        GUILayout.Space(8);

        // 이벤트 목록
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawEventList();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        DrawTimeline();
    }

    // ── 미리보기 영역 ─────────────────────────────────────────────

    private void DrawAnimPreview(Rect rect, GameObject prefab)
    {
        // 어두운 배경
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.12f));

        if (animClip == null) return;

        // 클립 커브에서 직접 스프라이트 읽기, 없으면 정적 스프라이트 폴백
        var  sprite           = GetSpriteAtTime(animClip, animTime);
        bool isStaticFallback = false;
        if (sprite == null)
        {
            sprite            = prefab.GetComponentInChildren<SpriteRenderer>()?.sprite;
            isStaticFallback  = sprite != null;
        }

        Rect drawRect = default;

        if (sprite != null)
        {
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            float size   = Mathf.Min(rect.width, rect.height) * 0.80f;
            float dw     = aspect >= 1f ? size : size * aspect;
            float dh     = aspect >= 1f ? size / aspect : size;
            drawRect = new Rect(
                rect.x + (rect.width  - dw) * 0.5f,
                rect.y + (rect.height - dh) * 0.5f,
                dw, dh);

            var tr = new Rect(
                sprite.rect.x      / sprite.texture.width,
                sprite.rect.y      / sprite.texture.height,
                sprite.rect.width  / sprite.texture.width,
                sprite.rect.height / sprite.texture.height);

            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, tr, true);

            // VFX 오버레이 (onAttack)
            if (vfxFlashTimer > 0f)
                DrawVfxOverlay(prefab, sprite, drawRect);
        }

        // 이벤트 발화 테두리 (빨간 테두리 플래시)
        if (vfxFlashTimer > 0f)
        {
            float a = vfxFlashTimer / VFX_FLASH_DUR;
            var   c = new Color(1f, 0.25f, 0.25f, a);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y,             rect.width, 2f), c);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f,     rect.width, 2f), c);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y,             2f, rect.height), c);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y,     2f, rect.height), c);
        }

        // 재생 중 표시
        if (animPlaying)
        {
            EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.y + 4f, 6f, 6f),
                               new Color(0.2f, 1f, 0.3f, 0.85f));
        }

        // 진단: 스프라이트 소스 표시
        string srcLabel = isStaticFallback
            ? "⚠ 키프레임 없음 (정적 스프라이트)"
            : (sprite != null ? "" : "스프라이트 없음");
        if (!string.IsNullOrEmpty(srcLabel))
            EditorGUI.DropShadowLabel(new Rect(rect.x + 2f, rect.yMax - 16f, rect.width - 4f, 14f),
                                      srcLabel);
    }

    private void DrawVfxOverlay(GameObject prefab, Sprite baseSp, Rect drawRect)
    {
        var fx = prefab?.GetComponent<EnemyFXConfig>();
        if (fx?.onAttack == null) return;

        float alpha = vfxFlashTimer / VFX_FLASH_DUR;

        // VFX 스프라이트가 있으면 그리기, 없으면 노란 원
        var vfxSr     = fx.onAttack.vfxPrefab?.GetComponent<SpriteRenderer>();
        var vfxSprite = vfxSr?.sprite;

        float ppu    = Mathf.Max(1f, baseSp.pixelsPerUnit);
        float scaleX = drawRect.width  / (baseSp.rect.width  / ppu);
        float scaleY = drawRect.height / (baseSp.rect.height / ppu);
        float oX     =  fx.onAttack.spawnOffset.x * scaleX;
        float oY     = -fx.onAttack.spawnOffset.y * scaleY;   // GUI y 반전

        if (vfxSprite != null)
        {
            float s   = Mathf.Min(drawRect.width, drawRect.height) * 0.55f;
            var vRect = new Rect(drawRect.center.x + oX - s * 0.5f,
                                 drawRect.center.y + oY - s * 0.5f, s, s);
            var vTr   = new Rect(
                vfxSprite.rect.x      / vfxSprite.texture.width,
                vfxSprite.rect.y      / vfxSprite.texture.height,
                vfxSprite.rect.width  / vfxSprite.texture.width,
                vfxSprite.rect.height / vfxSprite.texture.height);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(vRect, vfxSprite.texture, vTr, true);
            GUI.color = Color.white;
        }
        else
        {
            // VFX 프리팹 없으면 노란 원형 표시
            float r = 14f;
            EditorGUI.DrawRect(
                new Rect(drawRect.center.x + oX - r, drawRect.center.y + oY - r, r * 2f, r * 2f),
                new Color(1f, 0.9f, 0.1f, alpha * 0.75f));
        }
    }

    // ── 이벤트 목록 ──────────────────────────────────────────────

    private void DrawEventList()
    {
        if (animClip == null) return;

        EditorGUILayout.LabelField("애니메이션 이벤트", EditorStyles.boldLabel);

        var events = AnimationUtility.GetAnimationEvents(animClip);
        bool dirty = false;

        for (int i = 0; i < events.Length; i++)
        {
            bool sel     = selEventIdx == i;
            var  rowRect = EditorGUILayout.BeginHorizontal();
            if (sel) EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.6f, 1f, 0.18f));

            // 선택 / 플레이헤드 이동
            if (GUILayout.Button(sel ? "▶" : "  ", GUILayout.Width(22)))
            {
                selEventIdx = sel ? -1 : i;
                if (!sel) { animTime = events[i].time; animPlaying = false; }
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField($"{events[i].time:0.00}s", GUILayout.Width(44));
            events[i].functionName = EditorGUILayout.TextField(events[i].functionName,
                                         GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck()) dirty = true;

            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                var list = events.ToList();
                list.RemoveAt(i);
                AnimationUtility.SetAnimationEvents(animClip, list.ToArray());
                EditorUtility.SetDirty(animClip);
                if (selEventIdx >= i) selEventIdx--;
                EditorGUILayout.EndHorizontal();
                Repaint();
                return;
            }

            EditorGUILayout.EndHorizontal();

            // 선택된 이벤트 세부 편집
            if (sel)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                events[i].time           = EditorGUILayout.FloatField("시간 (s)",     events[i].time);
                events[i].stringParameter = EditorGUILayout.TextField("String Param", events[i].stringParameter);
                events[i].floatParameter  = EditorGUILayout.FloatField("Float Param", events[i].floatParameter);
                if (EditorGUI.EndChangeCheck()) dirty = true;
                EditorGUI.indentLevel--;
            }
        }

        if (dirty)
        {
            // time 변경 시 정렬 유지
            System.Array.Sort(events, (a, b) => a.time.CompareTo(b.time));
            AnimationUtility.SetAnimationEvents(animClip, events);
            EditorUtility.SetDirty(animClip);
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button($"+ 이벤트 추가  ({animTime:0.00}s)", GUILayout.Height(22)))
        {
            var ev = new AnimationEvent { time = animTime, functionName = "OnAttackEvent" };
            var list = events.ToList();
            list.Add(ev);
            list.Sort((a, b) => a.time.CompareTo(b.time));
            AnimationUtility.SetAnimationEvents(animClip, list.ToArray());
            EditorUtility.SetDirty(animClip);
        }
    }

    // ── 타임라인 바 ──────────────────────────────────────────────

    private void DrawTimeline()
    {
        if (animClip == null || animClip.length <= 0f) return;

        EditorGUILayout.LabelField("타임라인  (드래그로 이동)", EditorStyles.miniLabel);

        var barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                          GUILayout.ExpandWidth(true), GUILayout.Height(40));

        EditorGUI.DrawRect(barRect, new Color(0.13f, 0.13f, 0.13f));

        float len    = animClip.length;
        float padX   = 6f;
        float barX   = barRect.x + padX;
        float barW   = barRect.width - padX * 2f;
        float trackY = barRect.y + 16f;
        float trackH = 10f;

        // 트랙
        var trackRect = new Rect(barX, trackY, barW, trackH);
        EditorGUI.DrawRect(trackRect, new Color(0.22f, 0.22f, 0.22f));

        // 진행 채우기
        EditorGUI.DrawRect(new Rect(barX, trackY, (animTime / len) * barW, trackH),
                           new Color(0.3f, 0.6f, 1f, 0.45f));

        // 눈금
        for (int i = 0; i <= 4; i++)
        {
            float t  = i / 4f;
            float x  = barX + t * barW;
            EditorGUI.DrawRect(new Rect(x, trackY + trackH, 1f, 4f), new Color(0.5f, 0.5f, 0.5f));
            EditorGUI.LabelField(new Rect(x - 14f, trackY + trackH + 3f, 28f, 12f),
                                 $"{t * len:0.0}", EditorStyles.miniLabel);
        }

        // 이벤트 마커
        var events = AnimationUtility.GetAnimationEvents(animClip);
        foreach (var ev in events)
        {
            float ex = barX + (ev.time / len) * barW;
            EditorGUI.DrawRect(new Rect(ex - 1f, trackY - 8f, 2f, trackH + 10f),
                               new Color(1f, 0.4f, 0.15f, 0.9f));
            // 마커 머리
            EditorGUI.DrawRect(new Rect(ex - 4f, trackY - 8f, 8f, 4f),
                               new Color(1f, 0.4f, 0.15f, 0.9f));
            EditorGUI.LabelField(new Rect(ex + 2f, barRect.y, 70f, 14f),
                                 ev.functionName, EditorStyles.whiteMiniLabel);
        }

        // 플레이헤드
        float px = barX + (animTime / len) * barW;
        EditorGUI.DrawRect(new Rect(px - 1f, trackY - 4f, 2f, trackH + 6f), Color.white);
        EditorGUI.DrawRect(new Rect(px - 5f, barRect.yMax - 7f, 10f, 7f), Color.white);

        // 드래그로 이동
        var ev2 = Event.current;
        if ((ev2.type == EventType.MouseDown || ev2.type == EventType.MouseDrag) &&
            barRect.Contains(ev2.mousePosition))
        {
            float t  = Mathf.Clamp01((ev2.mousePosition.x - barX) / barW);
            animTime    = t * len;
            animPlaying = false;
            ev2.Use();
            Repaint();
        }
    }

    // ── 클립 추출 ─────────────────────────────────────────────────

    private void RefreshAnimClips(GameObject prefab)
    {
        lastAnimPrefab = prefab;
        animClip       = null;
        animClips      = null;
        animClipNames  = null;
        animPlaying    = false;
        animTime       = 0f;

        if (prefab == null) return;
        var animator = prefab.GetComponent<Animator>();
        if (animator == null) return;

        var ctrl = animator.runtimeAnimatorController;
        if (ctrl == null) return;

        // AnimatorOverrideController → 오버라이드된 실제 클립 우선 사용
        if (ctrl is AnimatorOverrideController aoc)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(overrides);
            var overridden = overrides
                .Where(kv => kv.Value != null)
                .Select(kv => kv.Value)
                .Distinct()
                .ToArray();
            animClips = overridden.Length > 0
                ? overridden
                : aoc.runtimeAnimatorController.animationClips
                    .Where(c => c != null).Distinct().ToArray();
        }
        else
        {
            animClips = ctrl.animationClips.Where(c => c != null).Distinct().ToArray();
        }

        animClipNames = animClips.Select(c => c.name).ToArray();
        animClipIdx   = Mathf.Clamp(animClipIdx, 0, Mathf.Max(0, animClips.Length - 1));
        animClip      = animClips.Length > 0 ? animClips[animClipIdx] : null;
    }

    // ── 클립에서 스프라이트 직접 읽기 ────────────────────────────

    /// <summary>
    /// AnimationClip 의 ObjectReferenceCurve 에서 지정 시각의 스프라이트를 반환합니다.
    /// AnimationMode / 프리뷰 씬이 필요 없으며, 모든 클립 유형에서 동작합니다.
    /// </summary>
    private static Sprite GetSpriteAtTime(AnimationClip clip, float time)
    {
        if (clip == null) return null;

        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            // b.type 비교는 Unity 버전에 따라 불안정 — propertyName만 확인합니다
            if (binding.propertyName != "m_Sprite") continue;

            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0) continue;

            // time 이하의 가장 마지막 키프레임 선택
            int idx = 0;
            for (int i = 1; i < keys.Length; i++)
            {
                if (keys[i].time <= time + 0.0001f) idx = i;
                else break;
            }
            return keys[idx].value as Sprite;
        }
        return null;
    }

    // ── 에디터 업데이트 (재생 tick) ──────────────────────────────

    private void OnEditorUpdate()
    {
        double now   = EditorApplication.timeSinceStartup;
        float  delta = (float)(now - lastUpdateTime);
        lastUpdateTime = now;

        bool needRepaint = false;

        if (animPlaying && animClip != null)
        {
            float prev  = animTime;
            animTime   += delta;

            CheckEventsFire(prev, Mathf.Min(animTime, animClip.length));

            if (animTime >= animClip.length)
            {
                if (animLoop)
                {
                    float excess = animTime - animClip.length;
                    animTime     = excess;
                    CheckEventsFire(0f, animTime);
                }
                else
                {
                    animTime    = animClip.length;
                    animPlaying = false;
                }
            }
            needRepaint = true;
        }

        // Animation 탭: 재생 중이면 Repaint만으로 충분 (GetSpriteAtTime이 직접 읽음)
        if (animPlaying && activeTab == 4) needRepaint = true;

        if (vfxFlashTimer > 0f)
        {
            vfxFlashTimer = Mathf.Max(0f, vfxFlashTimer - delta);
            needRepaint   = true;
        }

        if (needRepaint) Repaint();
    }

    private void CheckEventsFire(float from, float to)
    {
        if (animClip == null) return;
        foreach (var ev in AnimationUtility.GetAnimationEvents(animClip))
            if (ev.time > from && ev.time <= to)
                vfxFlashTimer = VFX_FLASH_DUR;
    }

    // ══════════════════════════════════════════════════════════════
    //  Prefab helpers
    // ══════════════════════════════════════════════════════════════

    private static void ApplyBehaviour(GameObject prefab, int idx)
    {
        System.Type t = idx switch
        {
            1 => typeof(MeleeBehaviour),
            2 => typeof(RangedBehaviour),
            3 => typeof(HordeBehaviour),
            4 => typeof(TankBehaviour),
            5 => typeof(ChargeBehaviour),
            6 => typeof(BossBehaviour),
            7 => typeof(MagicBehaviour),
            8 => typeof(HealerBehaviour),
            _ => null
        };
        if (t == null) return;

        // EditPrefabContentsScope는 디스크에서 읽으므로,
        // 메모리의 미저장 변경사항을 먼저 디스크에 반영합니다.
        AssetDatabase.SaveAssets();

        var path = AssetDatabase.GetAssetPath(prefab);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            var root = scope.prefabContentsRoot;
            foreach (var b in root.GetComponents<EnemyBehaviourBase>())
                Object.DestroyImmediate(b);
            root.AddComponent(t);
            if (t == typeof(BossBehaviour) && root.GetComponent<BossController>() == null)
                root.AddComponent<BossController>();
        }
        AssetDatabase.SaveAssets();
    }

    private static void AddToPrefab<T>(GameObject prefab) where T : Component
    {
        AssetDatabase.SaveAssets();
        var path = AssetDatabase.GetAssetPath(prefab);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            scope.prefabContentsRoot.AddComponent<T>();
        AssetDatabase.SaveAssets();
    }

    // ══════════════════════════════════════════════════════════════
    //  Create helpers
    // ══════════════════════════════════════════════════════════════

    private void CreateRoster()
    {
        EnsureFolder(saveFolder);
        var path = EditorUtility.SaveFilePanelInProject(
            "챕터 로스터 생성", $"Chapter_{rosters.Count + 1}_Roster", "asset",
            "저장 위치 선택", saveFolder);
        if (string.IsNullOrEmpty(path)) return;

        var r = CreateInstance<ChapterRosterSO>();
        r.chapter = rosters.Count + 1; r.chapterName = $"챕터 {r.chapter}";
        AssetDatabase.CreateAsset(r, path);
        AssetDatabase.SaveAssets();
        Refresh();
        selChapter = rosters.Count - 1;
    }

    private void CreatePrefab()
    {
        EnsureFolder(prefabFolder);
        var path = EditorUtility.SaveFilePanelInProject(
            "적 프리팹 생성", "NewEnemy", "prefab", "저장 위치 선택", prefabFolder);
        if (string.IsNullOrEmpty(path)) return;

        var go = new GameObject("NewEnemy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<Animator>();
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        go.AddComponent<EnemyController>();
        go.AddComponent<MeleeBehaviour>();
        go.AddComponent<EnemyFXConfig>();

        var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();

        var roster = Roster;
        if (roster != null)
        {
            Undo.RecordObject(roster, "Add Enemy To Roster");
            roster.normalEnemies.Add(new EnemySpawnEntry { displayName = saved.name, prefab = saved });
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            selIdx  = roster.normalEnemies.Count - 1;
            reoList = null;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        var parts  = folderPath.Split('/');
        var parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(parent, parts[i]);
            parent = next;
        }
    }

    private static string ToAssetPath(string absPath) =>
        string.IsNullOrEmpty(absPath) ? ""
            : "Assets" + absPath.Substring(Application.dataPath.Length).Replace('\\', '/');

    // ══════════════════════════════════════════════════════════════
    //  공통 유틸
    // ══════════════════════════════════════════════════════════════

    private static void Prop(SerializedObject so, string name, string label)
    {
        var p = so.FindProperty(name);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
    }

    private static void DrawDivider()
    {
        var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandHeight(true), GUILayout.Width(1f));
        EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f));
    }
}
