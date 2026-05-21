using UnityEngine;
using Mewage.DB;

/// <summary>
/// DB_유물 한 행을 런타임에서 사용하는 데이터 클래스.
/// 유물 하나에 주 효과(속성)와 보조 효과(특수속성) 두 가지를 가질 수 있습니다.
/// </summary>
public class RelicData
{
    public string         relicId;
    public string         relicName;
    public string         grade;
    public string         description;

    public RelicEffectType primaryEffect;
    public float           primaryBase;    // f_속성수치 (Lv.1 기준)

    public RelicEffectType secondaryEffect;
    public float           secondaryBase;  // f_특수속성수치 (Lv.1 기준)

    /// <summary>레벨에 따른 주 효과 수치. Lv.1 = base, Lv.10 = base × 2.35</summary>
    public float GetPrimaryValue(int level)
    {
        if (primaryEffect == RelicEffectType.NONE) return 0f;
        return primaryBase * ScaleMult(level);
    }

    /// <summary>레벨에 따른 보조 효과 수치.</summary>
    public float GetSecondaryValue(int level)
    {
        if (secondaryEffect == RelicEffectType.NONE) return 0f;
        return secondaryBase * ScaleMult(level);
    }

    private static float ScaleMult(int level)
    {
        if (level <= 0) return 0f;   // 잠금 상태
        int clamped = Mathf.Clamp(level, 1, RelicInventory.MaxLevel);
        return 1f + (clamped - 1) * 0.15f;
    }

    public static RelicData FromDB(DB_유물 row)
    {
        if (row == null) return null;

        var data = new RelicData
        {
            relicId       = row.f_ID,
            relicName     = row.f_이름,
            grade         = row.f_등급,
            description   = row.f_설명,
            primaryBase   = row.f_속성수치,
            secondaryBase = row.f_특수속성수치,
        };

        data.primaryEffect   = ParseEffect(row.f_속성ID);
        data.secondaryEffect = ParseEffect(row.f_특수속성ID);
        return data;
    }

    private static RelicEffectType ParseEffect(string id)
    {
        if (string.IsNullOrEmpty(id)) return RelicEffectType.NONE;
        // DB 표기와 enum 이름이 다른 경우 정규화
        if (id == "HP_MAX") id = "MAX_HP";
        return System.Enum.TryParse(id, out RelicEffectType result)
            ? result
            : RelicEffectType.NONE;
    }

    /// <summary>RelicEffectType → StatType 변환. 특수/무기 효과는 null 반환.</summary>
    public static StatType? EffectToStatType(RelicEffectType effect) => effect switch
    {
        RelicEffectType.ATK       => StatType.ATK,
        RelicEffectType.DEF       => StatType.DEF,
        RelicEffectType.MAX_HP    => StatType.MaxHP,
        RelicEffectType.MOV_SPD   => StatType.MoveSpeed,
        RelicEffectType.ATK_SPD   => StatType.AtkSpeed,
        RelicEffectType.EVA       => StatType.Evasion,
        RelicEffectType.CRT_RATE  => StatType.CritChance,
        RelicEffectType.CRT_DMG   => StatType.CritDmg,
        RelicEffectType.BLEED     => StatType.BleedPower,
        RelicEffectType.HP_REGEN  => StatType.HPRegen,
        RelicEffectType.MAGNET    => StatType.MagnetRadius,
        RelicEffectType.KNOCKBACK => StatType.Knockback,
        RelicEffectType.HEAL_KILL => StatType.HealOnKill,
        _                         => null,
    };
}
