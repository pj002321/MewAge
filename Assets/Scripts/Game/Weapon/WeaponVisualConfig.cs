using System;
using UnityEngine;

/// <summary>
/// 무기 아이콘 관련 비주얼 에셋을 한 곳에서 관리하는 ScriptableObject.
/// Resources/WeaponVisualConfig 에 저장해두면 Shared로 전역 접근 가능합니다.
///
/// 사용처: UpgradeCard, EquipSlot, InvenSlot
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Weapon Visual Config", fileName = "WeaponVisualConfig")]
public class WeaponVisualConfig : ScriptableObject
{
    // ── 등급 프레임 ──────────────────────────────────────────────────
    [Serializable]
    public struct GradeEntry
    {
        public WeaponGrade grade;
        public Sprite      sprite;
    }

    // ── 무기 타입 아이콘 ─────────────────────────────────────────────
    [Serializable]
    public struct TypeEntry
    {
        public WeaponType type;
        public Sprite     sprite;
    }

    [Header("등급 프레임 스프라이트 (Normal~Legend)")]
    public GradeEntry[] gradeFrames;

    [Header("무기 타입 아이콘")]
    public TypeEntry[] typeIcons;

    // ── 특성 뱃지 이미지 (등급별) ────────────────────────────────────
    [Serializable]
    public struct TraitBadgeEntry
    {
        public WeaponGrade grade;
        public Sprite      sprite;
    }

    [Header("특성 뱃지 이미지 (등급별, 특수효과 보유 무기에 표시)")]
    public TraitBadgeEntry[] traitBadges;

    // ── 조회 헬퍼 ───────────────────────────────────────────────────
    public Sprite GetGradeSprite(WeaponGrade grade)
    {
        foreach (var e in gradeFrames)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    public Sprite GetTypeSprite(WeaponType type)
    {
        foreach (var e in typeIcons)
            if (e.type == type) return e.sprite;
        return null;
    }

    public Sprite GetTraitBadgeSprite(WeaponGrade grade)
    {
        foreach (var e in traitBadges)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    // ── 전역 접근 ────────────────────────────────────────────────────
    private static WeaponVisualConfig _shared;
    public static WeaponVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<WeaponVisualConfig>("Weapons/WeaponUI/WeaponVisualConfig");
            return _shared;
        }
    }
}
