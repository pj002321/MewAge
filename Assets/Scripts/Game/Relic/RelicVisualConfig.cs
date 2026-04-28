using System;
using UnityEngine;

/// <summary>
/// 유물 등급별 비주얼 에셋을 한 곳에서 관리하는 ScriptableObject.
/// Resources/Relic/RelicVisualConfig 에 저장해두면 Shared로 전역 접근 가능합니다.
///
/// 사용처:
///   frameGrade  — RelicSlot 의 등급 테두리
///   iconGrade   — RelicInfoPopup 의 아이콘 등급 프레임
///   bannerGrade — RelicInfoPopup 의 등급 배너
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Relic Visual Config", fileName = "RelicVisualConfig")]
public class RelicVisualConfig : ScriptableObject
{
    public enum Grade { Common, Rare, Epic, Legendary, Mythic }

    [Serializable]
    public struct FrameEntry
    {
        public Grade  grade;
        public Sprite sprite;
    }

    [Serializable]
    public struct IconEntry
    {
        public Grade  grade;
        public Sprite sprite;
    }

    [Serializable]
    public struct BannerEntry
    {
        public Grade  grade;
        public Sprite sprite;
    }

    [Serializable]
    public struct RelicIconEntry
    {
        public string relicId;
        public Sprite icon;
    }

    [Header("등급 테두리 (RelicSlot)")]
    public FrameEntry[] frameGrades;

    [Header("아이콘 등급 프레임 (RelicInfoPopup)")]
    public IconEntry[] iconGrades;

    [Header("등급 배너 (RelicInfoPopup)")]
    public BannerEntry[] bannerGrades;

    [Header("유물 아이콘 (ID별)")]
    public RelicIconEntry[] relicIcons;

    // ── 조회 헬퍼 ────────────────────────────────────────────────────────
    public Sprite GetFrameSprite(Grade grade)
    {
        foreach (var e in frameGrades)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    public Sprite GetIconSprite(Grade grade)
    {
        foreach (var e in iconGrades)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    public Sprite GetBannerSprite(Grade grade)
    {
        foreach (var e in bannerGrades)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    public Sprite GetRelicIcon(string relicId)
    {
        foreach (var e in relicIcons)
            if (e.relicId == relicId) return e.icon;
        return null;
    }

    // RelicInventory의 gradeIndex(0~4) → Grade 변환
    public static Grade FromIndex(int gradeIndex) => (Grade)Mathf.Clamp(gradeIndex, 0, 4);

    // DB f_등급 문자열 → Grade 변환
    public static Grade ParseGrade(string gradeStr) => gradeStr switch
    {
        "고급" or "Rare"       => Grade.Rare,
        "희귀" or "Epic"       => Grade.Epic,
        "전설" or "Legendary"  => Grade.Legendary,
        "신화" or "Mythic"     => Grade.Mythic,
        _                      => Grade.Common,
    };

    // ── 전역 접근 ─────────────────────────────────────────────────────────
    private static RelicVisualConfig _shared;
    public static RelicVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<RelicVisualConfig>("Relic/RelicVisualConfig");
            return _shared;
        }
    }
}
