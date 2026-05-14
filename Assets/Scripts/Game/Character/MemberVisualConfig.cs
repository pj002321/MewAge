using System;
using UnityEngine;

/// <summary>
/// 대원(캐릭터) 패널 비주얼 에셋 관리 ScriptableObject.
/// Resources/Member/MemberVisualConfig 에 저장하면 Shared로 전역 접근 가능합니다.
///
/// 사용처:
///   skillIcons     — f_스킬ID 별 스킬 아이콘
///   gradeVisuals   — 캐릭터 등급(C/B/A/S/SS)별 패널 활성·비활성 이미지 + 인덱스 이미지
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Member Visual Config", fileName = "MemberVisualConfig")]
public class MemberVisualConfig : ScriptableObject
{
    [Serializable]
    public struct SkillIconEntry
    {
        public string skillId;
        public Sprite icon;
    }

    [Serializable]
    public struct CharacterGradeVisual
    {
        [Tooltip("C / B / A / S / SS")]
        public string grade;
        public Sprite panelActive;    // 해금된(활성) 패널 배경
        public Sprite panelInactive;  // 잠긴(비활성) 패널 배경
        public Sprite indexUnlocked;  // 해금 인덱스 아이콘
        public Sprite indexLocked;    // 잠금 인덱스 아이콘
    }

    [Serializable]
    public struct CharacterThumbnailEntry
    {
        [CharacterID] public string charId;
        public Sprite     thumbnail;
        public GameObject modelPrefab;
    }

    [Header("스킬 ID별 아이콘")]
    public SkillIconEntry[] skillIcons;

    [Header("캐릭터 등급별 비주얼 (패널 활성/비활성 · 인덱스 해금/잠금)")]
    public CharacterGradeVisual[] gradeVisuals;

    [Header("캐릭터 썸네일 + 모델 프리팹 (charId → Sprite / Prefab)")]
    public CharacterThumbnailEntry[] characterThumbnails;

    // ── 조회 헬퍼 ─────────────────────────────────────────────────────────

    public Sprite GetSkillIcon(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        foreach (var e in skillIcons)
            if (e.skillId == skillId) return e.icon;
        return null;
    }

    public Sprite GetPanelSprite(string grade, bool active)
    {
        foreach (var e in gradeVisuals)
            if (e.grade == grade) return active ? e.panelActive : e.panelInactive;
        return null;
    }

    public Sprite GetIndexSprite(string grade, bool unlocked)
    {
        foreach (var e in gradeVisuals)
            if (e.grade == grade) return unlocked ? e.indexUnlocked : e.indexLocked;
        return null;
    }

    public Sprite GetThumbnail(string charId)
    {
        if (string.IsNullOrEmpty(charId)) return null;
        foreach (var e in characterThumbnails)
            if (e.charId == charId) return e.thumbnail;
        return null;
    }

    public GameObject GetModelPrefab(string charId)
    {
        if (string.IsNullOrEmpty(charId)) return null;
        foreach (var e in characterThumbnails)
            if (e.charId == charId) return e.modelPrefab;
        return null;
    }

    // ── 전역 접근 ─────────────────────────────────────────────────────────
    private static MemberVisualConfig _shared;
    public static MemberVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<MemberVisualConfig>("Character/MemberVisualConfig");
            return _shared;
        }
    }
}
