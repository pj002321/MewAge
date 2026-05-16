#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(WeaponPoint), true)]
public class WeaponInfoEditor : Editor
{
    private SerializedProperty _behaviour;

    private SerializedProperty _hammerImpactWorldPos;
    private SerializedProperty _hammerImpactRadius;
    private SerializedProperty _hammerRiseHeight;
    private SerializedProperty _useParabolicShot;
    private SerializedProperty _parabolicSpawnLocalPos;

    private DB_무기[] _dbRows;

    private static readonly FieldInfo BehaviourField =
        typeof(WeaponPoint).GetField("_behaviour",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private void OnEnable()
    {
        _behaviour = serializedObject.FindProperty("_behaviour");

        _dbRows = targets.Select(FindDbRowFor).ToArray();
        SyncBehavioursFromDb();
        RefreshNestedProps();
    }

    private void OnDisable()
    {
        if (_vfxPreview != null) { DestroyImmediate(_vfxPreview); _vfxPreview = null; }
    }

    private static DB_무기 FindDbRowFor(UnityEngine.Object obj)
    {
        var go = ((Component)obj).gameObject;

        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go);
        string assetPath = stage?.assetPath;
        if (string.IsNullOrEmpty(assetPath))
            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
        if (string.IsNullOrEmpty(assetPath))
            assetPath = AssetDatabase.GetAssetPath(go.transform.root.gameObject);
        if (string.IsNullOrEmpty(assetPath)) return null;

        string filename = Path.GetFileNameWithoutExtension(assetPath).ToLower();
        DB_무기 found = null;
        DB_무기.ForEachEntity(row =>
        {
            if (found != null) return;
            if (!string.IsNullOrEmpty(row.f_무기ID) && row.f_무기ID.ToLower() == filename)
                found = row;
        });
        return found;
    }

    // ── SceneGUI ─────────────────────────────────────────────────────

    private GameObject _vfxPreview;

    private void RefreshNestedProps()
    {
        if (_behaviour == null) return;
        _hammerImpactWorldPos   = _behaviour.FindPropertyRelative("hammerImpactWorldPos");
        _hammerImpactRadius     = _behaviour.FindPropertyRelative("hammerImpactRadius");
        _hammerRiseHeight       = _behaviour.FindPropertyRelative("hammerRiseHeight");
        _useParabolicShot       = _behaviour.FindPropertyRelative("useParabolicShot");
        _parabolicSpawnLocalPos = _behaviour.FindPropertyRelative("parabolicSpawnLocalPos");
    }

