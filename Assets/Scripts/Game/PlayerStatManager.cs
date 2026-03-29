using UnityEngine;

/// <summary>
/// 플레이어 캐릭터 능력치(13종)를 관리합니다.
/// · 기본값(baseXxx) : Inspector 또는 캐릭터 선택 시 설정
/// · % 모디파이어   : AddPctMod()로 누적 (증강 등)
/// · 최종값         : GetFinal(stat) = base × (1 + totalPct / 100)
/// · 변경 시        : PlayerController.ApplyStatSheet() 자동 호출
/// </summary>
public class PlayerStatManager : MonoBehaviourSingleton<PlayerStatManager>
{
    protected override bool DontDestroy => true;

    [Header("기본 능력치 (캐릭터 기준값)")]
    [SerializeField] private float baseATK               = 0f;
    [SerializeField] private float baseDEF               = 0f;
    [SerializeField] private float baseMaxHP             = 100f;
    [SerializeField] private float baseBleedPower        = 0f;
    [SerializeField] private float baseHPRegen           = 0f;
    [SerializeField] private float baseMoveSpeed         = 5f;
    [SerializeField] private float baseMagnetRadius      = 1.5f;
    [SerializeField] private float baseAtkSpeed          = 0f;    // 0% 가 기본
    [SerializeField] private float baseEvasion           = 0f;
    [SerializeField] private float baseCritChance        = 0f;
    [SerializeField] private float baseCritDmg           = 0f;
    [SerializeField] private float baseStructureCooldown = 30f;
    [SerializeField] private float baseKnockback         = 0f;
    [SerializeField] private float baseHealOnKill        = 0f;

    private static readonly int StatCount =
        System.Enum.GetValues(typeof(StatType)).Length;

    // 스탯별 % 모디파이어 누계 (예: 10 = +10%)
    private readonly float[] _pctMods = new float[StatCount];

    // 장비 장착으로 누적된 플랫(고정값) 보너스
    private readonly float[] _equipFlat = new float[StatCount];

    // ── 외부 API ────────────────────────────────────────────────────

    /// <summary>매 게임 시작 시 % 모디파이어를 전부 초기화합니다. (GameManager.Start에서 호출)</summary>
    public void ResetMods()
    {
        System.Array.Clear(_pctMods, 0, _pctMods.Length);
    }

    /// <summary>기본값을 런타임에 설정하고 PlayerController에 즉시 반영합니다. (캐릭터 초기화 시 사용)</summary>
    public void SetBase(StatType stat, float value)
    {
        switch (stat)
        {
            case StatType.ATK:               baseATK               = value; break;
            case StatType.DEF:               baseDEF               = value; break;
            case StatType.MaxHP:             baseMaxHP             = value; break;
            case StatType.BleedPower:        baseBleedPower        = value; break;
            case StatType.HPRegen:           baseHPRegen           = value; break;
            case StatType.MoveSpeed:         baseMoveSpeed         = value; break;
            case StatType.MagnetRadius:      baseMagnetRadius      = value; break;
            case StatType.AtkSpeed:          baseAtkSpeed          = value; break;
            case StatType.Evasion:           baseEvasion           = value; break;
            case StatType.CritChance:        baseCritChance        = value; break;
            case StatType.CritDmg:           baseCritDmg           = value; break;
            case StatType.StructureCooldown: baseStructureCooldown = value; break;
            case StatType.Knockback:         baseKnockback         = value; break;
            case StatType.HealOnKill:        baseHealOnKill        = value; break;
        }
        ApplyToPlayer();
        StatPanelUI.Shared?.Refresh();
    }

    /// <summary>% 모디파이어를 추가하고 PlayerController에 즉시 반영합니다.</summary>
    public void AddPctMod(StatType stat, float pct)
    {
        _pctMods[(int)stat] += pct;
        ApplyToPlayer();
        StatPanelUI.Shared?.Refresh();
    }

    /// <summary>기본값을 반환합니다.</summary>
    public float GetBase(StatType stat) => BaseValue(stat);

