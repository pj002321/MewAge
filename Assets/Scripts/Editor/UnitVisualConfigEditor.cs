using System.Collections.Generic;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitVisualConfig))]
public class UnitVisualConfigEditor : Editor
{
    // chapter → [(unitId, displayName)]
    static Dictionary<int, List<(string id, string name)>> _unitCache;
    // bossId → displayName
    static Dictionary<string, string> _bossCache;
    static bool _cacheBuilt;

    static void EnsureCache()
    {
        if (_cacheBuilt) return;

        _unitCache = new Dictionary<int, List<(string, string)>>();
        DB_유닛.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_유닛ID)) return;
            var id  = row.f_유닛ID;
            int sep = id.IndexOf('_');
            if (sep < 2 || id[0] != 'C') return;
            if (!int.TryParse(id.Substring(1, sep - 1), out int ch)) return;

            if (!_unitCache.TryGetValue(ch, out var list))
                _unitCache[ch] = list = new List<(string, string)>();

            string label = !string.IsNullOrEmpty(row.f_이름) ? row.f_이름 : id;
            list.Add((id, label));
        });

        _bossCache = new Dictionary<string, string>();
        DB_보스.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_보스ID)) return;
            string label = !string.IsNullOrEmpty(row.f_보스이름) ? row.f_보스이름 : row.f_보스ID;
            _bossCache[row.f_보스ID] = label;
        });

        _cacheBuilt = true;
    }

    public override void OnInspectorGUI()
    {
        EnsureCache();

        var cfg = (UnitVisualConfig)target;
        serializedObject.Update();

        SyncWithDB(cfg);
        serializedObject.Update();

        EditorGUILayout.LabelField("프레임 스프라이트", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("normalEnemyFrame"), new GUIContent("일반 적 프레임"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bossEnemyFrame"),   new GUIContent("보스 프레임"));
        EditorGUILayout.Space();

        // 유닛 아이콘 (챕터별)
        var chaptersArr = serializedObject.FindProperty("chapters");
        foreach (var kv in _unitCache)
        {
            int ch    = kv.Key;
            var units = kv.Value;

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

        EditorGUILayout.Space();

        // 보스 아이콘 (DB_보스 기반)
        EditorGUILayout.LabelField("보스 아이콘", EditorStyles.boldLabel);
        var bossIconsProp = serializedObject.FindProperty("bossIcons");
        foreach (var kv in _bossCache)
        {
            string bossId    = kv.Key;
            string bossLabel = kv.Value;

            int bi = FindBossIndex(bossIconsProp, bossId);
            if (bi < 0) continue;

            var entryProp = bossIconsProp.GetArrayElementAtIndex(bi);
            EditorGUILayout.PropertyField(
                entryProp.FindPropertyRelative("icon"),
                new GUIContent(bossLabel));
        }

        EditorGUILayout.Space();
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

        // 유닛 아이콘 동기화
        var list = new List<UnitVisualConfig.ChapterEntry>(cfg.chapters ?? System.Array.Empty<UnitVisualConfig.ChapterEntry>());
        foreach (var kv in _unitCache)
        {
            int ch        = kv.Key;
            int unitCount = kv.Value.Count;
            int idx       = list.FindIndex(e => e.chapter == ch);

            if (idx < 0)
            {
                list.Add(new UnitVisualConfig.ChapterEntry { chapter = ch, unitIcons = new Sprite[unitCount] });
                dirty = true;
            }
            else if ((list[idx].unitIcons?.Length ?? 0) != unitCount)
            {
                var entry   = list[idx];
                var old     = entry.unitIcons ?? System.Array.Empty<Sprite>();
                var resized = new Sprite[unitCount];
                System.Array.Copy(old, resized, Mathf.Min(old.Length, unitCount));
                entry.unitIcons = resized;
                list[idx]       = entry;
                dirty           = true;
            }
        }
        list.Sort((a, b) => a.chapter.CompareTo(b.chapter));
        if (dirty || list.Count != cfg.chapters?.Length)
        {
            cfg.chapters = list.ToArray();
            EditorUtility.SetDirty(cfg);
        }

        // 보스 아이콘 동기화
        var bossList = new List<UnitVisualConfig.BossIconEntry>(cfg.bossIcons ?? System.Array.Empty<UnitVisualConfig.BossIconEntry>());
        bool bossDirty = false;
        foreach (var bossId in _bossCache.Keys)
        {
            if (bossList.FindIndex(e => e.bossId == bossId) < 0)
            {
                bossList.Add(new UnitVisualConfig.BossIconEntry { bossId = bossId });
                bossDirty = true;
            }
        }
        if (bossDirty)
        {
            cfg.bossIcons = bossList.ToArray();
            EditorUtility.SetDirty(cfg);
        }
    }

    static int FindChapterIndex(SerializedProperty arr, int chapter)
    {
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("chapter").intValue == chapter) return i;
        return -1;
    }

    static int FindBossIndex(SerializedProperty arr, string bossId)
    {
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("bossId").stringValue == bossId) return i;
        return -1;
    }
}
