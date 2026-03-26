using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AspectRatioFitter의 aspectRatio를 현재 화면 비율로 자동 설정합니다.
/// AspectRatioFitter가 붙은 오브젝트에 함께 부착하세요.
/// </summary>
[RequireComponent(typeof(AspectRatioFitter))]
public class ScreenAspectFitter : MonoBehaviour
{
    AspectRatioFitter _fitter;

    void Awake()
    {
        _fitter = GetComponent<AspectRatioFitter>();
        Apply();
    }

    void Apply()
    {
        float ratio = (float)Screen.width / Screen.height;
        // 9:16(0.5625)보다 세로로 긴 화면(iPhone 등) → 0.5 고정
        // 9:16 이하(넓은 화면) → 실제 비율 사용
        _fitter.aspectRatio = ratio < 0.5625f ? 0.5f : ratio;
    }
}
