using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 여러 기물 스킬이 동시에 Global Light2D를 어둡게 할 때
/// 레퍼런스 카운트로 충돌 없이 관리합니다.
///
/// 사용법:
///   Dim()   — 스킬 시작 시 호출 (카운트 +1, 가장 어두운 값 적용)
///   Release() — 스킬 종료/파괴 시 호출 (카운트 -1, 0이 되면 1f 복구)
///   SetIntensity() — 페이드 코루틴에서 직접 밝기 지정
/// </summary>
public static class GlobalLightDimmer
{
    private static int _dimCount = 0;

    /// <summary>스킬이 조명을 어둡게 쓰기 시작할 때 등록합니다.</summary>
    public static void Register() => _dimCount++;

    /// <summary>스킬 종료/파괴 시 호출. 마지막 스킬이 해제되면 즉시 복구합니다.</summary>
    public static void Release()
    {
        _dimCount = Mathf.Max(0, _dimCount - 1);
        if (_dimCount == 0)
            SetIntensity(1f);
    }

    /// <summary>현재 활성 스킬이 있는지 여부.</summary>
    public static bool IsActive => _dimCount > 0;

    /// <summary>Global Light2D intensity를 직접 설정합니다 (페이드 코루틴용).</summary>
    public static void SetIntensity(float intensity)
    {
        var light = FindGlobalLight();
        if (light != null) light.intensity = intensity;
    }

    public static Light2D FindGlobalLight()
    {
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) return l;
        return null;
    }
}
