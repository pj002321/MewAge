using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전반의 TextStyleApplier를 관리합니다.
/// ApplyAll()을 호출하면 등록된 모든 텍스트에 프리셋이 즉시 재적용됩니다.
/// </summary>
public class FontManager : MonoBehaviourSingleton<FontManager>
{
    protected override bool DontDestroy => true;

    private static readonly List<TextStyleApplier> _appliers = new();

    public static void Register(TextStyleApplier a)
    {
        if (!_appliers.Contains(a)) _appliers.Add(a);
    }

    public static void Unregister(TextStyleApplier a) => _appliers.Remove(a);

    /// <summary>등록된 모든 텍스트에 프리셋을 재적용합니다.</summary>
    public static void ApplyAll()
    {
        for (int i = _appliers.Count - 1; i >= 0; i--)
        {
            if (_appliers[i] == null) { _appliers.RemoveAt(i); continue; }
            _appliers[i].Apply();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _appliers.Clear();
}
