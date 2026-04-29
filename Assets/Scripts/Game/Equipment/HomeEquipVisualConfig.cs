using System;
using UnityEngine;

/// <summary>
/// 장비 등급별 비주얼 에셋을 한 곳에서 관리하는 ScriptableObject.
/// Resources/Equip/HomeEquipVisualConfig 에 저장해두면 Shared로 전역 접근 가능합니다.
///
/// 사용처:
///   frameGrades  — 장비 슬롯·목록의 등급 테두리
///   bannerGrades — 장비 세부정보 팝업의 등급 배너
///   indexGrades  — 장비 설명란 옆 인덱스 아이콘 (해금/잠금 2종)
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Home Equip Visual Config", fileName = "HomeEquipVisualConfig")]
public class HomeEquipVisualConfig : ScriptableObject
{
    [Serializable]
    public struct GradeFrameEntry
    {
        public EquipGrade grade;
        public Sprite     sprite;
    }

    [Serializable]
    public struct GradeBannerEntry
    {
        public EquipGrade grade;
        public Sprite     sprite;
    }

    [Serializable]
    public struct GradeIndexEntry
    {
        public EquipGrade grade;
        public Sprite     unlocked;
        public Sprite     locked;
    }

    [Serializable]
    public struct GradeTagEntry
    {
        public EquipGrade grade;
        public Sprite     sprite;
    }

    [Header("등급 테두리")]
    public GradeFrameEntry[] frameGrades;

    [Header("등급 배너 (세부정보 팝업)")]
    public GradeBannerEntry[] bannerGrades;

    [Header("등급 인덱스 아이콘 (해금/잠금)")]
    public GradeIndexEntry[] indexGrades;

    [Header("등급 태그 이미지")]
    public GradeTagEntry[] tagGrades;

    // ── 조회 헬퍼 ────────────────────────────────────────────────────────
    public Sprite GetFrame(EquipGrade grade)
    {
        foreach (var e in frameGrades)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    public Sprite GetBanner(EquipGrade grade)
    {
        foreach (var e in bannerGrades)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    public Sprite GetIndex(EquipGrade grade, bool unlocked)
    {
        foreach (var e in indexGrades)
            if (e.grade == grade) return unlocked ? e.unlocked : e.locked;
        return null;
    }

    public Sprite GetTag(EquipGrade grade)
    {
        foreach (var e in tagGrades)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    // ── 전역 접근 ─────────────────────────────────────────────────────────
    private static HomeEquipVisualConfig _shared;
    public static HomeEquipVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<HomeEquipVisualConfig>("Equip/HomeEquipVisualConfig");
            return _shared;
        }
    }
}
