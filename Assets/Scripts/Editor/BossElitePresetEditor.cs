using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// MuAge ▸ Boss / Elite Preset Editor
/// BossController·BossSkillBase·EliteMarker 프리팹을 한 창에서 통합 편집합니다.
/// Tools > Boss・Elite Preset Editor
/// </summary>
public class BossElitePresetEditor : EditorWindow
{
    // ── 상태 ─────────────────────────────────────────────────────────
    private enum SourceTab { Boss, Elite }
    private SourceTab _sourceTab = SourceTab.Boss;

    private int     _selIdx = -1;
    private Vector2 _scrollLeft, _scrollRight;

    private readonly List<GameObject> _bossPrefabs  = new();
    private readonly List<GameObject> _elitePrefabs = new();

    // ── 우측 탭 ──────────────────────────────────────────────────────
    private static readonly string[] BossRightTabs  = { "  Controller  ", "  Skills  ", "  Visual  ", "  FX  " };
    private static readonly string[] EliteRightTabs = { "  Stats  ", "  Behaviour  ", "  Visual  ", "  FX  " };
    private int _rightTab = 0;

    // ── 컴포넌트 에디터 캐시 (Odin ShowIf 지원) ──────────────────────
    private readonly Dictionary<Object, Editor> _editorCache = new();

    // ── 완료 체크 (EditorPrefs 저장, key = GUID) ─────────────────────
    private const string DoneKeyPrefix = "BossElitePreset_Done_";
    private readonly HashSet<string> _doneGuids = new();

    // ── 애니메이션 미리보기 ───────────────────────────────────────────
    private AnimationClip   _animClip;
    private AnimationClip[] _animClips;
    private string[]        _animClipNames;
    private int             _animClipIdx  = 0;
    private float           _animTime     = 0f;
    private bool            _animPlaying  = false;
    private bool            _animLoop     = true;
    private double          _lastAnimTime = 0;
    private GameObject      _lastAnimPrefab;

    // ══════════════════════════════════════════════════════════════════
    //  열기 / Enable / Disable
    // ══════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Boss・Elite Preset Editor")]
    public static void Open()
    {
        var w = GetWindow<BossElitePresetEditor>("Boss/Elite 프리셋");
        w.minSize = new Vector2(820, 520);
        w.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        LoadDoneState();
        Refresh();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        _animPlaying = false;
        ClearEditorCache();
    }

    // OnFocus: 목록만 재빌드, 선택은 유지
    private void OnFocus() => RebuildLists();

    // ══════════════════════════════════════════════════════════════════
    //  데이터 헬퍼
    // ══════════════════════════════════════════════════════════════════

    /// <summary>툴바 새로고침 버튼 — 목록 재빌드 + 선택·캐시 초기화.</summary>
    private void Refresh()
    {
        RebuildLists();
        _selIdx = -1;
        ClearEditorCache();
    }

