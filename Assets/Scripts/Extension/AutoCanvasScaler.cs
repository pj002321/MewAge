using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 비율에 따라 CanvasScaler의 matchWidthOrHeight를 자동 결정합니다.
/// - 화면이 기준보다 세로로 길면 → Width 기준 (좌우 꽉 채움, 위아래 여백)
/// - 화면이 기준보다 가로로 넓으면 → Height 기준 (위아래 꽉 채움, 좌우 여백)
/// HomeUICanvas 등 최상위 Canvas에 부착하세요.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class AutoCanvasScaler : MonoBehaviour
{
    CanvasScaler _scaler;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    void Apply()
    {
        float refRatio    = _scaler.referenceResolution.x / _scaler.referenceResolution.y;
        float screenRatio = (float)Screen.width / Screen.height;

        // 화면이 기준보다 세로로 길수록 → Width 기준
        // 화면이 기준보다 가로로 넓을수록 → Height 기준
        _scaler.matchWidthOrHeight = screenRatio < refRatio ? 0f : 1f;
    }
}
