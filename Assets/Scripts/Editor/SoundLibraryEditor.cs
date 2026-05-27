#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SoundLibrarySO Inspector 상단에 [DB 동기화] 버튼을 추가합니다.
/// 버튼을 누르면:
///   - DB_무기에서 챕터별 무기 목록을 읽어 WeaponSoundEntry를 추가/정리
///   - Assets/Data/ 의 ChapterRosterSO에서 적 목록을 읽어 EnemySoundEntry를 추가/정리
///   - 기존에 클립이 할당된 엔트리는 덮어쓰지 않음
/// </summary>
[CustomEditor(typeof(SoundLibrarySO))]
public class SoundLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("▶  DB 동기화 (무기 · 적 목록 갱신)", GUILayout.Height(32)))
            Sync((SoundLibrarySO)target);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(4);

        DrawDefaultInspector();
    }

    private static void Sync(SoundLibrarySO lib)
    {
        Undo.RecordObject(lib, "SoundLibrary DB Sync");

        SyncWeapons(lib);

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log("[SoundLibrary] DB 동기화 완료");
    }

    // ── 무기 동기화 ─────────────────────────────────────────────────────

    private static void SyncWeapons(SoundLibrarySO lib)
    {
        // 챕터 → 무기 목록 수집
        var byChapter = new Dictionary<int, List<(string id, string name)>>();

        DB_무기.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_이름)) return;
            int ch = row.f_챕터;
            if (!byChapter.ContainsKey(ch)) byChapter[ch] = new();
            byChapter[ch].Add((row.f_무기ID, row.f_이름));
        });

        foreach (var kv in byChapter.OrderBy(x => x.Key))
        {
            var preset = GetOrCreateChapterPreset(lib, kv.Key);

            // 기존 엔트리 맵
            var existing = new Dictionary<string, SoundLibrarySO.WeaponSoundEntry>();
            foreach (var e in preset.weapons) existing[e.weaponName] = e;

            var next = new List<SoundLibrarySO.WeaponSoundEntry>();
            foreach (var (id, name) in kv.Value.OrderBy(x => x.name))
            {
                if (existing.TryGetValue(name, out var e))
                    next.Add(e);           // 기존 유지 (클립 보존)
                else
                    next.Add(new SoundLibrarySO.WeaponSoundEntry { weaponId = id, weaponName = name });
            }
            preset.weapons = next;
        }
    }

    // ── 유틸 ────────────────────────────────────────────────────────────

    private static SoundLibrarySO.ChapterSoundPreset GetOrCreateChapterPreset(SoundLibrarySO lib, int chapter)
    {
        foreach (var p in lib.chapters)
            if (p.chapter == chapter) return p;

        var preset = new SoundLibrarySO.ChapterSoundPreset
        {
            chapter = chapter,
            label   = $"C{chapter}",
        };
        lib.chapters.Add(preset);
        lib.chapters.Sort((a, b) => a.chapter.CompareTo(b.chapter));
        return preset;
    }
}
#endif
