using Mewage.DB;
using UnityEngine;

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

    [Header("등급")]
    public WeaponGrade grade;

    [Header("가격 & 출현 조건")]
    public int price   = 20;
    public int chapter = 1;

    // ────────────────────────────────────────────────────────────────
    // BGDatabase → WeaponData 변환
    // ────────────────────────────────────────────────────────────────
    public static WeaponData FromDB(DB_무기 row)
    {
        var data       = CreateInstance<WeaponData>();
        data.weaponName = row.f_이름;
        data.grade      = row.f_등급;
        data.weaponType = row.f_타입;
        data.price      = row.f_가격;
        data.chapter    = row.f_챕터;
        data.prefab     = row.f_프리팹;
        if (data.prefab != null)
            data.icon = data.prefab.GetComponent<SpriteRenderer>()?.sprite;
        return data;
    }
}
