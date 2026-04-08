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
        float screenRatio = (float)Screen.width / Screen.height;

        // 세로 모드(portrait) → Width 기준(0f): 너비 1080px 고정, 기기가 길수록 세로 공간 확보
        //   16:9  → 캔버스 1080×1920  (에뮬레이터 포함)
        //   19.5:9 → 캔버스 1080×2340  (실제 기기)
        // 가로 모드(landscape) → Height 기준(1f)
        _scaler.matchWidthOrHeight = screenRatio < 1f ? 0f : 1f;
    }
}