    /// <summary>최종값 = (base + equipFlat) × (1 + totalPct / 100) 을 반환합니다.</summary>
    public float GetFinal(StatType stat)
    {
        float b    = BaseValue(stat);
        float flat = _equipFlat[(int)stat];
        float pct  = _pctMods[(int)stat];
        return (b + flat) * (1f + pct / 100f);
    }

    /// <summary>장비 장착 플랫 보너스를 덮어씁니다. EquipmentInventory에서 호출.</summary>
    public void SetEquipFlat(StatType stat, float value)
    {
        _equipFlat[(int)stat] = value;
        ApplyToPlayer();
        StatPanelUI.Shared?.Refresh();
    }

    /// <summary>모든 장비 플랫 보너스를 초기화합니다.</summary>
    public void ResetEquipFlat()
    {
        System.Array.Clear(_equipFlat, 0, _equipFlat.Length);
        ApplyToPlayer();
        StatPanelUI.Shared?.Refresh();
    }

    /// <summary>현재 누적 % 모디파이어를 반환합니다.</summary>
    public float GetPctMod(StatType stat) => _pctMods[(int)stat];

    /// <summary>현재 장비 플랫 보너스를 반환합니다.</summary>
    public float GetEquipFlat(StatType stat) => _equipFlat[(int)stat];

    // ── 초기화 ──────────────────────────────────────────────────────

    private void Start()
    {
        // PlayerController.Awake 완료 후 적용
        ApplyToPlayer();
        StatPanelUI.Shared?.Refresh();
    }

    // ── 내부 ────────────────────────────────────────────────────────

    private void ApplyToPlayer()
    {
        PlayerController.Instance?.ApplyStatSheet(this);
    }

    private float BaseValue(StatType stat) => stat switch
    {
        StatType.ATK               => baseATK,
        StatType.DEF               => baseDEF,
        StatType.MaxHP             => baseMaxHP,
        StatType.BleedPower        => baseBleedPower,
        StatType.HPRegen           => baseHPRegen,
        StatType.MoveSpeed         => baseMoveSpeed,
        StatType.MagnetRadius      => baseMagnetRadius,
        StatType.AtkSpeed          => baseAtkSpeed,
        StatType.Evasion           => baseEvasion,
        StatType.CritChance        => baseCritChance,
        StatType.CritDmg           => baseCritDmg,
        StatType.StructureCooldown => baseStructureCooldown,
        StatType.Knockback         => baseKnockback,
        StatType.HealOnKill        => baseHealOnKill,
        _                          => 0f,
    };

    // ── 스탯 이름 (UI 표시용) ────────────────────────────────────────

    public static string GetStatName(StatType stat) => stat switch
    {
        StatType.ATK               => "공격력",
        StatType.DEF               => "방어력",
        StatType.MaxHP             => "최대 체력",
        StatType.BleedPower        => "출혈 데미지",
        StatType.HPRegen           => "체력 회복",
        StatType.MoveSpeed         => "이동속도",
        StatType.MagnetRadius      => "흡수 반경",
        StatType.AtkSpeed          => "공격속도",
        StatType.Evasion           => "회피",
        StatType.CritChance        => "크리티컬 확률",
        StatType.CritDmg           => "크리티컬 데미지",
        StatType.StructureCooldown => "기물 쿨타임",
        StatType.Knockback         => "넉백",
        StatType.HealOnKill        => "처치 회복",
        _                          => stat.ToString(),
    };

    /// <summary>스탯 값 포맷 (단위 포함). UI 표시용.</summary>
    public static string FormatValue(StatType stat, float value) => stat switch
    {
        StatType.Evasion           => $"{value * 100f:0.#}%",          // fraction → %
        StatType.CritChance        => $"{value:0.#}%",
        StatType.CritDmg           => $"+{value:0.#}%",
        StatType.AtkSpeed          => $"+{value:0.#}%",
        StatType.HPRegen           => $"{value:0.##}/s",
        StatType.HealOnKill        => $"{value:0.#}%",
        StatType.StructureCooldown => $"{value:0.#}s",
        StatType.MagnetRadius      => $"{value:0.##}u",               // units
        _                          => $"{value:0.##}",
    };

}
