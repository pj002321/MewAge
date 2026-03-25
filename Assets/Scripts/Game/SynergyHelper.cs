using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// DB_시너지 조회 유틸리티.
/// 챕터별 시너지 태그·아이콘 조회, 장착 개수에 따른 등급 색상 제공.
/// </summary>
public static class SynergyHelper
{
    // ── 등급 컬러 ───────────────────────────────────────────────
    public static readonly Color ColorNone   = new Color(0.35f, 0.35f, 0.35f, 0.5f); // 어두운 회색
    public static readonly Color ColorBronze = new Color(0.80f, 0.50f, 0.20f, 1f);   // 동
    public static readonly Color ColorSilver = new Color(0.78f, 0.78f, 0.78f, 1f);   // 은
    public static readonly Color ColorGold   = new Color(1.00f, 0.85f, 0.10f, 1f);   // 금

    /// <summary>장착 수 → 시너지 등급 색상. 1이상=동, 3이상=은, 5이상=금</summary>
    public static Color GetTierColor(int count)
    {
        if (count >= 5) return ColorGold;
        if (count >= 3) return ColorSilver;
        if (count >= 1) return ColorBronze;
        return ColorNone;
    }

    // ── 챕터 시너지 목록 ─────────────────────────────────────────
    public struct SynergyEntry
    {
        public WeaponSynergyTag Tag;
        public Sprite           Icon;
        public string           Description;
    }

    /// <summary>해당 챕터의 시너지 목록(최대 4개)을 반환합니다.</summary>
    public static List<SynergyEntry> GetChapterSynergies(int chapter)
    {
        var result = new List<SynergyEntry>(4);

        var row = DB_시너지.FindEntity(r => r.f_챕터 == chapter);
        if (row == null) return result;

        AddIfValid(result, row.f_시너지1, row.f_시너지1_아이콘, row.f_설명_1);
        AddIfValid(result, row.f_시너지2, row.f_시너지2_아이콘, row.f_설명_2);
        AddIfValid(result, row.f_시너지3, row.f_시너지3_아이콘, row.f_설명_3);
       // AddIfValid(result, row.f_시너지4, row.f_시너지4_아이콘, row.f_설명_4);

        return result;
    }

    /// <summary>특정 태그의 아이콘을 챕터 DB에서 찾아 반환합니다.</summary>
    public static Sprite GetIcon(WeaponSynergyTag tag, int chapter)
    {
        if (tag == WeaponSynergyTag.None) return null;

        var row = DB_시너지.FindEntity(r => r.f_챕터 == chapter);
        if (row == null) return null;

        if (row.f_시너지1 == tag) return row.f_시너지1_아이콘;
        if (row.f_시너지2 == tag) return row.f_시너지2_아이콘;
        if (row.f_시너지3 == tag) return row.f_시너지3_아이콘;
        if (row.f_시너지4 == tag) return row.f_시너지4_아이콘;

        return null;
    }

    /// <summary>장착 무기 중 특정 태그를 보유한 개수를 반환합니다.</summary>
    public static int CountEquippedTag(WeaponSynergyTag tag)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null || tag == WeaponSynergyTag.None) return 0;

        int count = 0;
        foreach (var w in inv.Equipped)
        {
            if (w == null) continue;
            if (w.tag1 == tag || w.tag2 == tag) count++;
        }
        return count;
    }

    // ── 내부 ────────────────────────────────────────────────────
    private static void AddIfValid(List<SynergyEntry> list, WeaponSynergyTag tag, Sprite icon, string desc)
    {
        if (tag != WeaponSynergyTag.None)
            list.Add(new SynergyEntry { Tag = tag, Icon = icon, Description = desc ?? string.Empty });
    }
}