    private void OnSceneGUI()
    {
        if (targets.Length > 1) return;
        var weapon = (WeaponPoint)target;

        if (_hammerImpactWorldPos != null && _hammerImpactRadius != null)
        {
            Vector2 impact   = _hammerImpactWorldPos.vector2Value;
            Vector3 worldPos = new Vector3(impact.x, impact.y, 0f);
            float   radius   = _hammerImpactRadius.floatValue;
            float   peakH    = _hammerRiseHeight != null ? _hammerRiseHeight.floatValue : 4f;

            Handles.color = new Color(1f, 0.4f, 0f, 0.18f);
            Handles.DrawSolidDisc(worldPos, Vector3.forward, radius);
            Handles.color = new Color(1f, 0.4f, 0f, 1f);
            Handles.DrawWireDisc(worldPos, Vector3.forward, radius);

            Vector3 peakPos = worldPos + Vector3.up * peakH;
            Handles.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Handles.DrawDottedLine(worldPos, peakPos, 4f);
            Handles.DrawWireDisc(peakPos, Vector3.forward, 0.15f);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(worldPos, 0.12f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Hammer Impact World Pos");
                _hammerImpactWorldPos.vector2Value = new Vector2(moved.x, moved.y);
                serializedObject.ApplyModifiedProperties();
            }

            Handles.Label(worldPos + Vector3.up * (radius + 0.15f), "착지");
            Handles.Label(peakPos  + Vector3.up * 0.2f,             "정점");
        }

        if (_useParabolicShot != null && _useParabolicShot.boolValue && _parabolicSpawnLocalPos != null)
        {
            Vector3 spawnWorld = weapon.transform.TransformPoint(_parabolicSpawnLocalPos.vector3Value);
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, spawnWorld, Quaternion.identity, 0.1f, EventType.Repaint);
        }
    }

    // ── 도트 텍스처 ──────────────────────────────────────────────────

    private static readonly Color DbDotColor = new Color(0.35f, 0.65f, 1f,  1f);
    private static readonly Color WpDotColor = new Color(1f,    0.82f, 0.2f, 1f);

    private static Texture2D _dbDotTex;
    private static Texture2D _wpDotTex;

    private static Texture2D DbDot => _dbDotTex != null ? _dbDotTex : (_dbDotTex = MakeDotTex(DbDotColor));
    private static Texture2D WpDot => _wpDotTex != null ? _wpDotTex : (_wpDotTex = MakeDotTex(WpDotColor));

    private static Texture2D MakeDotTex(Color col)
    {
        const int S = 12;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                tex.SetPixel(x, y, new Color(col.r, col.g, col.b, a));
            }
        tex.Apply();
        return tex;
    }

    // ── 헤더 / 도트 행 ───────────────────────────────────────────────

    private static readonly Color DbBg = new Color(0.3f, 0.5f, 1f,  0.35f);
    private static readonly Color WpBg = new Color(1f,   0.8f, 0.2f, 0.35f);

    private static void DrawColoredHeader(string label, Color bg)
    {
        var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 6f);
        EditorGUI.DrawRect(rect, bg);
        rect.xMin += 4f;
        EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
    }

    private const float DotSize = 8f;
    private const float DotPad  = 4f;

    private static void DrawDotLabel(string label, Texture2D dot, string value)
    {
        var rect    = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        var dotRect = new Rect(rect.x, rect.y + (rect.height - DotSize) * 0.5f, DotSize, DotSize);
        GUI.DrawTexture(dotRect, dot);
        rect.xMin += DotSize + DotPad;
        using (new EditorGUI.DisabledScope(true))
            EditorGUI.LabelField(rect, label, value);
    }

    private static void DrawPropWithDot(SerializedProperty prop, Texture2D dot)
    {
        float h     = EditorGUI.GetPropertyHeight(prop, true);
        var rect    = EditorGUILayout.GetControlRect(true, h);
        var dotRect = new Rect(rect.x, rect.y + (EditorGUIUtility.singleLineHeight - DotSize) * 0.5f, DotSize, DotSize);
        GUI.DrawTexture(dotRect, dot);
        rect.xMin += DotSize + DotPad;
        EditorGUI.PropertyField(rect, prop, true);
    }

    // ── 유틸 ─────────────────────────────────────────────────────────

    private static string F(float v) => v == 0f ? "-" : v.ToString("0.##");

    // ── 챕터 파싱 ─────────────────────────────────────────────────────

    private static int ChapterFromId(string weaponId, int fallback)
    {
        if (!string.IsNullOrEmpty(weaponId))
        {
            var p0 = weaponId.ToLower().Split('_')[0];
            if (p0.StartsWith("ch") && int.TryParse(p0.Substring(2), out int n))
                return n;
        }
        return fallback;
    }

    // ── Proj 프리팹 캐시 ──────────────────────────────────────────────

    private static readonly Dictionary<int, (GameObject[] prefabs, string[] names)> ProjCache = new();

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void ClearProjCache() => ProjCache.Clear();

    private static (GameObject[] prefabs, string[] names) GetProjPrefabs(int chapter)
    {
        if (ProjCache.TryGetValue(chapter, out var cached)) return cached;

        string folder = $"Assets/Resources/Weapons/Ch{chapter}/Proj";
        var guids  = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        var prefabs = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
            .Where(g => g != null)
            .OrderBy(g => g.name)
            .ToArray();
        var names = prefabs.Select(g => g.name).Prepend("(선택)").ToArray();
        var result = (prefabs, names);
        ProjCache[chapter] = result;
        return result;
    }

    // ── Projectile Prefab 피커 ────────────────────────────────────────

    private void DrawProjectilePicker(SerializedProperty prop, int chapter)
    {
        var (prefabs, names) = GetProjPrefabs(chapter);

        float h    = EditorGUIUtility.singleLineHeight;
        var   rect = EditorGUILayout.GetControlRect(true, h);

        // 도트
        var dotRect = new Rect(rect.x, rect.y + (h - DotSize) * 0.5f, DotSize, DotSize);
        GUI.DrawTexture(dotRect, WpDot);
        rect.xMin += DotSize + DotPad;

        // 라벨
        float lw      = EditorGUIUtility.labelWidth - DotSize - DotPad;
        var   lblRect = new Rect(rect.x, rect.y, lw, h);
        EditorGUI.LabelField(lblRect, prop.displayName);

        float valueX = rect.x + lw + 2f;
        float valueW = rect.width - lw - 2f;

        if (prefabs.Length > 0)
        {
            // 왼쪽 절반: Proj 폴더 드롭다운
            float dropW = valueW * 0.5f - 2f;
            var   dropRect = new Rect(valueX, rect.y, dropW, h);
            var   objRect  = new Rect(valueX + dropW + 2f, rect.y, valueW - dropW - 2f, h);

            var current = prop.objectReferenceValue as GameObject;
            int curIdx  = current != null ? Array.IndexOf(prefabs, current) + 1 : 0;
            int picked  = EditorGUI.Popup(dropRect, curIdx, names);
            if (picked > 0 && picked - 1 < prefabs.Length)
                prop.objectReferenceValue = prefabs[picked - 1];

            // 오른쪽 절반: ObjectField (드래그 앤 드롭 / 직접 할당)
            prop.objectReferenceValue =
                EditorGUI.ObjectField(objRect, prop.objectReferenceValue, typeof(GameObject), false);
        }
        else
        {
            // Proj 폴더가 비어있으면 ObjectField만
            var objRect = new Rect(valueX, rect.y, valueW, h);
            prop.objectReferenceValue =
                EditorGUI.ObjectField(objRect, prop.objectReferenceValue, typeof(GameObject), false);
        }
    }

    // 여러 DB 행에서 동일 필드 값 추출 — 모두 같으면 그 값, 다르면 "—"
    private string DbStr(Func<DB_무기, string> sel)
    {
        if (_dbRows == null || _dbRows.Length == 0) return "-";
        var vals = _dbRows.Select(r => r != null ? sel(r) : null).ToArray();
        return vals.All(v => v == vals[0]) ? (vals[0] ?? "-") : "—";
    }

    private static Type GetTypeFromDbTag(string dbType)
    {
        if (string.IsNullOrEmpty(dbType)) return null;
        return TypeCache.GetTypesDerivedFrom<WeaponBehaviourBase>()
            .FirstOrDefault(t => !t.IsAbstract &&
                t.Name.IndexOf(dbType, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    // DB 기준으로 모든 target의 _behaviour 자동 동기화
    private void SyncBehavioursFromDb()
    {
        bool changed = false;
        for (int i = 0; i < targets.Length; i++)
        {
            var db = i < _dbRows.Length ? _dbRows[i] : null;
            if (db == null) continue;

            var expectedType = GetTypeFromDbTag(db.f_타입);
            if (expectedType == null) continue;

            var current = BehaviourField?.GetValue(targets[i])?.GetType();
            if (current == expectedType) continue;

            BehaviourField?.SetValue(targets[i], Activator.CreateInstance(expectedType));
            EditorUtility.SetDirty((UnityEngine.Object)targets[i]);
            changed = true;
        }
        if (changed)
        {
            serializedObject.Update();
            AssetDatabase.SaveAssets();
        }
    }

    // 각 target의 _behaviour 타입 (reflection)
    private Type[] GetAllBehaviourTypes() =>
        targets.Select(t => BehaviourField?.GetValue(t)?.GetType()).ToArray();

    // ── Inspector ────────────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── DB 자동 주입 섹션 ────────────────────────────────────────
        DrawColoredHeader("DB_무기 자동 주입 (Init)  —  인스펙터 설정 불필요", DbBg);
        EditorGUI.indentLevel++;

        bool anyDbMissing = _dbRows.Any(r => r == null);
        if (anyDbMissing)
            EditorGUILayout.HelpBox("일부 무기의 DB 엔트리를 찾지 못했습니다.", MessageType.Warning);

        DrawDotLabel("무기 타입", DbDot, DbStr(r => r.f_타입));
        DrawDotLabel("공격력",   DbDot, DbStr(r => F(r.f_기본공격력)));
        DrawDotLabel("탐지범위", DbDot, DbStr(r => F(r.f_탐지범위)));
        DrawDotLabel("쿨타임",   DbDot, DbStr(r => $"{r.f_공격속도:0.##}s"));
        DrawDotLabel("주변범위", DbDot, DbStr(r => F(r.f_주변범위)));
        DrawDotLabel("넉백",     DbDot, DbStr(r => F(r.f_넉백)));
        DrawDotLabel("치명타",   DbDot, DbStr(r => $"{r.f_치명타:0.#}%"));
        DrawDotLabel("출혈",     DbDot, DbStr(r => F(r.f_출혈)));
        DrawDotLabel("흡혈",     DbDot, DbStr(r => F(r.f_흡혈)));
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(6);

        // ── 인스펙터 설정값 섹션 ─────────────────────────────────────
        DrawColoredHeader("인스펙터 설정값", WpBg);
        DrawBehaviourSelector();
        EditorGUILayout.Space(4);

        var prop = serializedObject.GetIterator();
        prop.NextVisible(true);
        while (prop.NextVisible(false))
        {
            if (prop.name == "_behaviour") continue;
            DrawPropWithDot(prop.Copy(), WpDot);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBehaviourSelector()
    {
        var allBehTypes = GetAllBehaviourTypes();
        bool sameType   = allBehTypes.All(t => t == allBehTypes[0]);
        Type currentType = sameType ? allBehTypes[0] : null;

        // Behaviour 타입 — DB가 결정, 읽기 전용
        var row     = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
        var dotRect = new Rect(row.x, row.y + (row.height - DotSize) * 0.5f, DotSize, DotSize);
        GUI.DrawTexture(dotRect, DbDot);
        row.xMin += DotSize + DotPad;
        using (new EditorGUI.DisabledScope(true))
            EditorGUI.LabelField(row, "Behaviour", currentType?.Name ?? "—");

        if (currentType == null || _behaviour.managedReferenceValue == null) return;

        // 서브필드 — 타입이 모두 동일할 때만 표시
        if (!sameType) return;

        int chapter = ChapterFromId(_dbRows.FirstOrDefault()?.f_무기ID,
                                    _dbRows.FirstOrDefault()?.f_챕터 ?? 1);

        EditorGUI.indentLevel++;
        var it  = _behaviour.Copy();
        var end = _behaviour.GetEndProperty();
        if (it.NextVisible(true))
        {
            while (!SerializedProperty.EqualContents(it, end))
            {
                if (it.name == "projectilePrefab")
                    DrawProjectilePicker(it.Copy(), chapter);
                else
                    DrawPropWithDot(it.Copy(), WpDot);

                if (!it.NextVisible(false)) break;
            }
        }
        EditorGUI.indentLevel--;
    }
}
#endif
