using System;
using UnityEngine;

/// <summary>
/// 증강 ID(ImprovType)별 아이콘 스프라이트를 관리하는 ScriptableObject.
/// Resources/AUG/ImprovVisualConfig 에 저장하면 Shared로 전역 접근됩니다.
///
/// 사용처: ImprovCard, StatPanelUI (획득 증강 아이콘 목록)
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Improv Visual Config", fileName = "ImprovVisualConfig")]
public class ImprovVisualConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ImprovType type;
        public Sprite     icon;
    }

    [Header("증강 ID → 아이콘 (ImprovType 열거형 순서대로 설정)")]
    public Entry[] entries;

    /// <summary>ImprovType으로 스프라이트를 반환합니다.</summary>
    public Sprite GetSprite(ImprovType type)
    {
        foreach (var e in entries)
            if (e.type == type) return e.icon;
        return null;
    }

    /// <summary>DB 문자열(예: "AUG_ATK")로 스프라이트를 반환합니다.</summary>
    public Sprite GetSprite(string idStr)
    {
        if (string.IsNullOrEmpty(idStr)) return null;
        return Enum.TryParse<ImprovType>(idStr, out var type) ? GetSprite(type) : null;
    }

    // ── 전역 접근 ────────────────────────────────────────────────────
    private static ImprovVisualConfig _shared;
    public static ImprovVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<ImprovVisualConfig>("AUG/ImprovVisualConfig");
            return _shared;
        }
    }
}
