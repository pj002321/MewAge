using System;
using UnityEngine;

/// <summary>
/// 증강 ID(ImprovType)별 비주얼 에셋을 관리하는 ScriptableObject.
/// Resources/AUG/ImprovVisualConfig 에 저장하면 Shared로 전역 접근됩니다.
/// MuAge/Augment Visual Config 에디터 창에서 편집하세요.
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Improv Visual Config", fileName = "ImprovVisualConfig")]
public class ImprovVisualConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ImprovType type;
        public Sprite     icon;
        public GameObject companionPrefab;  // 동반자형 증강 (고사리, 야수)
        public GameObject projectilePrefab; // 투사체형 증강 (진흙탕)
        public GameObject playerVfxPrefab;  // 플레이어 이펙트형 (빙하기)
    }

    [Header("MuAge/Augment Visual Config 에디터 창에서 편집하세요.")]
    public Entry[] entries;

    public Sprite GetSprite(ImprovType type)
    {
        foreach (var e in entries)
            if (e.type == type) return e.icon;
        return null;
    }

    public Sprite GetSprite(string idStr)
    {
        if (string.IsNullOrEmpty(idStr)) return null;
        return Enum.TryParse<ImprovType>(idStr, out var type) ? GetSprite(type) : null;
    }

    public Entry GetEntry(ImprovType type)
    {
        foreach (var e in entries)
            if (e.type == type) return e;
        return default;
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
