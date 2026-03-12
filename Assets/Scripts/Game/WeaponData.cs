using UnityEngine;

public enum WeaponGrade { Normal, Rare, Epic, Legendary }

/// <summary>
/// 업그레이드 카드 하나에 해당하는 무기 데이터 ScriptableObject.
/// Assets 우클릭 → Create → MuAge → WeaponData 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "MuAge/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("기본 정보")]
    public string weaponName;
    public Sprite icon;

    [Header("등급")]
    public WeaponGrade grade;
    public Sprite gradeSprite;   // 등급 아이콘 이미지

    [Header("가격 & 출현 조건")]
    public int  price        = 20;
    public int  minPlayerLevel = 1;  // 이 레벨 이상일 때 카드풀에 포함
}
