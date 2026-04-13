using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// DB_시너지 조회 유틸리티.
/// 챕터별 시너지 태그·설명 조회, 장착 개수에 따른 등급 색상 제공.
/// </summary>
public static class SynergyHelper
{
    // ── 등급 컬러 ───────────────────────────────────────────────
    public static readonly Color ColorNone   = new Color(0.35f, 0.35f, 0.35f, 0.5f);
    public static readonly Color ColorBronze = new Color(0.80f, 0.50f, 0.20f, 1f);
    public static readonly Color ColorSilver = new Color(0.78f, 0.78f, 0.78f, 1f);
    public static readonly Color ColorGold   = new Color(1.00f, 0.85f, 0.10f, 1f);

    /// <summary>장착 수 → 시너지 등급 색상. 1이상=동, 3이상=은, 5이상=금</summary>
    public static Color GetTierColor(int count)
    {
        if (count >= 5) return ColorGold;
        if (count >= 3) return ColorSilver;
        if (count >= 1) return ColorBronze;
        return ColorNone;
    }

    // ── ID ↔ Tag 변환 ─────────────────────────────────────────
    /// <summary>DB의 SY 문자열 ID → WeaponSynergyTag 변환</summary>
    public static WeaponSynergyTag IDToTag(string id)
    {
        return id switch
        {
            "SY_FIELD"  => WeaponSynergyTag.야수,
            "SY_GATH"   => WeaponSynergyTag.채집,
            "SY_HUNTER" => WeaponSynergyTag.사냥꾼,
            "SY_WISDOM" => WeaponSynergyTag.지혜,
            _ => WeaponSynergyTag.None
        };
    }

    /// <summary>WeaponSynergyTag → DB 문자열 ID 변환</summary>
    public static string TagToID(WeaponSynergyTag tag)
    {
        return tag switch
        {
            WeaponSynergyTag.야수   => "SY_FIELD",
            WeaponSynergyTag.채집   => "SY_GATH",
            WeaponSynergyTag.사냥꾼 => "SY_HUNTER",
            WeaponSynergyTag.지혜   => "SY_WISDOM",
            _ => string.Empty
        };
    }

    // ── 챕터 시너지 목록 ─────────────────────────────────────────
    public struct SynergyEntry
    {
        public WeaponSynergyTag Tag;
        public Sprite           Icon;
        public string           Name;
        public string           Description;
        public string           Label;      // 효과 레이블 (예: "공격력 증가")
        public string           Unit;       // 단위 (예: "%", "배", "")
        public List<int>        StatValues; // [0]=동, [1]=은, [2]=금 수치
    }

    /// <summary>해당 챕터의 시너지 목록(최대 4개)을 반환합니다.</summary>
    public static List<SynergyEntry> GetChapterSynergies(int chapter)
    {
        var result = new List<SynergyEntry>(4);

        var row = DB_시너지.FindEntity(r => r.f_챕터 == chapter && !string.IsNullOrEmpty(r.f_SY1));
        if (row == null) return result;

        TryAddEntry(result, row.f_SY1, row.f_S1);
        TryAddEntry(result, row.f_SY2, row.f_S2);
        TryAddEntry(result, row.f_SY3, row.f_S3);
        TryAddEntry(result, row.f_SY4, row.f_S4);

        return result;
    }

    /// <summary>Resources/Synergy/{synergyID} 에서 아이콘 스프라이트를 로드합니다.</summary>
    public static Sprite GetIcon(WeaponSynergyTag tag, int chapter = 0)
    {
        string id = TagToID(tag);
        if (string.IsNullOrEmpty(id)) return null;
        return Resources.Load<Sprite>($"Synergy/{id.ToLower()}");
    }

