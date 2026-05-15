using Mewage.DB;
using UnityEngine;

public enum EquipCategory { RING, NECK, FOOT, TOP, CLOAK, HEAD }
public enum EquipGrade    { NORMAL, RARE, EPIC, LEGENDARY }
public enum EquipCiv      { 구석기, 중석기, 신석기, 청동기, 철기, 고대, 중세, 근세, 근대, 현대 }

/// <summary>
/// DB_장비 행의 런타임 복사본.
/// DB 필드: f_장비ID, f_이름, f_카테고리, f_등급(NORMAL/RARE/EPIC/LEGENDARY),
///          f_스탯ID, f_수치, f_문명, f_설명
/// 아이콘: HomeEquipVisualConfig.Shared.GetIcon(equipId)
/// </summary>
public class EquipmentData
{
    public string        equipId;
    public string        itemName;
    public EquipCategory category;
    public EquipGrade    grade;
    public EquipCiv      civilization;
    public string        statId;      // STAT_ATK, STAT_DEF, STAT_HP, ...
    public float         statValue;
    public string        description;

    public const int MaxLevel = 100;
    public int level = 1;

    // 레벨마다 기본 수치의 10%씩 증가
    public float EffectiveStatValue => statValue * (1f + 0.1f * (level - 1));

    public void SaveLevel()  => PlayerPrefs.SetInt($"EquipLevel_{equipId}", level);
    public void LoadLevel()  => level = PlayerPrefs.GetInt($"EquipLevel_{equipId}", 1);

    private Sprite _icon;
    public  Sprite  Icon => _icon ??= HomeEquipVisualConfig.Shared?.GetIcon(equipId);

    // ── ID로 DB에서 생성 ─────────────────────────────────────────────
    public static EquipmentData FromId(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EquipmentData result = null;
        DB_장비.ForEachEntity(row =>
        {
            if (result == null && row?.f_장비ID == id)
                result = FromDB(row);
        });
        return result;
    }

    // ── DB에서 생성 ─────────────────────────────────────────────────
    public static EquipmentData FromDB(DB_장비 row)
    {
        var e = new EquipmentData
        {
            itemName    = row.f_이름,
            statId      = row.f_스탯ID,
            statValue   = (float)row.f_수치,
            description = row.f_설명,
        };
        e.equipId = row.f_장비ID;

        if (!System.Enum.TryParse(row.f_카테고리, true, out e.category))
            e.category = EquipCategory.RING;
        if (!System.Enum.TryParse(row.f_등급, true, out e.grade))
            e.grade = EquipGrade.NORMAL;
        if (!System.Enum.TryParse(row.f_문명, true, out e.civilization))
            e.civilization = EquipCiv.구석기;

        e.LoadLevel();
        return e;
    }

    // ── 등급 색상 ────────────────────────────────────────────────────
    public static Color GradeColor(EquipGrade g) => g switch
    {
        EquipGrade.LEGENDARY => new Color(0.90f, 0.70f, 0.10f, 1f), // 금
        EquipGrade.EPIC      => new Color(0.55f, 0.20f, 0.85f, 1f), // 보라
        EquipGrade.RARE      => new Color(0.15f, 0.45f, 0.90f, 1f), // 파랑
        _                    => new Color(0.60f, 0.60f, 0.60f, 1f), // 회색
    };

    // ── 등급 표시 이름 ───────────────────────────────────────────────
    public string GradeLabel() => grade switch
    {
        EquipGrade.LEGENDARY => "전설",
        EquipGrade.EPIC      => "에픽",
        EquipGrade.RARE      => "레어",
        _                    => "일반",
    };

    // ── 카테고리 표시 이름 ──────────────────────────────────────────
    public string CategoryLabel() => category switch
    {
        EquipCategory.RING  => "반지",
        EquipCategory.NECK  => "목걸이",
        EquipCategory.FOOT  => "발",
        EquipCategory.TOP   => "상의",
        EquipCategory.CLOAK => "망토",
        EquipCategory.HEAD  => "머리",
        _                   => category.ToString(),
    };

    // ── DB statId → StatType 변환 ────────────────────────────────────
    /// <summary>DB f_스탯ID 문자열을 StatType으로 변환합니다. 미지원 ID는 null 반환.</summary>
    public static StatType? StatIdToType(string statId) => statId switch
    {
        "STAT_ATK"      => StatType.ATK,
        "STAT_DEF"      => StatType.DEF,
        "STAT_HP"       => StatType.MaxHP,
        "STAT_CRI_DMG"  => StatType.CritDmg,
        "STAT_CRI_RATE" => StatType.CritChance,
        "STAT_EVA_RATE" => StatType.Evasion,
        "STAT_SPD_MOV"  => StatType.MoveSpeed,
        "STAT_SPD_ATK"  => StatType.AtkSpeed,
        "STAT_DOT_DMG"  => StatType.BleedPower,
        _               => (StatType?)null,
    };

    // ── 스탯 설명 문자열 ─────────────────────────────────────────────
    public string StatDescription()
    {
        if (string.IsNullOrEmpty(statId)) return string.Empty;
        string label = statId switch
        {
            "STAT_ATK"        => "공격력",
            "STAT_DEF"        => "방어력",
            "STAT_HP"         => "최대 체력",
            "STAT_CRI_DMG"    => "치명타 피해",
            "STAT_ATK_RNG"    => "공격 사거리",
            "STAT_CUR_REWARD" => "골드 보너스",
            "STAT_STUN"       => "적 둔화",
            "STAT_EVA_RATE"   => "회피율",
            "STAT_SPD_MOV"    => "이동속도",
            "STAT_DOT_DMG"    => "출혈 피해",
            "STAT_SPD_ATK"    => "공격속도",
            "STAT_CRI_RATE"   => "치명타 확률",
            _                 => statId,
        };
        return $"{label} +{EffectiveStatValue:F0}";
    }
}
