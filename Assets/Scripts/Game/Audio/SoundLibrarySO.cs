using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 사운드 라이브러리.
/// ■ UI      — 상황별 named field
/// ■ 무기    — 타입별 프리셋 (DB 동기화 버튼으로 자동 생성, 클립은 직접 할당)
///             조회 우선순위: 무기 ID 개별 클립 → 글로벌 default 클립
/// ■ 적      — 기본 폴백 클립
/// </summary>
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "MuAge/Sound Library")]
public class SoundLibrarySO : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume      = 1f;
        [Tooltip("같은 사운드 최소 재생 간격(초). 0이면 제한 없음")]
        public float     minInterval = 0.05f;
    }

    /// <summary>무기 1개의 사운드 슬롯. DB 동기화로 자동 생성됨.</summary>
    [System.Serializable]
    public class WeaponSoundEntry
    {
        [Tooltip("DB f_무기ID (자동 입력)")]
        public string    weaponId;
        [Tooltip("DB f_이름   (자동 입력)")]
        public string    weaponName;
        [Tooltip("공격 시작음 — 미할당 시 defaultWeaponShoot 사용")]
        public AudioClip shoot;
        [Tooltip("적 명중음  — 미할당 시 defaultWeaponHit 사용")]
        public AudioClip hit;
        [Range(0f, 1f)] public float volume = 1f;
    }

    /// <summary>무기 타입 1개 묶음. DB 동기화로 자동 생성됨.</summary>
    [System.Serializable]
    public class WeaponTypeSoundPreset
    {
        [Tooltip("무기 타입 (자동 입력)")]
        public WeaponType weaponType;
        public List<WeaponSoundEntry> weapons = new();
    }

    // ── UI 공통 ──────────────────────────────────────────────────────────
    [Header("── UI 공통 ─────────────────────────────────────────────────")]
    public Entry uiClick;
    public Entry uiHover;

    [Header("── 상점 액션 ───────────────────────────────────────────────")]
    public Entry uiBuy;
    public Entry uiReroll;
    public Entry uiEquip;
    public Entry uiUnequip;
    public Entry uiSell;
    public Entry uiMerge;

    [Header("── 무기 장착 / 기본 폴백 ────────────────────────────────────")]
    public Entry weaponEquip;
    [Tooltip("개별 무기에 shoot 클립 없을 때 사용")]
    public Entry defaultWeaponAttack;
    [Tooltip("개별 무기에 hit 클립 없을 때 사용")]
    public Entry defaultWeaponHit;

    [Header("── 플레이어 ──────────────────────────────────────────────────")]
    public Entry playerHit;

    [Header("── 적 기본 폴백 ──────────────────────────────────────────────")]
    public Entry defaultEnemyHit;
    public Entry defaultEnemyDeath;

    [Header("── 아이템 픽업 ──────────────────────────────────────────────")]
    public Entry pickupGold;
    public Entry pickupExp;

    [Header("── 증강 ────────────────────────────────────────────────────")]
    public Entry improvSelect;
    public Entry improvConfirm;

    [Header("── 가챠 ────────────────────────────────────────────────────")]
    public Entry gachaOpen;
    public Entry gachaItemReveal;

    [Header("── 장비 ────────────────────────────────────────────────────")]
    public Entry equipLevelUp;

    [Header("── 유물 ────────────────────────────────────────────────────")]
    public Entry relicLevelUp;

    // ── 무기 타입별 프리셋 ───────────────────────────────────────────────
    [Header("── 무기 타입별 사운드 프리셋 ──────────────────────────────────")]
    [Tooltip("에디터 상단 [DB 동기화] 버튼으로 타입·무기 목록을 자동 생성합니다.")]
    public List<WeaponTypeSoundPreset> weaponTypePresets = new();

    // ── SoundKey 조회 ────────────────────────────────────────────────────

    public Entry Get(SoundKey key) => key switch
    {
        SoundKey.UI_Click         => uiClick,
        SoundKey.UI_Hover         => uiHover,
        SoundKey.UI_Buy           => uiBuy,
        SoundKey.UI_Reroll        => uiReroll,
        SoundKey.UI_Equip         => uiEquip,
        SoundKey.UI_Unequip       => uiUnequip,
        SoundKey.UI_Sell          => uiSell,
        SoundKey.UI_Merge         => uiMerge,
        SoundKey.Weapon_Equip     => weaponEquip,
        SoundKey.Weapon_Shoot     => defaultWeaponAttack,
        SoundKey.Weapon_Attack    => defaultWeaponHit,
        SoundKey.Player_Hit       => playerHit,
        SoundKey.Enemy_Hit        => defaultEnemyHit,
        SoundKey.Enemy_Die        => defaultEnemyDeath,
        SoundKey.Pickup_Gold      => pickupGold,
        SoundKey.Pickup_Exp       => pickupExp,
        SoundKey.Improv_Select    => improvSelect,
        SoundKey.Improv_Confirm   => improvConfirm,
        SoundKey.Gacha_Open       => gachaOpen,
        SoundKey.Gacha_ItemReveal => gachaItemReveal,
        SoundKey.Equip_LevelUp    => equipLevelUp,
        SoundKey.Relic_LevelUp    => relicLevelUp,
        _                         => null,
    };

    // ── 무기 사운드 런타임 조회 ──────────────────────────────────────────

    /// <summary>무기 ID로 발사음 조회. 미할당 시 defaultWeaponAttack 반환.</summary>
    public (AudioClip clip, float volume) GetWeaponShoot(string weaponId)
    {
        var e = FindWeapon(weaponId);
        if (e?.shoot != null) return (e.shoot, e.volume);
        return (defaultWeaponAttack?.clip, defaultWeaponAttack?.volume ?? 1f);
    }

    /// <summary>무기 ID로 명중음 조회. 미할당 시 defaultWeaponHit 반환.</summary>
    public (AudioClip clip, float volume) GetWeaponHit(string weaponId)
    {
        var e = FindWeapon(weaponId);
        if (e?.hit != null) return (e.hit, e.volume);
        return (defaultWeaponHit?.clip, defaultWeaponHit?.volume ?? 1f);
    }

    private WeaponSoundEntry FindWeapon(string weaponId)
    {
        foreach (var preset in weaponTypePresets)
            foreach (var w in preset.weapons)
                if (w.weaponId == weaponId) return w;
        return null;
    }
}
