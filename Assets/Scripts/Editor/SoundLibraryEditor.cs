#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SoundLibrarySO Inspector 상단에 [DB 동기화] 버튼을 추가합니다.
/// 버튼을 누르면:
///   - DB_무기에서 무기 목록을 읽어 WeaponType별로 분류
///   - 기존에 클립이 할당된 엔트리는 덮어쓰지 않음
/// </summary>
[CustomEditor(typeof(SoundLibrarySO))]
public class SoundLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("▶  DB 동기화 (무기 타입별 목록 갱신)", GUILayout.Height(32)))
            Sync((SoundLibrarySO)target);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(4);

        DrawDefaultInspector();
    }

    private static void Sync(SoundLibrarySO lib)
    {
        Undo.RecordObject(lib, "SoundLibrary DB Sync");

        SyncWeaponTypePresets(lib);

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log("[SoundLibrary] DB 동기화 완료");
    }

    // ── 무기 타입별 동기화 ───────────────────────────────────────────────

    private static void SyncWeaponTypePresets(SoundLibrarySO lib)
    {
        // 기존 엔트리 보존용 맵: weaponId → WeaponSoundEntry
        var existing = new Dictionary<string, SoundLibrarySO.WeaponSoundEntry>();
        foreach (var preset in lib.weaponTypePresets)
            foreach (var w in preset.weapons)
                existing[w.weaponId] = w;

        // DB_무기에서 타입별로 분류
        var byType = new Dictionary<WeaponType, List<(string id, string name)>>();

        DB_무기.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_이름)) return;
            var type = ParseWeaponType(row.f_타입);
            if (!byType.ContainsKey(type)) byType[type] = new();
            byType[type].Add((row.f_무기ID, row.f_이름));
        });

        // WeaponType enum 순서대로 프리셋 구성
        var orderedTypes = new[]
        {
            WeaponType.Melee,
            WeaponType.Ranged,
            WeaponType.Magic,
            WeaponType.Boomerang,
            WeaponType.Spin,
            WeaponType.Slash,
            WeaponType.Charge,
            WeaponType.Hammer,
        };

        var nextPresets = new List<SoundLibrarySO.WeaponTypeSoundPreset>();

        foreach (var type in orderedTypes)
        {
            if (!byType.TryGetValue(type, out var weapons) || weapons.Count == 0)
                continue;

            var preset = GetOrCreateTypePreset(lib, type);

            var next = new List<SoundLibrarySO.WeaponSoundEntry>();
            foreach (var (id, name) in weapons.OrderBy(x => x.id))
            {
                if (existing.TryGetValue(id, out var e))
                {
                    e.weaponName = name; // 이름만 갱신, 클립 보존
                    next.Add(e);
                }
                else
                {
                    next.Add(new SoundLibrarySO.WeaponSoundEntry
                    {
                        weaponId   = id,
                        weaponName = name,
                    });
                }
            }
            preset.weapons = next;
            nextPresets.Add(preset);
        }

        lib.weaponTypePresets = nextPresets;
    }

    private static SoundLibrarySO.WeaponTypeSoundPreset GetOrCreateTypePreset(
        SoundLibrarySO lib, WeaponType type)
    {
        foreach (var p in lib.weaponTypePresets)
            if (p.weaponType == type) return p;

        return new SoundLibrarySO.WeaponTypeSoundPreset { weaponType = type };
    }

    // ── 무기 타입 파싱 (WeaponData.ParseWeaponType 와 동일 매핑) ─────────

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
