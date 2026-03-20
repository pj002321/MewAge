using Mewage.DB;
using UnityEngine;

/// <summary>희귀도 — 상점 뽑기 확률 결정. DB_무기.f_등급에서 읽습니다.</summary>
public enum WeaponGrade { Normal, Rare, Elite, Epic, Legend }

/// <summary>
/// 무기 한 종류의 런타임 데이터.
/// DB_무기에서 FromDB()로 생성합니다.
/// </summary>
public class WeaponData : ScriptableObject
{
    [Header("기본 정보")]
    public string     weaponName;
    public Sprite     icon;
    public WeaponType weaponType;
    public GameObject prefab;

    [Header("희귀도 (상점 뽑기 확률)")]
    public WeaponGrade grade;

    [Header("성 단계 (합성으로 증가, 프레임 색 결정)")]
    public int star = 1;        // 1~5성

    [Header("시너지 태그")]
    public WeaponSynergyTag tag1;
    public WeaponSynergyTag tag2;

    [Header("가격 & 출현 조건")]
    public int price   = 20;
    public int chapter = 1;

    [Header("스토리 설명")]
    public string storyDescription;

    // ────────────────────────────────────────────────────────────────
    // 기본 스탯 (1성 기준, WeaponPoint에서 로드. 합성 중 변하지 않음)
    // ────────────────────────────────────────────────────────────────
    [Header("기본 스탯 (1성 기준, WeaponPoint)")]
    public float baseAttackDamage;
    public float baseAttackCooldown;
    public float baseAttackRange;
    public float baseCriticalRate;
    public float baseBleedDamage;   // 출혈
    public float baseLifeSteal;     // 흡혈

    // ────────────────────────────────────────────────────────────────
    // 현재 스탯 (성 보너스 적용 후)
    // ────────────────────────────────────────────────────────────────
    [Header("현재 스탯 (성 보너스 적용 후)")]
    public float attackDamage;
    public float attackCooldown;
    public float attackRange;
    public float criticalRate;
    public float bleedDamage;   // 출혈
    public float lifeSteal;     // 흡혈

    // ────────────────────────────────────────────────────────────────
    // 성별 강화 보너스
    // ────────────────────────────────────────────────────────────────
    [Header("성별 강화 보너스")]
    public WeaponStarBonus bonus2Star;
    public WeaponStarBonus bonus3Star;
    public WeaponStarBonus bonus4Star;

    // ────────────────────────────────────────────────────────────────
    // 합성 — 같은 무기를 복사하고 성 1단계 올립니다
    // ────────────────────────────────────────────────────────────────
    public WeaponData Upgraded()
    {
        var next              = CreateInstance<WeaponData>();
        next.weaponName       = weaponName;
        next.icon             = icon;
        next.weaponType       = weaponType;
        next.prefab           = prefab;
        next.grade            = grade;
        next.price            = price;
        next.chapter          = chapter;
        next.star             = star + 1;
        next.tag1             = tag1;
        next.tag2             = tag2;
        next.storyDescription = storyDescription;
        next.bonus2Star       = bonus2Star;
        next.bonus3Star       = bonus3Star;
        next.bonus4Star       = bonus4Star;

        next.baseAttackDamage   = baseAttackDamage;
        next.baseAttackCooldown = baseAttackCooldown;
        next.baseAttackRange    = baseAttackRange;
        next.baseCriticalRate   = baseCriticalRate;
        next.baseBleedDamage    = baseBleedDamage;
        next.baseLifeSteal      = baseLifeSteal;

        next.ResetToBase();
        next.ApplyBonusesUpToStar();

        return next;
    }

    // ────────────────────────────────────────────────────────────────
    // BGDatabase → WeaponData 변환
    // ────────────────────────────────────────────────────────────────
    public static WeaponData FromDB(DB_무기 row)
    {
        WeaponStatRegistry.EnsureLoaded();

        var data              = CreateInstance<WeaponData>();
        data.weaponName       = row.f_이름;
        data.grade            = row.f_등급;
        data.star             = 1;
        data.weaponType       = row.f_타입;
        data.price            = row.f_가격;
        data.chapter          = row.f_챕터;
        data.prefab           = row.f_프리팹;
        data.tag1             = row.f_태그1;
        data.tag2             = row.f_태그2;
        data.storyDescription = row.f_스토리설명;

        data.bonus2Star = WeaponStatRegistry.Build(row.f_속성2성ID, row.f_S1);
        data.bonus3Star = WeaponStatRegistry.Build(row.f_속성3성ID, row.f_S2);
        data.bonus4Star = WeaponStatRegistry.Build(row.f_속성4성ID, row.f_S3);

        if (data.prefab != null)
        {
            data.icon = data.prefab.GetComponent<SpriteRenderer>()?.sprite
                     ?? data.prefab.GetComponentInChildren<SpriteRenderer>()?.sprite;

            var wp = data.prefab.GetComponentInChildren<WeaponPoint>();
            if (wp != null)
            {
                data.baseAttackDamage   = wp.AttackDamage;
                data.baseAttackCooldown = wp.AttackCooldown;
                data.baseAttackRange    = wp.AttackRange;
                data.baseCriticalRate   = wp.CriticalRate;
                data.baseBleedDamage    = wp.BleedDamage;
                data.baseLifeSteal      = wp.LifeSteal;
            }
        }

        data.ResetToBase();
        return data;
    }

    // ────────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ────────────────────────────────────────────────────────────────

    private void ResetToBase()
    {
        attackDamage   = baseAttackDamage;
        attackCooldown = baseAttackCooldown;
        attackRange    = baseAttackRange;
        criticalRate   = baseCriticalRate;
        bleedDamage    = baseBleedDamage;
        lifeSteal      = baseLifeSteal;
    }

    private void ApplyBonusesUpToStar()
    {
        if (star >= 2) WeaponStatRegistry.ApplyBonus(this, bonus2Star);
        if (star >= 3) WeaponStatRegistry.ApplyBonus(this, bonus3Star);
        if (star >= 4) WeaponStatRegistry.ApplyBonus(this, bonus4Star);
    }
}
