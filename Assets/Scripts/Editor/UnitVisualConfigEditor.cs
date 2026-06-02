using System.Collections.Generic;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitVisualConfig))]
public class UnitVisualConfigEditor : Editor
{
    // chapter → [(unitId, displayName)]
    static Dictionary<int, List<(string id, string name)>> _dbCache;
    static bool _cacheBuilt;

    static void EnsureCache()
    {
        if (_cacheBuilt) return;
        _dbCache = new Dictionary<int, List<(string, string)>>();
        DB_유닛.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_유닛ID)) return;
            var id  = row.f_유닛ID;
            int sep = id.IndexOf('_');
            if (sep < 2 || id[0] != 'C') return;
            if (!int.TryParse(id.Substring(1, sep - 1), out int ch)) return;

            if (!_dbCache.TryGetValue(ch, out var list))
                _dbCache[ch] = list = new List<(string, string)>();

            string label = !string.IsNullOrEmpty(row.f_이름) ? row.f_이름 : id;
            list.Add((id, label));
        });
        _cacheBuilt = true;
    }

    public override void OnInspectorGUI()
    {
        EnsureCache();

        var cfg = (UnitVisualConfig)target;
        serializedObject.Update();

        // DB 구조에 맞게 자동 동기화
        SyncWithDB(cfg);
        serializedObject.Update();

        var chaptersArr = serializedObject.FindProperty("chapters");

        foreach (var kv in _dbCache)
        {
            int ch    = kv.Key;
            var units = kv.Value;

            // 해당 챕터 인덱스 찾기
            int ci = FindChapterIndex(chaptersArr, ch);
            if (ci < 0) continue;

            var chProp    = chaptersArr.GetArrayElementAtIndex(ci);
            var iconsProp = chProp.FindPropertyRelative("unitIcons");

            chProp.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
                chProp.isExpanded, $"챕터 {ch}  ({units.Count}개)");

            if (chProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < units.Count; i++)
                {
                    var (_, displayName) = units[i];
                    EditorGUILayout.PropertyField(
                        iconsProp.GetArrayElementAtIndex(i),
                        new GUIContent(displayName));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        if (GUILayout.Button("DB 재캐시"))
        {
            _cacheBuilt = false;
            EnsureCache();
        }

        serializedObject.ApplyModifiedProperties();
    }

    static void SyncWithDB(UnitVisualConfig cfg)
    {
        bool dirty = false;
        var  list  = new List<UnitVisualConfig.ChapterEntry>(cfg.chapters ?? System.Array.Empty<UnitVisualConfig.ChapterEntry>());

        foreach (var kv in _dbCache)
        {
            int ch       = kv.Key;
            int unitCount = kv.Value.Count;

            int idx = list.FindIndex(e => e.chapter == ch);
            if (idx < 0)
            {
                list.Add(new UnitVisualConfig.ChapterEntry
                {
                    chapter   = ch,
                    unitIcons = new Sprite[unitCount],
                });
                dirty = true;
            }
            else if ((list[idx].unitIcons?.Length ?? 0) != unitCount)
            {
                var entry    = list[idx];
                var old      = entry.unitIcons ?? System.Array.Empty<Sprite>();
                var resized  = new Sprite[unitCount];
                System.Array.Copy(old, resized, Mathf.Min(old.Length, unitCount));
                entry.unitIcons = resized;
                list[idx]       = entry;
                dirty           = true;
            }
        }

        // 챕터 번호 순 정렬
        list.Sort((a, b) => a.chapter.CompareTo(b.chapter));

        if (dirty || list.Count != cfg.chapters?.Length)
        {
            cfg.chapters = list.ToArray();
            EditorUtility.SetDirty(cfg);
        }
    }

    static int FindChapterIndex(SerializedProperty arr, int chapter)
    {
        for (int i = 0; i < arr.arraySize; i++)
        {
            var p = arr.GetArrayElementAtIndex(i);
            if (p.FindPropertyRelative("chapter").intValue == chapter) return i;
        }
        return -1;
    }
}