    /// <summary>장착 무기 중 특정 태그를 보유한 유니크 무기 개수를 반환합니다.</summary>
    public static int CountEquippedTag(WeaponSynergyTag tag)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null || tag == WeaponSynergyTag.None) return 0;

        var uniqueNames = new System.Collections.Generic.HashSet<string>();
        foreach (var w in inv.Equipped)
        {
            if (w == null) continue;
            if (w.tag1 == tag)
                uniqueNames.Add(w.weaponName);
        }
        return uniqueNames.Count;
    }

    /// <summary>장착 무기 중 특정 태그를 보유한 무기들의 아이콘 목록을 반환합니다.</summary>
    public static List<Sprite> GetEquippedIconsForTag(WeaponSynergyTag tag)
    {
        var result = new List<Sprite>();
        var inv = PlayerInventory.Shared;
        if (inv == null || tag == WeaponSynergyTag.None) return result;

        foreach (var w in inv.Equipped)
        {
            if (w == null) continue;
            if (w.tag1 == tag)
                result.Add(w.icon);
        }
        return result;
    }

    /// <summary>장착 무기 중 특정 태그를 보유한 WeaponData 목록을 반환합니다.</summary>
    public static List<WeaponData> GetEquippedWeaponsForTag(WeaponSynergyTag tag)
    {
        var result = new List<WeaponData>();
        var inv = PlayerInventory.Shared;
        if (inv == null || tag == WeaponSynergyTag.None) return result;

        foreach (var w in inv.Equipped)
        {
            if (w == null) continue;
            if (w.tag1 == tag)
                result.Add(w);
        }
        return result;
    }

    // ── 내부 ────────────────────────────────────────────────────
    private static void TryAddEntry(List<SynergyEntry> list, string synergyID, List<int> statValues)
    {
        if (string.IsNullOrEmpty(synergyID)) return;
        var tag = IDToTag(synergyID);
        if (tag == WeaponSynergyTag.None) return;

        string name = GetDisplayName(tag);
        string desc = FormatDescription(tag, statValues);
        var icon = Resources.Load<Sprite>($"Synergy/{synergyID.ToLower()}");

        (string label, string unit) = tag switch
        {
            WeaponSynergyTag.야수   => ("공격력 증가",     "%"),
            WeaponSynergyTag.채집   => ("경험치 획득 증가", "배"),
            WeaponSynergyTag.사냥꾼 => ("출혈 피해 증가",  ""),
            WeaponSynergyTag.지혜   => ("치명타 확률 증가", "%"),
            _ => ("스탯 증가", "")
        };

        list.Add(new SynergyEntry
        {
            Tag        = tag,
            Icon       = icon,
            Name       = name,
            Description = desc,
            Label      = label,
            Unit       = unit,
            StatValues = statValues != null ? new List<int>(statValues) : new List<int>()
        });
    }

    private static string GetDisplayName(WeaponSynergyTag tag) => tag switch
    {
        WeaponSynergyTag.야수   => "야수",
        WeaponSynergyTag.채집   => "채집",
        WeaponSynergyTag.사냥꾼 => "사냥꾼",
        WeaponSynergyTag.지혜   => "지혜",
        _ => tag.ToString()
    };

    /// <summary>시너지 태그 + 수치 배열(동/은/금)로 설명 문자열 생성</summary>
    private static string FormatDescription(WeaponSynergyTag tag, List<int> v)
    {
        if (v == null || v.Count < 3) return string.Empty;

        (string label, string unit) = tag switch
        {
            WeaponSynergyTag.야수   => ("공격력 증가",     "%"),
            WeaponSynergyTag.채집   => ("경험치 획득 증가", "배"),
            WeaponSynergyTag.사냥꾼 => ("출혈 피해 증가",  ""),
            WeaponSynergyTag.지혜   => ("치명타 확률 증가", "%"),
            _ => ("스탯 증가", "")
        };

        return $"{label}\n동 +{v[0]}{unit}  /  은 +{v[1]}{unit}  /  금 +{v[2]}{unit}";
    }
}