    /// <summary>프리팹 목록만 재빌드. 현재 선택된 프리팹 GUID를 보존해 선택이 유지됨.</summary>
    private void RebuildLists()
    {
        // 현재 선택된 프리팹의 GUID 보존
        string selGuid = null;
        var selPrefab = SelPrefab;
        if (selPrefab != null)
            selGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selPrefab));

        _bossPrefabs.Clear();
        _elitePrefabs.Clear();

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            if (go.GetComponentInChildren<BossController>(true) != null)
                _bossPrefabs.Add(go);
            else if (go.GetComponentInChildren<EliteMarker>(true) != null || IsElitePath(path))
                _elitePrefabs.Add(go);
        }

        _bossPrefabs .Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        _elitePrefabs.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        // 선택 복원
        if (selGuid != null)
        {
            _selIdx = -1;
            var list = CurrentList;
            for (int i = 0; i < list.Count; i++)
            {
                if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(list[i])) == selGuid)
                {
                    _selIdx = i;
                    break;
                }
            }
        }
    }

    // ── 완료 상태 저장/로드 ───────────────────────────────────────────

    private void LoadDoneState()
    {
        _doneGuids.Clear();
        string raw = EditorPrefs.GetString(DoneKeyPrefix + "All", "");
        if (!string.IsNullOrEmpty(raw))
            foreach (var g in raw.Split(','))
                if (!string.IsNullOrEmpty(g)) _doneGuids.Add(g);
    }

    private void SaveDoneState()
    {
        EditorPrefs.SetString(DoneKeyPrefix + "All", string.Join(",", _doneGuids));
    }

    private bool IsDone(GameObject prefab)
    {
        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        return _doneGuids.Contains(guid);
    }

    private void SetDone(GameObject prefab, bool value)
    {
        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        if (value) _doneGuids.Add(guid);
        else       _doneGuids.Remove(guid);
        SaveDoneState();
    }

    private static bool IsElitePath(string assetPath)
    {
        // Assets/…/Elite/… 또는 Assets/…/elite/… 폴더에 있으면 Elite로 인식
        string lower = assetPath.Replace('\\', '/').ToLowerInvariant();
        int idx = lower.IndexOf("/elite/", System.StringComparison.Ordinal);
        return idx >= 0;
    }

    private void ClearEditorCache()
    {
        foreach (var e in _editorCache.Values)
            if (e != null) DestroyImmediate(e);
        _editorCache.Clear();
    }

    private Editor GetOrCreateEditor(Object target)
    {
        if (!_editorCache.TryGetValue(target, out var editor) || editor == null)
        {
            editor = Editor.CreateEditor(target);
            _editorCache[target] = editor;
        }
        return editor;
    }

    private List<GameObject> CurrentList =>
        _sourceTab == SourceTab.Boss ? _bossPrefabs : _elitePrefabs;

    private GameObject SelPrefab
    {
        get
        {
            var list = CurrentList;
            return (_selIdx >= 0 && _selIdx < list.Count) ? list[_selIdx] : null;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  루트 GUI
    // ══════════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
        DrawLeft();
        DrawDivider();
        DrawRight();
        EditorGUILayout.EndHorizontal();
    }

    // ══════════════════════════════════════════════════════════════════
    //  툴바
    // ══════════════════════════════════════════════════════════════════

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(64)))
            Refresh();

        GUILayout.Space(6);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.45f, 1f, 0.55f, 1f);
        if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(56)))
        {
            GUIUtility.keyboardControl = 0; // 포커스 중인 필드 먼저 커밋
            AssetDatabase.SaveAssets();
            Debug.Log("[Boss/Elite Preset] 저장 완료");
        }
        GUI.backgroundColor = prevBg;

        GUILayout.FlexibleSpace();

        int next = GUILayout.Toolbar((int)_sourceTab, new[] { "  Boss  ", "  Elite  " }, EditorStyles.toolbarButton);
        if (next != (int)_sourceTab)
        {
            _sourceTab = (SourceTab)next;
            _selIdx    = -1;
            _rightTab  = 0;
        }

        EditorGUILayout.EndHorizontal();
    }

    // ══════════════════════════════════════════════════════════════════
    //  좌측 목록
    // ══════════════════════════════════════════════════════════════════

    private void DrawLeft()
    {
        var list = CurrentList;
        EditorGUILayout.BeginVertical(GUILayout.Width(200), GUILayout.ExpandHeight(true));

        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleCenter
        };
        int doneCount = list.Count(p => IsDone(p));
        EditorGUILayout.LabelField($"{_sourceTab} 프리팹 ({doneCount}/{list.Count})", headerStyle, GUILayout.Height(22));

        _scrollLeft = EditorGUILayout.BeginScrollView(_scrollLeft, GUILayout.ExpandHeight(true));
        for (int i = 0; i < list.Count; i++)
        {
            bool sel  = (i == _selIdx);
            var  rect = GUILayoutUtility.GetRect(180f, 24f);

            if (sel)
                EditorGUI.DrawRect(rect, new Color(0.25f, 0.5f, 1f, 0.3f));

            // 완료 체크박스 (왼쪽 18px)
            bool done    = IsDone(list[i]);
            var  cbRect  = new Rect(rect.x + 3, rect.y + 4, 16, 16);
            bool newDone = EditorGUI.Toggle(cbRect, done);
            if (newDone != done)
            {
                SetDone(list[i], newDone);
                Repaint();
            }

            // 이름 레이블 (체크박스 오른쪽)
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = sel ? FontStyle.Bold : FontStyle.Normal,
                normal    = { textColor = done ? new Color(0.5f, 0.85f, 0.5f) : EditorStyles.label.normal.textColor }
            };
            GUI.Label(new Rect(rect.x + 22, rect.y, rect.width - 22, rect.height), list[i].name, style);

            // 행 클릭 → 선택 (체크박스 영역 제외)
            var clickRect = new Rect(rect.x + 22, rect.y, rect.width - 22, rect.height);
            if (Event.current.type == EventType.MouseDown && clickRect.Contains(Event.current.mousePosition))
            {
                if (i != _selIdx)
                {
                    // 입력 중인 텍스트 필드 강제 커밋 → 현재 프리팹에 값이 저장됨
                    GUIUtility.keyboardControl = 0;
                    AssetDatabase.SaveAssets();

                    _selIdx   = i;
                    _rightTab = 0;
                    RefreshAnimClips(list[i]);
                    ClearEditorCache();
                }
                Event.current.Use();
                Repaint();
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ══════════════════════════════════════════════════════════════════
    //  우측 패널
    // ══════════════════════════════════════════════════════════════════

    private void DrawRight()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        var prefab = SelPrefab;
        if (prefab == null)
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 왼쪽에서 프리팹을 선택하세요",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 });
                GUILayout.FlexibleSpace();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawRightHeader(prefab);

        var tabs = (_sourceTab == SourceTab.Boss) ? BossRightTabs : EliteRightTabs;
        _rightTab = GUILayout.Toolbar(_rightTab, tabs, GUILayout.Height(22));

        _scrollRight = EditorGUILayout.BeginScrollView(_scrollRight);
        EditorGUILayout.Space(4);

        if (_sourceTab == SourceTab.Boss)
        {
            switch (_rightTab)
            {
                case 0: DrawBossController(prefab); break;
                case 1: DrawBossSkills(prefab);     break;
                case 2: DrawVisual(prefab);          break;
                case 3: DrawFX(prefab);              break;
            }
        }
        else
        {
            switch (_rightTab)
            {
                case 0: DrawEliteStats(prefab);     break;
                case 1: DrawEliteBehaviour(prefab); break;
                case 2: DrawVisual(prefab);          break;
                case 3: DrawFX(prefab);              break;
            }
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static void DrawRightHeader(GameObject prefab)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        var sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr?.sprite != null)
        {
            var tex = AssetPreview.GetAssetPreview(sr.sprite);
            if (tex != null) GUILayout.Label(tex, GUILayout.Width(48), GUILayout.Height(48));
        }

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(prefab.name, EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("프리팹 열기",    GUILayout.Width(88)))  AssetDatabase.OpenAsset(prefab);
        if (GUILayout.Button("Project 찾기",  GUILayout.Width(88)))  EditorGUIUtility.PingObject(prefab);
        if (GUILayout.Button("Inspector 선택", GUILayout.Width(100))) Selection.activeObject = prefab;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Boss — Controller 탭 (BossController 전체 필드)
    // ══════════════════════════════════════════════════════════════════

    private void DrawBossController(GameObject prefab)
    {
        var bc = prefab.GetComponentInChildren<BossController>(true);
        if (bc == null) { EditorGUILayout.HelpBox("BossController 없음", MessageType.Warning); return; }

        GetOrCreateEditor(bc).OnInspectorGUI();
    }

    // ══════════════════════════════════════════════════════════════════
    //  Boss — Skills 탭 (스킬 컴포넌트별 전체 필드)
    // ══════════════════════════════════════════════════════════════════

    private void DrawBossSkills(GameObject prefab)
    {
        var skills = prefab.GetComponentsInChildren<BossSkillBase>(true);
        if (skills.Length == 0)
        {
            EditorGUILayout.HelpBox("BossSkillBase 컴포넌트 없음", MessageType.Info);
            return;
        }

        foreach (var skill in skills)
        {
            if (skill == null) continue;
            EditorGUILayout.Space(4);
            SectionHeader(skill.GetType().Name);
            GetOrCreateEditor(skill).OnInspectorGUI();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Elite — Stats 탭
    // ══════════════════════════════════════════════════════════════════

    private static void DrawEliteStats(GameObject prefab)
    {
        var ec = prefab.GetComponentInChildren<EnemyController>(true);
        if (ec == null) { EditorGUILayout.HelpBox("EnemyController 없음", MessageType.Warning); return; }

        var so = new SerializedObject(ec);
        so.Update();

        SectionHeader("스탯");
        Prop(so, "maxHP",          "최대 HP");
        Prop(so, "contactDamage", "접촉 데미지");
        Prop(so, "moveSpeed",     "이동 속도");

        EditorGUILayout.Space(6);
        SectionHeader("보상");
        Prop(so, "goldDrop",   "골드");
        Prop(so, "expDrop",    "경험치");
        Prop(so, "scoreValue", "점수");

        so.ApplyModifiedProperties();

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("넉백 면역은 EliteMarker.Awake()에서 자동 적용됩니다.", MessageType.None);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Elite — Behaviour 탭
    // ══════════════════════════════════════════════════════════════════

    private void DrawEliteBehaviour(GameObject prefab)
    {
        var behaviours = prefab.GetComponentsInChildren<EnemyBehaviourBase>(true);
        if (behaviours.Length == 0)
        {
            EditorGUILayout.HelpBox("EnemyBehaviourBase 없음", MessageType.Info);
            return;
        }

        foreach (var b in behaviours)
        {
            if (b == null) continue;
            EditorGUILayout.Space(4);
            SectionHeader($"행동: {b.GetType().Name}");
            GetOrCreateEditor(b).OnInspectorGUI();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Visual 탭 (SpriteRenderer + Animator + 애니메이션 미리보기)
    // ══════════════════════════════════════════════════════════════════

    private void DrawVisual(GameObject prefab)
    {
        var sr   = prefab.GetComponentInChildren<SpriteRenderer>();
        var anim = prefab.GetComponentInChildren<Animator>();

        if (sr != null)
        {
            SectionHeader("Sprite Renderer");
            var so = new SerializedObject(sr);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("m_Sprite"),             new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(so.FindProperty("m_Color"),              new GUIContent("Color"));
            EditorGUILayout.PropertyField(so.FindProperty("m_FlipX"),              new GUIContent("Flip X"));
            EditorGUILayout.PropertyField(so.FindProperty("m_FlipY"),              new GUIContent("Flip Y"));
            EditorGUILayout.PropertyField(so.FindProperty("m_DrawMode"),           new GUIContent("Draw Mode"));
            EditorGUILayout.PropertyField(so.FindProperty("m_MaskInteraction"),    new GUIContent("Mask Interaction"));
            EditorGUILayout.PropertyField(so.FindProperty("m_SpriteSortPoint"),    new GUIContent("Sprite Sort Point"));
            EditorGUILayout.PropertyField(so.FindProperty("m_Materials"),          new GUIContent("Material"));

            EditorGUILayout.Space(4);
            SectionHeader("Additional Settings");
            EditorGUILayout.PropertyField(so.FindProperty("m_SortingLayerID"),     new GUIContent("Sorting Layer"));
            EditorGUILayout.PropertyField(so.FindProperty("m_SortingOrder"),       new GUIContent("Order in Layer"));
            EditorGUILayout.PropertyField(so.FindProperty("m_RenderingLayerMask"), new GUIContent("Rendering Layer Mask"));
            so.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(8);

        if (anim != null)
        {
            SectionHeader("Animator");
            var so = new SerializedObject(anim);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("m_Controller"), new GUIContent("Controller"));
            so.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(8);
        DrawAnimationSection(prefab);
    }

    // ── 애니메이션 미리보기 ───────────────────────────────────────────

    private void DrawAnimationSection(GameObject prefab)
    {
        if (_lastAnimPrefab != prefab)
            RefreshAnimClips(prefab);

        if (_animClips == null || _animClips.Length == 0)
        {
            EditorGUILayout.HelpBox("AnimatorController가 없거나 클립이 없습니다.", MessageType.Info);
            return;
        }

        SectionHeader("애니메이션 미리보기");

        // 클립 선택 + 재생 컨트롤
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("클립", GUILayout.Width(28));
        int newIdx = EditorGUILayout.Popup(_animClipIdx, _animClipNames, GUILayout.Width(160));
        if (newIdx != _animClipIdx)
        {
            _animClipIdx = newIdx;
            _animClip    = _animClips[_animClipIdx];
            _animTime    = 0f;
            _animPlaying = false;
        }
        GUILayout.Space(8);
        if (GUILayout.Button(_animPlaying ? "⏸" : "▶", GUILayout.Width(28)))
        {
            _animPlaying   = !_animPlaying;
            _lastAnimTime  = EditorApplication.timeSinceStartup;
        }
        if (GUILayout.Button("■", GUILayout.Width(24)))
        {
            _animPlaying = false; _animTime = 0f; Repaint();
        }
        _animLoop = GUILayout.Toggle(_animLoop, "↻", EditorStyles.miniButton, GUILayout.Width(28));
        EditorGUILayout.LabelField($"{_animTime:0.00}s / {(_animClip?.length ?? 0f):0.00}s",
            GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // 스프라이트 미리보기
        var previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.Width(160f), GUILayout.Height(160f));
        DrawAnimPreview(previewRect, prefab);

        DrawTimeline();
    }

    private void DrawAnimPreview(Rect rect, GameObject prefab)
    {
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.12f));
        if (_animClip == null) return;

        var sprite = GetSpriteAtTime(_animClip, _animTime);
        if (sprite == null)
            sprite = prefab.GetComponentInChildren<SpriteRenderer>()?.sprite;

        if (sprite != null)
        {
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            float size   = Mathf.Min(rect.width, rect.height) * 0.80f;
            float dw = aspect >= 1f ? size : size * aspect;
            float dh = aspect >= 1f ? size / aspect : size;
            var drawRect = new Rect(
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
        }

        if (_animPlaying)
            EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.y + 4f, 6f, 6f),
                new Color(0.2f, 1f, 0.3f, 0.85f));
    }

    private void DrawTimeline()
    {
        if (_animClip == null || _animClip.length <= 0f) return;

        EditorGUILayout.LabelField("타임라인  (드래그로 이동)", EditorStyles.miniLabel);
        var barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.Height(32));

        EditorGUI.DrawRect(barRect, new Color(0.13f, 0.13f, 0.13f));

        float len  = _animClip.length;
        float padX = 6f;
        float barX = barRect.x + padX;
        float barW = barRect.width - padX * 2f;
        float trackY = barRect.y + 10f;
        float trackH = 8f;

        EditorGUI.DrawRect(new Rect(barX, trackY, barW, trackH), new Color(0.22f, 0.22f, 0.22f));
        EditorGUI.DrawRect(new Rect(barX, trackY, (_animTime / len) * barW, trackH),
            new Color(0.3f, 0.6f, 1f, 0.45f));

        float px = barX + (_animTime / len) * barW;
        EditorGUI.DrawRect(new Rect(px - 1f, trackY - 4f, 2f, trackH + 6f), Color.white);

        var ev = Event.current;
        if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) &&
            barRect.Contains(ev.mousePosition))
        {
            _animTime    = Mathf.Clamp01((ev.mousePosition.x - barX) / barW) * len;
            _animPlaying = false;
            ev.Use();
            Repaint();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  FX 탭 (EnemyFXConfig)
    // ══════════════════════════════════════════════════════════════════

    private static void DrawFX(GameObject prefab)
    {
        var fx = prefab.GetComponentInChildren<EnemyFXConfig>(true);
        if (fx == null)
        {
            EditorGUILayout.HelpBox("EnemyFXConfig 없음", MessageType.Info);
            return;
        }

        var so = new SerializedObject(fx);
        so.Update();
        EffectGroup(so, "onAttack", "공격");
        EffectGroup(so, "onHit",   "피격");
        EffectGroup(so, "onDeath", "사망");
        EffectGroup(so, "onSpawn", "스폰");
        so.ApplyModifiedProperties();
    }

    private static void EffectGroup(SerializedObject so, string field, string label)
    {
        EditorGUILayout.Space(4);
        SectionHeader(label);
        var p = so.FindProperty(field);
        if (p == null) return;
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(p.FindPropertyRelative("vfxPrefab"),  new GUIContent("VFX 프리팹"));
        EditorGUILayout.PropertyField(p.FindPropertyRelative("spawnOffset"), new GUIContent("스폰 오프셋"));
        EditorGUILayout.PropertyField(p.FindPropertyRelative("sfxClip"),    new GUIContent("사운드 클립"));
        EditorGUILayout.PropertyField(p.FindPropertyRelative("volume"),     new GUIContent("볼륨"));
        EditorGUI.indentLevel--;
    }

    // ══════════════════════════════════════════════════════════════════
    //  애니메이션 클립 추출
    // ══════════════════════════════════════════════════════════════════

    private void RefreshAnimClips(GameObject prefab)
    {
        _lastAnimPrefab = prefab;
        _animClip       = null;
        _animClips      = null;
        _animClipNames  = null;
        _animPlaying    = false;
        _animTime       = 0f;

        if (prefab == null) return;
        var animator = prefab.GetComponentInChildren<Animator>();
        if (animator == null) return;
        var ctrl = animator.runtimeAnimatorController;
        if (ctrl == null) return;

        if (ctrl is AnimatorOverrideController aoc)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(overrides);
            var overridden = overrides
                .Where(kv => kv.Value != null)
                .Select(kv => kv.Value)
                .Distinct()
                .ToArray();
            _animClips = overridden.Length > 0
                ? overridden
                : aoc.runtimeAnimatorController.animationClips
                    .Where(c => c != null).Distinct().ToArray();
        }
        else
        {
            _animClips = ctrl.animationClips.Where(c => c != null).Distinct().ToArray();
        }

        _animClipNames = _animClips.Select(c => c.name).ToArray();
        _animClipIdx   = Mathf.Clamp(_animClipIdx, 0, Mathf.Max(0, _animClips.Length - 1));
        _animClip      = _animClips.Length > 0 ? _animClips[_animClipIdx] : null;
    }

    private static Sprite GetSpriteAtTime(AnimationClip clip, float time)
    {
        if (clip == null) return null;
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.propertyName != "m_Sprite") continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0) continue;
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

    private void OnEditorUpdate()
    {
        if (!_animPlaying || _animClip == null) return;

        double now   = EditorApplication.timeSinceStartup;
        float  delta = (float)(now - _lastAnimTime);
        _lastAnimTime = now;

        _animTime += delta;
        if (_animTime >= _animClip.length)
        {
            if (_animLoop) _animTime -= _animClip.length;
            else           { _animTime = _animClip.length; _animPlaying = false; }
        }
        Repaint();
    }

    // ══════════════════════════════════════════════════════════════════
    //  공통 유틸
    // ══════════════════════════════════════════════════════════════════

    private static void SectionHeader(string title)
    {
        EditorGUILayout.Space(4);
        var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
        EditorGUI.LabelField(rect, "  " + title,
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            });
        EditorGUILayout.Space(2);
    }

    private static void Prop(SerializedObject so, string fieldName, string label)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
    }

    private static void DrawDivider()
    {
        var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandHeight(true), GUILayout.Width(1f));
        EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f));
    }
}
