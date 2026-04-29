using System;
using UnityEngine;

/// <summary>
/// 대원(캐릭터) 패널 비주얼 에셋 관리 ScriptableObject.
/// Resources/Member/MemberVisualConfig 에 저장하면 Shared로 전역 접근 가능합니다.
///
/// 사용처:
///   skillIcons        — f_스킬ID 별 스킬 아이콘
///   augmentIndexImages — 증강 레벨(1~4)별 해금/잠금 인덱스 이미지
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
    public struct AugmentIndexEntry
    {
        [Tooltip("1~3 = 속성 레벨, 4 = 고유능력")]
        public int    level;
        public Sprite unlocked;
        public Sprite locked;
    }

    [Header("스킬 ID별 아이콘")]
    public SkillIconEntry[] skillIcons;

    [Header("증강 레벨별 인덱스 이미지 (레벨 1~4, 해금/잠금)")]
    public AugmentIndexEntry[] augmentIndexImages;

    public Sprite GetSkillIcon(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        foreach (var e in skillIcons)
            if (e.skillId == skillId) return e.icon;
        return null;
    }

    public Sprite GetAugmentIndex(int level, bool unlocked)
    {
        foreach (var e in augmentIndexImages)
            if (e.level == level) return unlocked ? e.unlocked : e.locked;
        return null;
    }

    // ── 전역 접근 ─────────────────────────────────────────────────────────
    private static MemberVisualConfig _shared;
    public static MemberVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<MemberVisualConfig>("Member/MemberVisualConfig");
            return _shared;
        }
    }
}
