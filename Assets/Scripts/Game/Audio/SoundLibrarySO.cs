using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 사운드 라이브러리.
/// ■ UI      — 상황별 named field
/// ■ 무기/적 — 챕터별 프리셋 (DB 동기화 버튼으로 자동 생성)
///             클립 미할당 시 default 클립으로 폴백
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

    [System.Serializable]
    public class WeaponSoundEntry
    {
        [HideInInspector] public string weaponId;
        [Tooltip("DB f_이름 — 에디터에서 자동 입력됨")]
        public string    weaponName;
        [Tooltip("미할당 시 defaultWeaponAttack 사용")]
        public AudioClip attack;
        [Tooltip("미할당 시 defaultWeaponHit 사용")]
        public AudioClip hit;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [System.Serializable]
    public class ChapterSoundPreset
    {
        public int    chapter;
        [Tooltip("에디터 표시용 라벨 (예: C1 구석기)")]
        public string label;

        [Header("무기 사운드")]
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
    public Entry defaultWeaponAttack;
    public Entry defaultWeaponHit;

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

    // ── 챕터 프리셋 ──────────────────────────────────────────────────────
    [Header("── 챕터별 무기 · 적 사운드 프리셋 ──────────────────────────")]
    [Tooltip("에디터 상단 [DB 동기화] 버튼으로 목록을 자동 생성하세요.")]
    public List<ChapterSoundPreset> chapters = new();

    // ── 런타임 조회 ──────────────────────────────────────────────────────

    public Entry Get(SoundKey key) => key switch
    {
        SoundKey.UI_Click           => uiClick,
        SoundKey.UI_Hover           => uiHover,
        SoundKey.UI_Buy             => uiBuy,
        SoundKey.UI_Reroll          => uiReroll,
        SoundKey.UI_Equip           => uiEquip,
        SoundKey.UI_Unequip         => uiUnequip,
        SoundKey.UI_Sell            => uiSell,
        SoundKey.UI_Merge           => uiMerge,
        SoundKey.Weapon_Equip       => weaponEquip,
        SoundKey.Enemy_Hit          => defaultEnemyHit,
        SoundKey.Pickup_Gold        => pickupGold,
        SoundKey.Pickup_Exp         => pickupExp,
        SoundKey.Improv_Select      => improvSelect,
        SoundKey.Improv_Confirm     => improvConfirm,
        SoundKey.Gacha_Open         => gachaOpen,
        SoundKey.Gacha_ItemReveal   => gachaItemReveal,
        SoundKey.Equip_LevelUp      => equipLevelUp,
        SoundKey.Relic_LevelUp => relicLevelUp,
        _                           => null,
    };

    public (AudioClip clip, float volume) GetWeaponAttack(string weaponName, int chapter)
    {
        var e = FindWeapon(weaponName, chapter);
        if (e?.attack != null) return (e.attack, e.volume);
        return (defaultWeaponAttack?.clip, defaultWeaponAttack?.volume ?? 1f);
    }

    public (AudioClip clip, float volume) GetWeaponHit(string weaponName, int chapter)
    {
        var e = FindWeapon(weaponName, chapter);
        if (e?.hit != null) return (e.hit, e.volume);
        return (defaultWeaponHit?.clip, defaultWeaponHit?.volume ?? 1f);
    }

    private ChapterSoundPreset GetChapter(int chapter)
    {
        foreach (var c in chapters)
            if (c.chapter == chapter) return c;
        return null;
    }

    private WeaponSoundEntry FindWeapon(string weaponName, int chapter)
    {
        var preset = GetChapter(chapter);
        if (preset == null) return null;
        foreach (var w in preset.weapons)
            if (w.weaponName == weaponName) return w;
        return null;
    }
}
