public enum SoundKey
{
    // UI 공통
    UI_Hover     = 0,
    UI_Click     = 1,

    // 상점 액션
    UI_Buy       = 2,
    UI_Reroll    = 3,
    UI_Equip     = 4,
    UI_Unequip   = 5,
    UI_Sell      = 6,
    UI_Merge     = 7,

    // 전투
    Weapon_Shoot    = 8,   // 발사/근접 공격
    Weapon_Attack   = 9,   // 적命中
    Enemy_Hit       = 10,

    // 아이템 획득
    Pickup_Gold  = 11,
    Pickup_Exp   = 12,

    // 플레이어
    Player_Hit   = 13,

    // 기물 스킬
    Structure_Thunder = 14,

    // 증강
    Improv_Select     = 15,
    Improv_Confirm   = 16,

    // 무기
    Weapon_Equip     = 17,

    // 기물 설치
    Structure_Place  = 18,

    // 가챠
    Gacha_Open       = 19,   // 상자 열리는 순간
    Gacha_ItemReveal = 20,   // 아이템 팝인
}
