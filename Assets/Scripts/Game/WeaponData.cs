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

    [Header("가격 & 출현 조건")]
    public int price   = 20;
    public int chapter = 1;

    // ────────────────────────────────────────────────────────────────
    // 합성 — 같은 무기를 복사하고 성 1단계 올립니다
    // ────────────────────────────────────────────────────────────────
    public WeaponData Upgraded()
    {
        var next        = CreateInstance<WeaponData>();
        next.weaponName = weaponName;
        next.icon       = icon;
        next.weaponType = weaponType;
        next.prefab     = prefab;
        next.grade      = grade;
        next.price      = price;
        next.chapter    = chapter;
        next.star       = star + 1;   // 성 단계만 올림
        return next;
    }

    // ────────────────────────────────────────────────────────────────
    // BGDatabase → WeaponData 변환
    // ────────────────────────────────────────────────────────────────
    public static WeaponData FromDB(DB_무기 row)
    {
        var data        = CreateInstance<WeaponData>();
        data.weaponName = row.f_이름;
        data.grade      = row.f_등급;   // 희귀도는 DB에서 읽음
        data.star       = 1;            // 구매 시 항상 1성, 합성으로만 성 올라감
        data.weaponType = row.f_타입;
        data.price      = row.f_가격;
        data.chapter    = row.f_챕터;
        data.prefab     = row.f_프리팹;
        if (data.prefab != null)
            data.icon = data.prefab.GetComponent<SpriteRenderer>()?.sprite;
        return data;
    }

}
