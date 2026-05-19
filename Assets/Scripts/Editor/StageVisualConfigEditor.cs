using System.Collections.Generic;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageVisualConfig))]
public class StageVisualConfigEditor : Editor
{
    struct StageInfo { public string chapterID; public string displayName; }

    static List<StageInfo> _stages;

    static void EnsureStages()
    {
        if (_stages != null) return;
        RebuildStages();
    }

    static void RebuildStages()
    {
        var raw = new List<(int c, int s, string id, string name)>();
        DB_챕터.ForEachEntity(row =>
        {
            string id    = row.f_챕터ID;
            string[] p   = id.Split('_');
            if (p.Length < 2) return;
            if (!int.TryParse(p[0].Substring(1), out int c) ||
                !int.TryParse(p[1].Substring(1), out int s)) return;
            raw.Add((c, s, id, row.f_이름));
        });
        raw.Sort((a, b) => a.c != b.c ? a.c.CompareTo(b.c) : a.s.CompareTo(b.s));

        _stages = new List<StageInfo>();
        foreach (var r in raw)
            _stages.Add(new StageInfo { chapterID = r.id, displayName = $"{r.id}  {r.name}" });
    }

    public override void OnInspectorGUI()
    {
        EnsureStages();

        var config = (StageVisualConfig)target;
        serializedObject.Update();

        // ── DB 동기화 버튼 ─────────────────────────────────────────
        if (GUILayout.Button("DB 기준으로 항목 수 맞추기"))
        {
            RebuildStages();
            int need = _stages?.Count ?? 0;
            int have = config.entries?.Length ?? 0;
            if (need != have)
            {
                Undo.RecordObject(config, "Resize StageVisualConfig entries");
                var next = new StageVisualConfig.Entry[need];
                for (int i = 0; i < need; i++)
                    next[i] = (config.entries != null && i < have)
                        ? config.entries[i]
                        : new StageVisualConfig.Entry();
                config.entries = next;
                EditorUtility.SetDirty(config);
            }
        }

        EditorGUILayout.Space(4);

        // ── 항목별 드로우 ──────────────────────────────────────────
        var entriesProp = serializedObject.FindProperty("entries");
        int count = entriesProp.arraySize;

        for (int i = 0; i < count; i++)
        {
            string label = (_stages != null && i < _stages.Count)
                ? _stages[i].displayName
                : $"Entry {i}";

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            var entryProp = entriesProp.GetArrayElementAtIndex(i);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("icon"),         new GUIContent("Icon"));
            EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("symbolPrefab"), new GUIContent("Symbol Prefab"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
