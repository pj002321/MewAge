using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 기물 역할 태그 시너지 정의 및 인게임 적용.
///
/// 시너지 조건: 동일 역할 태그의 기물 3개 이상 프리셋 장착
///
/// 태그별 시너지 효과:
///   CC    × 3 → 플레이어 공격 시 15% 확률로 보스 그로기 적용
///   버스트 × 3 → 폭발·버스트 기물 피해 +25%
///   지속   × 3 → 초당 체력 회복 +5
///   버프   × 3 → 모든 버프 효과 +20%
///   둔화   × 3 → 둔화 기물 효과 +30%
/// </summary>
public static class WareSynergySystem
{
    // ── 설명 텍스트 ───────────────────────────────────────────────────

    public static string GetSynergyDescription(string tag, int count) => tag switch
    {
        "CC"   => $"[CC {count}개] 공격 시 15% 확률 보스 그로기",
        "버스트" => $"[버스트 {count}개] 버스트 기물 피해 +25%",
        "지속"  => $"[지속 {count}개] 초당 체력 회복 +5",
        "버프"  => $"[버프 {count}개] 버프 효과 +20%",
        "둔화"  => $"[둔화 {count}개] 둔화 효과 +30%",
        _      => $"[{tag} {count}개] 시너지 발동",
    };

    // ── 인게임 적용 ───────────────────────────────────────────────────

    /// <summary>
    /// 게임 씬 시작 시 1회 호출합니다.
    /// 프리셋 태그 시너지 + 시대 시너지를 PlayerStatManager에 적용합니다.
    /// </summary>
    public static void ApplySynergies()
    {
        var mgr = StructureWareManager.Shared;
        if (mgr == null) return;

        StructureSynergyFlags.Reset();
        ApplyTagSynergies(mgr);
        ApplyEraSynergy(mgr);
    }

    static void ApplyTagSynergies(StructureWareManager mgr)
    {
        var tagCnt = mgr.BuildPresetTagCount();

        foreach (var kv in tagCnt)
        {
            if (kv.Value < 3) continue;
            switch (kv.Key)
            {
                case "CC":
                    StructureSynergyFlags.CCSynergyActive = true;
                    break;
                case "버스트":
                    PlayerStatManager.Shared?.AddPctMod(StatType.ATK, 25f);
                    break;
                case "지속":
                    PlayerStatManager.Shared?.AddPctMod(StatType.HPRegen, 5f);
                    break;
                case "버프":
                    PlayerStatManager.Shared?.AddPctMod(StatType.ATK, 20f);
                    break;
                case "둔화":
                    // 기물 스킬 내부에서 StructureSynergyFlags.SlowSynergyBonus 참조
                    StructureSynergyFlags.SlowSynergyBonus = 0.30f;
                    break;
            }
        }
    }

    static void ApplyEraSynergy(StructureWareManager mgr)
    {
        var eraCnt = new Dictionary<string, int>();
        for (int i = 0; i < 4; i++)
        {
            string id = mgr.GetPreset(i);
            if (string.IsNullOrEmpty(id)) continue;
            string era = StructureWareManager.EraOf(id);
            eraCnt[era] = eraCnt.TryGetValue(era, out int c) ? c + 1 : 1;
        }

        int variety = eraCnt.Count;
        bool appliedSingleEra = false;
        foreach (var kv in eraCnt)
        {
            if (kv.Value >= 4)
            {
                PlayerStatManager.Shared?.AddPctMod(StatType.StructureCooldown, -30f);
                PlayerStatManager.Shared?.AddPctMod(StatType.ATK, 20f);
                return;
            }
            if (kv.Value >= 2 && !appliedSingleEra)
            {
                PlayerStatManager.Shared?.AddPctMod(StatType.StructureCooldown, -15f);
                appliedSingleEra = true;
            }
        }

        if (variety >= 4)      PlayerStatManager.Shared?.AddPctMod(StatType.ATK, 50f);
        else if (variety >= 3) PlayerStatManager.Shared?.AddPctMod(StatType.StructureCooldown, -20f);
        // variety >= 2: 체력 회복 → 게임 씬 내 PlayerController.Heal 필요, 현재는 HPRegen 보정
        else if (variety >= 2) PlayerStatManager.Shared?.AddPctMod(StatType.HPRegen, 3f);
    }
}
