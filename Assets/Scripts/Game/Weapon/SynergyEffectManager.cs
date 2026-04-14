using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 시너지 등급에 따른 스탯 효과를 PlayerController / PlayerStats에 적용합니다.
/// PlayerInventory.OnChanged 이벤트를 구독해 장착 변경 시 자동으로 재계산합니다.
///
/// 등급 임계값: 1개 이상=동, 3개 이상=은, 5개 이상=금
/// DB_시너지의 f_S1~f_S4 (List&lt;int&gt;): [0]=동 값, [1]=은 값, [2]=금 값
/// </summary>
public enum SynergyStat { None, Attack, Defense, MoveSpeed, MaxHP, GoldBonus, ExpBonus, BleedBonus, CritBonus }

public class SynergyEffectManager : MonoBehaviourSingleton<SynergyEffectManager>
{
    [System.Serializable]
    public struct SynergyTagConfig
    {
        public WeaponSynergyTag tag;
        public SynergyStat      stat;
    }

    [Header("태그 → 스탯 매핑")]
    [SerializeField] private SynergyTagConfig[] tagConfigs = new SynergyTagConfig[]
    {
        new SynergyTagConfig { tag = WeaponSynergyTag.야수,   stat = SynergyStat.Attack    },  // SY_FIELD  : 근접 무기 공격력 증가
        new SynergyTagConfig { tag = WeaponSynergyTag.채집,   stat = SynergyStat.ExpBonus  },  // SY_GATH   : 획득 경험치 증가
        new SynergyTagConfig { tag = WeaponSynergyTag.사냥꾼, stat = SynergyStat.BleedBonus},  // SY_HUNTER : 모든 무기 출혈 지수 증가
        new SynergyTagConfig { tag = WeaponSynergyTag.지혜,   stat = SynergyStat.CritBonus },  // SY_WISDOM : 보스 치명타 증가
    };

    protected override bool DontDestroy => false;

    private float _curAtk;
    private float _curDef;
    private float _curSpeed;
    private float _curMaxHp;
    private int   _curGoldBonus;
    private int   _curExpBonus;
    private float _curBleed;
    private float _curCrit;

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnEnable()
    {
        var inv = PlayerInventory.Shared;
        if (inv != null) inv.OnChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        var inv = PlayerInventory.Shared;
        if (inv != null) inv.OnChanged -= OnInventoryChanged;
    }

    private void OnInventoryChanged() => RecalculateSynergies();

    /// <summary>모든 시너지 태그를 재계산하고 스탯을 반영합니다. 외부에서도 호출 가능.</summary>
    public void RecalculateSynergies()
    {
        int chapter = UpgradeManager.Shared?.CurrentChapter ?? 1;
        var row = DB_시너지.FindEntity(r => r.f_챕터 == chapter && !string.IsNullOrEmpty(r.f_SY1));

        float atk = 0f, def = 0f, speed = 0f, maxHp = 0f, bleed = 0f, crit = 0f;
        int   goldBonus = 0, expBonus = 0;

        foreach (var cfg in tagConfigs)
        {
            if (cfg.tag == WeaponSynergyTag.None || cfg.stat == SynergyStat.None) continue;

            var statValues = GetStatValuesForTag(row, cfg.tag);
            if (statValues == null || statValues.Count == 0) continue;

            int count   = SynergyHelper.CountEquippedTag(cfg.tag);
            int tierIdx = GetTierIndex(count); // 0=없음, 1=동, 2=은, 3=금
            if (tierIdx == 0 || tierIdx - 1 >= statValues.Count) continue;

            float value = statValues[tierIdx - 1];

            switch (cfg.stat)
            {
                case SynergyStat.Attack:    atk       += value;       break;
                case SynergyStat.Defense:   def       += value;       break;
                case SynergyStat.MoveSpeed: speed     += value;       break;
                case SynergyStat.MaxHP:     maxHp     += value;       break;
                case SynergyStat.GoldBonus: goldBonus += (int)value;  break;
                case SynergyStat.ExpBonus:  expBonus  += (int)value;  break;
                case SynergyStat.BleedBonus:bleed     += value;       break;
                case SynergyStat.CritBonus: crit      += value / 100f; break; // % → fraction
            }
        }

        ApplyBonuses(atk, def, speed, maxHp, goldBonus, expBonus, bleed, crit);
    }

    private void ApplyBonuses(float atk, float def, float speed, float maxHp,
                              int goldBonus, int expBonus, float bleed, float crit)
    {
        PlayerController.Instance?.SetSynergyBonus(atk, def, speed, maxHp, bleed, crit);
        PlayerStats.Shared?.SetSynergyBonus(goldBonus, expBonus);

        _curAtk       = atk;
        _curDef       = def;
        _curSpeed     = speed;
        _curMaxHp     = maxHp;
        _curGoldBonus = goldBonus;
        _curExpBonus  = expBonus;
        _curBleed     = bleed;
        _curCrit      = crit;
    }

    // ── 내부 유틸 ─────────────────────────────────────────────────────

    private static List<int> GetStatValuesForTag(DB_시너지 row, WeaponSynergyTag tag)
    {
        if (row == null) return null;
        string id = SynergyHelper.TagToID(tag);
        if (string.IsNullOrEmpty(id)) return null;
        if (row.f_SY1 == id) return row.f_S1;
        if (row.f_SY2 == id) return row.f_S2;
        if (row.f_SY3 == id) return row.f_S3;
        if (row.f_SY4 == id) return row.f_S4;
        return null;
    }

    /// <summary>장착 수 → 티어 인덱스. 0=없음, 1=동(1+), 2=은(3+), 3=금(5+)</summary>
    public static int GetTierIndex(int count)
    {
        if (count >= 5) return 3;
        if (count >= 3) return 2;
        if (count >= 1) return 1;
        return 0;
    }
}
