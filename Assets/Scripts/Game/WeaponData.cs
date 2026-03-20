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
    public WeaponGrade grade;   // Normal/Rare/Elite/Epic/Legend

    [Header("성 단계 (합성으로 증가, 프레임 색 결정)")]
    public int star = 1;        // 1~5성

    [Header("시너지 태그")]
    public WeaponSynergyTag tag1;
    public WeaponSynergyTag tag2;

    [Header("가격 & 출현 조건")]
    public int price   = 20;
    public int chapter = 1;

    [Header("무기 스탯 (WeaponPoint에서 자동 로드)")]
    public float attackDamage;
    public float attackCooldown;
    public float attackRange;
    public float criticalRate;
    public float dotDamage;

    // ────────────────────────────────────────────────────────────────
    // 합성 — 같은 무기를 복사하고 성 1단계 올립니다
    // ────────────────────────────────────────────────────────────────
    public WeaponData Upgraded()
    {
        var next            = CreateInstance<WeaponData>();
        next.weaponName     = weaponName;
        next.icon           = icon;
        next.weaponType     = weaponType;
        next.prefab         = prefab;
        next.grade          = grade;
        next.price          = price;
        next.chapter        = chapter;
        next.star           = star + 1;
        next.tag1           = tag1;
        next.tag2           = tag2;
        next.attackDamage   = attackDamage;
        next.attackCooldown = attackCooldown;
        next.attackRange    = attackRange;
        next.criticalRate   = criticalRate;
        next.dotDamage      = dotDamage;
        return next;
    }

    // ────────────────────────────────────────────────────────────────
    // BGDatabase → WeaponData 변환
    // ────────────────────────────────────────────────────────────────
    public static WeaponData FromDB(DB_무기 row)
    {
        var data        = CreateInstance<WeaponData>();
        data.weaponName = row.f_이름;
        data.grade      = row.f_등급;
        data.star       = 1;
        data.weaponType = row.f_타입;
        data.price      = row.f_가격;
        data.chapter    = row.f_챕터;
        data.prefab     = row.f_프리팹;
        data.tag1 = row.f_태그1;
        data.tag2 = row.f_태그2;

        if (data.prefab != null)
        {
            // 아이콘: SpriteRenderer (최상위 우선, 없으면 자식 탐색)
            data.icon = data.prefab.GetComponent<SpriteRenderer>()?.sprite
                     ?? data.prefab.GetComponentInChildren<SpriteRenderer>()?.sprite;

            // 스탯: WeaponPoint 컴포넌트에서 읽음
            var wp = data.prefab.GetComponentInChildren<WeaponPoint>();
            if (wp != null)
            {
                data.attackDamage   = wp.AttackDamage;
                data.attackCooldown = wp.AttackCooldown;
                data.attackRange    = wp.AttackRange;
                data.criticalRate   = wp.CriticalRate;
                data.dotDamage      = wp.DotDamage;
            }
        }

        return data;
    }
}
